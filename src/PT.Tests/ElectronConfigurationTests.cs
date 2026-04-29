using PeriodicTable;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

/// <summary>
/// Verifies that every element's <see cref="Element.ElectronConfiguration"/>
/// string parses back to exactly Z electrons. Catches typos in the data table
/// (a missing superscript, a wrong noble-gas core, a transposed digit).
///
/// Cross-checked against the NIST Chemistry WebBook ground-state configs;
/// known anomalies (Cr, Cu, Mo, Ru, Rh, Pd, Ag, La, Ce, Gd, Pt, Au, Ac, Th,
/// Pa, U, Np, Cm, Lr) are pinned with explicit asserts.
/// </summary>
public class ElectronConfigurationTests
{
    [Theory]
    [MemberData(nameof(AllZ))]
    public void ConfigSumEquals_AtomicNumber(int z)
    {
        var e = Elements.ByAtomicNumber[z];
        var sum = SumElectrons(e.ElectronConfiguration);
        sum.ShouldBe(z, $"{e.Symbol} ({e.AtomicNumber}): \"{e.ElectronConfiguration}\" parsed to {sum} electrons");
    }

    public static IEnumerable<object[]> AllZ =>
        Enumerable.Range(1, 118).Select(z => new object[] { z });

    // Spot-check the known ground-state anomalies. If any of these regress,
    // the data table is wrong. Source: NIST Atomic Reference Data.
    [Theory]
    [InlineData(24, "Cr", "[Ar] 3d⁵ 4s¹")]
    [InlineData(29, "Cu", "[Ar] 3d¹⁰ 4s¹")]
    [InlineData(41, "Nb", "[Kr] 4d⁴ 5s¹")]
    [InlineData(42, "Mo", "[Kr] 4d⁵ 5s¹")]
    [InlineData(44, "Ru", "[Kr] 4d⁷ 5s¹")]
    [InlineData(45, "Rh", "[Kr] 4d⁸ 5s¹")]
    [InlineData(46, "Pd", "[Kr] 4d¹⁰")]
    [InlineData(47, "Ag", "[Kr] 4d¹⁰ 5s¹")]
    [InlineData(57, "La", "[Xe] 5d¹ 6s²")]
    [InlineData(58, "Ce", "[Xe] 4f¹ 5d¹ 6s²")]
    [InlineData(64, "Gd", "[Xe] 4f⁷ 5d¹ 6s²")]
    [InlineData(78, "Pt", "[Xe] 4f¹⁴ 5d⁹ 6s¹")]
    [InlineData(79, "Au", "[Xe] 4f¹⁴ 5d¹⁰ 6s¹")]
    // Mercury — user flagged this region; verifies the regular [Xe] 4f¹⁴ 5d¹⁰ 6s²
    // (i.e. NO anomaly for Hg, in contrast to its neighbours Pt/Au).
    [InlineData(80, "Hg", "[Xe] 4f¹⁴ 5d¹⁰ 6s²")]
    [InlineData(89, "Ac", "[Rn] 6d¹ 7s²")]
    [InlineData(90, "Th", "[Rn] 6d² 7s²")]
    [InlineData(91, "Pa", "[Rn] 5f² 6d¹ 7s²")]
    [InlineData(92, "U",  "[Rn] 5f³ 6d¹ 7s²")]
    [InlineData(93, "Np", "[Rn] 5f⁴ 6d¹ 7s²")]
    [InlineData(96, "Cm", "[Rn] 5f⁷ 6d¹ 7s²")]
    // Lawrencium has the relativistic 7p¹ ground state per 2017 measurement —
    // not the older predicted [Rn] 5f¹⁴ 6d¹ 7s².
    [InlineData(103, "Lr", "[Rn] 5f¹⁴ 7s² 7p¹")]
    public void Anomaly_Pinned(int z, string symbol, string expected)
    {
        var e = Elements.ByAtomicNumber[z];
        e.Symbol.ShouldBe(symbol);
        e.ElectronConfiguration.ShouldBe(expected);
    }

    // Block continuity: noble-gas cores progress in the expected order, and
    // every config above [He] starts with one of the noble-gas brackets.
    [Fact]
    public void EveryConfigBeyondHydrogen_StartsWithNobleGasBracket()
    {
        foreach (var e in Elements.All)
        {
            if (e.AtomicNumber <= 2)
            {
                e.ElectronConfiguration.ShouldStartWith("1s");
                continue;
            }
            e.ElectronConfiguration.ShouldStartWith("[", customMessage:
                $"{e.Symbol} should use noble-gas shorthand");
            var endIdx = e.ElectronConfiguration.IndexOf(']');
            endIdx.ShouldBeGreaterThan(0);
            var coreSym = e.ElectronConfiguration[1..endIdx];
            new[] { "He", "Ne", "Ar", "Kr", "Xe", "Rn" }.ShouldContain(coreSym);
            // Core must have lower Z than this element.
            Elements.BySymbol[coreSym].AtomicNumber.ShouldBeLessThan(e.AtomicNumber);
        }
    }

    private static int SumElectrons(string config)
    {
        int total = 0;
        foreach (var raw in config.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length >= 3 && raw[0] == '[' && raw[^1] == ']')
            {
                var sym = raw[1..^1];
                total += Elements.BySymbol[sym].AtomicNumber;
                continue;
            }
            // Subshell token: <n><letter><superscript-digits>+
            // e.g. "4s¹", "3d¹⁰", "5f¹⁴". n and letter are 1 char each;
            // remainder is one or more Unicode superscript digits.
            if (raw.Length < 3) continue;
            int i = 2;
            int count = 0;
            while (i < raw.Length)
            {
                var d = SuperToInt(raw[i]);
                if (d < 0) throw new FormatException(
                    $"Unexpected char '{raw[i]}' (U+{(int)raw[i]:X4}) in subshell token \"{raw}\"");
                count = count * 10 + d;
                i++;
            }
            count.ShouldBeGreaterThan(0, $"subshell token \"{raw}\" parsed to 0");
            total += count;
        }
        return total;
    }

    private static int SuperToInt(char c) => c switch
    {
        '\u2070' => 0, // ⁰
        '\u00B9' => 1, // ¹
        '\u00B2' => 2, // ²
        '\u00B3' => 3, // ³
        '\u2074' => 4, // ⁴
        '\u2075' => 5, // ⁵
        '\u2076' => 6, // ⁶
        '\u2077' => 7, // ⁷
        '\u2078' => 8, // ⁸
        '\u2079' => 9, // ⁹
        _ => -1,
    };
}
