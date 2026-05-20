using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Multi-row detail card for the currently-selected element. Renders the
/// header lines (name + Z, symbol + group/period/block, atomic weight) as
/// plain Markdown, and the electron configuration as a <c>$$…$$</c> display-
/// math block so it can go through the pixel-render path (Sextant / HalfBlock /
/// Sixel) when the caller supplies a math mode + font. Falls back to single-row
/// Unicode super/subscripts when <c>mathMode</c> is null.
/// </summary>
public sealed class DetailPanel : CL.Widget
{
    /// <summary>
    /// Compact layout: divider + name + symbol + atomic-weight + single-row
    /// electron config (Unicode super/subscripts). Used when no math font is
    /// available or the terminal is too short to spare extra rows for the
    /// pixel-rendered block.
    /// </summary>
    public const int RowsCompact = 5;

    /// <summary>
    /// Expanded layout: the compact rows above plus a "config: [Rn]" prefix
    /// row and a multi-row <c>$$…$$</c> outer-shell math block. Sextant at
    /// 12pt typically emits ~5 rows for the outer-shell form (the noble-gas
    /// prefix lives outside the fence — the math grammar can't parse literal
    /// <c>[…]</c>; see <see cref="ElectronConfig.SplitShorthandLatex"/>).
    /// </summary>
    public const int RowsExpanded = 16;

    private readonly CL.MarkdownWidget _md;
    private Element _element = Elements.ByAtomicNumber[1];

    private readonly bool _expanded;

    public DetailPanel(CL.ITerminalViewport viewport,
        CL.BoxRenderMode? mathMode = null, string? mathFontPath = null)
        : base(viewport)
    {
        _expanded = mathMode is not null;
        _md = new CL.MarkdownWidget(viewport)
        {
            MathMode = mathMode,
            MathFontPath = mathFontPath,
        }.Markdown(BuildMarkdown(_element, _expanded));
    }

    public void SetElement(Element e)
    {
        _element = e;
        _md.Markdown(BuildMarkdown(e, _expanded));
    }

    public override void Render() => _md.Render();

    /// <summary>
    /// Builds the markdown body. Header lines stay plain text + inline emphasis.
    /// <para>
    /// In compact mode (<paramref name="expanded"/> = false): the electron
    /// configuration is one inline <c>\(…\)</c> math span — single-row Unicode
    /// super/subscripts. Five output rows total, identical to the pre-pixel-
    /// path layout.
    /// </para>
    /// <para>
    /// In expanded mode: the noble-gas prefix sits on its own "config: [Rn]"
    /// row and only the outer-shell remainder goes inside a <c>$$ … $$</c>
    /// fence (delimiters on their own lines so the block grammar opens an
    /// <c>MdMathBlock</c> instead of inline math). The math grammar bails on
    /// literal <c>[…]</c>, so keeping the prefix outside the fence is what
    /// stops the pixel path from silently falling back to Unicode.
    /// </para>
    /// </summary>
    internal static string BuildMarkdown(Element e, bool expanded)
    {
        var grp = e.Group?.ToString() ?? "—";
        var band = e.Category switch
        {
            Category.Lanthanide => "Lanthanide",
            Category.Actinide => "Actinide",
            _ => "",
        };
        var weight = e.IsSynthetic
            ? $"({(int)System.Math.Round(e.AtomicWeight)})"
            : e.AtomicWeight.ToString("F4");
        var block = e.Block.ToString().ToLowerInvariant();
        var bandSuffix = band.Length > 0 ? $"   {band}" : "";

        if (!expanded)
        {
            var configLatex = ElectronConfig.ExpandLatex(e);
            return string.Join("\n\n",
                "---",
                $"**{e.Name}** *#{e.AtomicNumber}*",
                $"**{e.Symbol}**   group {grp}   period {e.Period}   block {block}{bandSuffix}",
                $"atomic weight: {weight}",
                $"""config: \({configLatex}\)""");
        }

        var (prefix, outerLatex) = ElectronConfig.SplitShorthandLatex(e);
        var configHeader = prefix.Length > 0 ? $"config: {prefix}" : "config:";
        return string.Join("\n\n",
            "---",
            $"**{e.Name}** *#{e.AtomicNumber}*",
            $"**{e.Symbol}**   group {grp}   period {e.Period}   block {block}{bandSuffix}",
            $"atomic weight: {weight}",
            configHeader,
            $"$$\n{outerLatex}\n$$");
    }
}
