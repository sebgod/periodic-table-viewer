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

    /// <summary>A math mode was supplied, so the expanded body is available at all.</summary>
    private readonly bool _mathCapable;

    /// <summary>Which body <see cref="_md"/> currently holds; see <see cref="Render"/>.</summary>
    private bool _expanded;

    public DetailPanel(CL.ITerminalViewport viewport,
        CL.BoxRenderMode? mathMode = null, string? mathFontPath = null)
        : base(viewport)
    {
        _mathCapable = mathMode is not null;
        _expanded = _mathCapable;
        _md = new CL.MarkdownWidget(viewport)
        {
            // Left set even when the card renders compact: compact emits only INLINE math, which is
            // always single-row Unicode regardless of mode, so the mode is inert until a $$…$$ block
            // appears. That is what lets Render flip between the two bodies without rebuilding _md.
            MathMode = mathMode,
            MathFontPath = mathFontPath,
        }.Markdown(BuildMarkdown(_element, _expanded));
    }

    public void SetElement(Element e)
    {
        _element = e;
        _md.Markdown(BuildMarkdown(e, _expanded));
    }

    /// <summary>
    /// Renders the card, first re-bodying it if the rows it was ALLOCATED no longer match the body it is
    /// holding.
    ///
    /// <para>Expansion has to be a function of the current viewport rather than a flag set once at
    /// construction: <see cref="ViewerFrameLayout"/> re-costs the frame on every resize, so the same
    /// panel can be handed 16 rows and then 5. A card that kept its startup answer would either draw a
    /// math block into five rows or leave eleven rows blank. (<see cref="Soft.SixelDecayChainPanel"/>
    /// reached the same conclusion independently for its legend — see its <c>MathLegendMinRows</c>.)</para>
    /// </summary>
    public override void Render()
    {
        var expanded = _mathCapable && Viewport.Size.Height >= RowsExpanded;
        if (expanded != _expanded)
        {
            _expanded = expanded;
            _md.Markdown(BuildMarkdown(_element, expanded));
        }

        _md.Render();
    }

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
