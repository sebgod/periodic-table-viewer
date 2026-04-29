using PeriodicTable;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

public class ElementsTests
{
    [Fact]
    public void All118Present()
    {
        Elements.All.Count.ShouldBe(118);
        for (int z = 1; z <= 118; z++)
            Elements.ByAtomicNumber.ContainsKey(z).ShouldBeTrue($"missing Z={z}");
    }

    [Fact]
    public void SymbolsAreUnique()
    {
        Elements.All
            .GroupBy(e => e.Symbol)
            .Where(g => g.Count() > 1)
            .ShouldBeEmpty();
        Elements.BySymbol.Count.ShouldBe(118);
    }

    [Fact]
    public void NamesAreUnique()
    {
        Elements.All
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Lanthanides_57_to_71_AreLanthanide()
    {
        for (int z = 57; z <= 71; z++)
            Elements.ByAtomicNumber[z].Category.ShouldBe(Category.Lanthanide);
    }

    [Fact]
    public void Actinides_89_to_103_AreActinide()
    {
        for (int z = 89; z <= 103; z++)
            Elements.ByAtomicNumber[z].Category.ShouldBe(Category.Actinide);
    }

    [Fact]
    public void NobleGases_AreGroup18()
    {
        foreach (var sym in new[] { "He", "Ne", "Ar", "Kr", "Xe", "Rn", "Og" })
        {
            var e = Elements.BySymbol[sym];
            e.Group.ShouldBe(18, $"{sym} should be group 18");
            e.Category.ShouldBe(Category.NobleGas, $"{sym} should be noble gas");
        }
    }

    [Fact]
    public void AlkaliMetals_AreGroup1()
    {
        foreach (var sym in new[] { "Li", "Na", "K", "Rb", "Cs", "Fr" })
        {
            var e = Elements.BySymbol[sym];
            e.Group.ShouldBe(1);
            e.Category.ShouldBe(Category.AlkaliMetal);
        }
    }

    [Fact]
    public void Hydrogen_IsGroup1_ButNotAlkaliMetal()
    {
        var h = Elements.BySymbol["H"];
        h.Group.ShouldBe(1);
        h.Category.ShouldBe(Category.ReactiveNonmetal);
    }

    // Bug fix from physics/atoms.pl: sulfur was Z=15 (collision with phosphorus).
    [Fact]
    public void Sulfur_IsZ16()
    {
        Elements.BySymbol["S"].AtomicNumber.ShouldBe(16);
        Elements.BySymbol["P"].AtomicNumber.ShouldBe(15);
    }

    // Bug fix from physics/atoms.pl: Hf was period 7.
    [Fact]
    public void Hafnium_IsPeriod6()
    {
        Elements.BySymbol["Hf"].Period.ShouldBe(6);
    }

    // Bug fix: Prolog had "flurine" / "ribidium" / "tellerium".
    [Theory]
    [InlineData("F", "Fluorine")]
    [InlineData("Rb", "Rubidium")]
    [InlineData("Te", "Tellurium")]
    public void ProlongTyposCorrected(string symbol, string expectedName)
    {
        Elements.BySymbol[symbol].Name.ShouldBe(expectedName);
    }

    // Bug fix: 113/115/117/118 had old IUPAC placeholder names.
    [Theory]
    [InlineData(113, "Nh", "Nihonium")]
    [InlineData(115, "Mc", "Moscovium")]
    [InlineData(117, "Ts", "Tennessine")]
    [InlineData(118, "Og", "Oganesson")]
    public void ModernNamesForSuperheavies(int z, string symbol, string name)
    {
        var e = Elements.ByAtomicNumber[z];
        e.Symbol.ShouldBe(symbol);
        e.Name.ShouldBe(name);
    }

    [Fact]
    public void LanthanidesAndActinides_HaveNullGroup()
    {
        foreach (var e in Elements.All)
        {
            if (e.Category is Category.Lanthanide or Category.Actinide)
                e.Group.ShouldBeNull($"{e.Symbol} f-block element should have null Group");
            else
                e.Group.ShouldNotBeNull($"{e.Symbol} should have a Group");
        }
    }

    [Fact]
    public void FBlockIndex_ForLanthanidesIs0Through14()
    {
        for (int z = 57; z <= 71; z++)
            Elements.ByAtomicNumber[z].FBlockIndex.ShouldBe(z - 57);
    }

    [Fact]
    public void Synthetic_IsNotEmpty_AndDoesNotIncludeBismuth()
    {
        // Bismuth (Z=83) is officially synthetic by half-life but here we treat
        // long-half-life elements as non-synthetic; only intrinsically unstable
        // / lab-only elements are flagged.
        var synthetics = Elements.All.Where(e => e.IsSynthetic).Select(e => e.AtomicNumber).ToList();
        synthetics.Count.ShouldBeGreaterThan(20);
        synthetics.ShouldNotContain(83); // Bi
        synthetics.ShouldContain(43);    // Tc
        synthetics.ShouldContain(95);    // Am
        synthetics.ShouldContain(118);   // Og
    }
}
