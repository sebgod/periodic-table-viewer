using Console.Lib;
using PeriodicTable;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

/// <summary>
/// Covers <see cref="ElectronConfig.ExpandLatex"/>: source-string shape, and a
/// round-trip through <see cref="MarkdownRenderer.RenderLines"/> to confirm
/// the LaTeX inline-math path produces Unicode-superscript output (i.e. that
/// the Markdown widget actually wires the <c>\(…\)</c> spans through
/// LatexUnicodeVisitor rather than emitting raw <c>^{N}</c> source).
/// </summary>
public class ElectronConfigLatexTests
{
    [Theory]
    [InlineData(1,   "1s^{1}")]                                                        // H
    [InlineData(2,   "1s^{2}")]                                                        // He
    [InlineData(11,  "1s^{2}\\,2s^{2}\\,2p^{6}\\,3s^{1}")]                             // Na
    [InlineData(18,  "1s^{2}\\,2s^{2}\\,2p^{6}\\,3s^{2}\\,3p^{6}")]                    // Ar
    [InlineData(29,  "3d^{10}\\,4s^{1}")]                                              // Cu (anomaly): suffix only
    [InlineData(46,  "4d^{10}")]                                                       // Pd (anomaly): no 5s
    public void ExpandLatex_KnownElements(int z, string expectedFragment)
    {
        var e = Elements.ByAtomicNumber[z];
        var latex = ElectronConfig.ExpandLatex(e);
        latex.ShouldContain(expectedFragment);
    }

    [Fact]
    public void ExpandLatex_NoUnicodeSupersRemain()
    {
        // After ExpandLatex, every superscript digit must have been converted
        // into a `^{N}` group — no raw Unicode super-digits should survive.
        // The compact form already has them; ExpandLatex must rewrite all.
        foreach (var e in Elements.All)
        {
            var latex = ElectronConfig.ExpandLatex(e);
            foreach (var ch in latex)
            {
                IsUnicodeSuperDigit(ch).ShouldBeFalse(
                    $"{e.Symbol}: ExpandLatex result still contains Unicode super '{ch}': \"{latex}\"");
            }
        }
    }

    [Theory]
    [InlineData(1,  '¹')]   // H → 1s^{1} renders as 1s¹
    [InlineData(2,  '²')]   // He → 1s^{2} renders as 1s²
    [InlineData(7,  '³')]   // N → ... 2p^{3} → 2p³
    [InlineData(8,  '⁴')]   // O → ... 2p^{4} → 2p⁴
    [InlineData(11, '⁶')]   // Na → ... 2p^{6} ... renders with ⁶
    [InlineData(13, '⁶')]   // Al → ... 2p⁶ ...
    public void MarkdownRender_LatexInlineEmitsUnicodeSupers(int z, char expectedSuper)
    {
        var e = Elements.ByAtomicNumber[z];
        var latex = ElectronConfig.ExpandLatex(e);
        var src = $"config: \\({latex}\\)";

        var lines = MarkdownRenderer.RenderLines(src, width: 200, ColorMode.None);
        var joined = string.Join("\n", lines);

        joined.ShouldContain(expectedSuper.ToString());
        // And no raw ^{ leftovers (would mean the math span didn't parse).
        joined.ShouldNotContain("^{");
    }

    private static bool IsUnicodeSuperDigit(char c) => c is
        '⁰' or '¹' or '²' or '³' or '⁴' or '⁵' or '⁶' or '⁷' or '⁸' or '⁹';

}
