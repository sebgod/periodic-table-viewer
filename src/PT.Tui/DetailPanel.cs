using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Multi-row detail card for the currently-selected element. Always renders
/// the four lines [name + Z], [symbol + group/period/block], [atomic weight],
/// [electron configuration]. The body is a small Markdown document fed to a
/// nested <see cref="CL.MarkdownWidget"/>, so the electron-config row is
/// rendered through the LaTeX inline-math path (<c>\(1s^{2}\,2s^{2}\,…\)</c>)
/// rather than via bespoke Unicode-superscript baking.
/// </summary>
public sealed class DetailPanel : CL.Widget
{
    public const int Rows = 5;

    private readonly CL.MarkdownWidget _md;
    private Element _element = Elements.ByAtomicNumber[1];

    public DetailPanel(CL.ITerminalViewport viewport) : base(viewport)
    {
        _md = new CL.MarkdownWidget(viewport).Markdown(BuildMarkdown(_element));
    }

    public void SetElement(Element e)
    {
        _element = e;
        _md.Markdown(BuildMarkdown(e));
    }

    public override void Render() => _md.Render();

    /// <summary>
    /// Builds the 5-block markdown body. Layout (one output row per block):
    /// thematic-break divider, name + Z, symbol + group/period/block, atomic
    /// weight, electron config (LaTeX inline math).
    /// </summary>
    internal static string BuildMarkdown(Element e)
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
        var config = ElectronConfig.ExpandLatex(e);
        var block = e.Block.ToString().ToLowerInvariant();
        var bandSuffix = band.Length > 0 ? $"   {band}" : "";

        return string.Join("\n\n",
            "---",
            $"**{e.Name}** *#{e.AtomicNumber}*",
            $"**{e.Symbol}**   group {grp}   period {e.Period}   block {block}{bandSuffix}",
            $"atomic weight: {weight}",
            $"""config: \({config}\)""");
    }
}
