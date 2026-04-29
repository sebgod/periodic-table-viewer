using PeriodicTable;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

public class DecayTests
{
    public static IEnumerable<object[]> AllChains =>
        DecayChains.All.Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(AllChains))]
    public void EachStep_HasValidConservationLaws(string name)
    {
        var chain = DecayChains.All.Single(c => c.Name == name);
        foreach (var step in chain.Steps)
        {
            // Conservation laws by mode:
            // α:  ΔA = -4, ΔZ = -2
            // β⁻: ΔA =  0, ΔZ = +1
            // β⁺: ΔA =  0, ΔZ = -1
            // EC: ΔA =  0, ΔZ = -1
            // IT: ΔA =  0, ΔZ =  0
            // SF: not applicable (chain shouldn't contain it on the dominant path)
            var dA = step.Daughter.A - step.Parent.A;
            var dZ = step.Daughter.Z - step.Parent.Z;
            switch (step.Mode)
            {
                case DecayMode.Alpha:
                    dA.ShouldBe(-4, $"{step.Parent} →α {step.Daughter} ΔA");
                    dZ.ShouldBe(-2, $"{step.Parent} →α {step.Daughter} ΔZ");
                    break;
                case DecayMode.BetaMinus:
                    dA.ShouldBe(0,  $"{step.Parent} →β⁻ {step.Daughter} ΔA");
                    dZ.ShouldBe(+1, $"{step.Parent} →β⁻ {step.Daughter} ΔZ");
                    break;
                case DecayMode.BetaPlus:
                case DecayMode.ElectronCapture:
                    dA.ShouldBe(0,  $"{step.Parent} →{step.Mode} {step.Daughter} ΔA");
                    dZ.ShouldBe(-1, $"{step.Parent} →{step.Mode} {step.Daughter} ΔZ");
                    break;
                case DecayMode.IsomericTransition:
                    dA.ShouldBe(0);
                    dZ.ShouldBe(0);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"unexpected mode {step.Mode} on chain {name}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllChains))]
    public void Chain_IsContinuous(string name)
    {
        var chain = DecayChains.All.Single(c => c.Name == name);
        chain.Steps[0].Parent.ShouldBe(chain.Start);
        for (int i = 0; i < chain.Steps.Count - 1; i++)
        {
            chain.Steps[i].Daughter.ShouldBe(
                chain.Steps[i + 1].Parent,
                $"chain {name} discontinuity at step {i}: {chain.Steps[i]} ⇒ {chain.Steps[i + 1]}");
        }
        chain.Steps[^1].Daughter.ShouldBe(chain.End);
    }

    [Fact]
    public void EveryNuclide_ReferencesAValidElement()
    {
        foreach (var chain in DecayChains.All)
        {
            foreach (var step in chain.Steps)
            {
                Elements.ByAtomicNumber.ContainsKey(step.Parent.Z).ShouldBeTrue();
                Elements.ByAtomicNumber.ContainsKey(step.Daughter.Z).ShouldBeTrue();
                step.Parent.Symbol.ShouldNotBeEmpty();
                step.Daughter.Symbol.ShouldNotBeEmpty();
            }
        }
    }

    [Theory]
    [InlineData("U",  "Uranium series (4n+2)")]
    [InlineData("Th", "Thorium series (4n)")]
    [InlineData("Np", "Neptunium series (4n+1)")]
    [InlineData("Ac", "Actinium series (4n+3)")]
    [InlineData("Pa", "Actinium series (4n+3)")]
    public void ForElement_MapsCorrectly(string symbol, string expectedChain)
    {
        var e = Elements.BySymbol[symbol];
        DecayChains.ForElement(e)?.Name.ShouldBe(expectedChain);
    }

    [Theory]
    [InlineData("H")]
    [InlineData("C")]
    [InlineData("Fe")]
    [InlineData("Au")]
    public void ForElement_ReturnsNull_ForStableElements(string symbol)
    {
        var e = Elements.BySymbol[symbol];
        DecayChains.ForElement(e).ShouldBeNull();
    }

    [Fact]
    public void DecayMode_Symbol_ProducesUnicode()
    {
        DecayMode.Alpha.Symbol().ShouldBe("α");
        DecayMode.BetaMinus.Symbol().ShouldBe("β\u207B");
        DecayMode.BetaPlus.Symbol().ShouldBe("β\u207A");
    }

    [Fact]
    public void Mass4n_ParityIsConsistent()
    {
        // Each chain conserves A modulo 4 (alpha decreases A by 4, beta keeps A).
        foreach (var chain in DecayChains.All)
        {
            var mod = chain.Start.A % 4;
            chain.End.A.Mod(4).ShouldBe(mod, $"chain {chain.Name} parity");
            foreach (var step in chain.Steps)
            {
                step.Parent.A.Mod(4).ShouldBe(mod);
                step.Daughter.A.Mod(4).ShouldBe(mod);
            }
        }
    }
}

internal static class IntExtensions
{
    public static int Mod(this int value, int mod) => ((value % mod) + mod) % mod;
}
