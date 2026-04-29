using PeriodicTable.Tui.Soft;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Multi-row detail card for the currently-selected element. Always renders
/// the four lines [name + Z], [symbol + group/period/block], [atomic weight],
/// [electron configuration].
/// </summary>
public sealed class DetailPanel : CL.Widget
{
    public const int Rows = 5;

    private Element _element = Elements.ByAtomicNumber[1];

    public DetailPanel(CL.ITerminalViewport viewport) : base(viewport) { }

    public void SetElement(Element e) => _element = e;

    public override void Render()
    {
        var mode = Viewport.ColorMode;
        int width = Viewport.Size.Width;
        if (width <= 0) return;

        var dim = new CL.VtStyle(CL.SgrColor.BrightBlack, CL.SgrColor.Black);
        var label = new CL.VtStyle(CL.SgrColor.BrightWhite, CL.SgrColor.Black);
        var value = new CL.VtStyle(CL.SgrColor.BrightYellow, CL.SgrColor.Black);

        // Top divider line.
        if (TrySetCursorPosition(Viewport, 0, 0))
            Viewport.Write(new string('\u2500', width));

        Write(1, $"{_element.Name}", label, $" #{_element.AtomicNumber}", dim);

        var grp = _element.Group?.ToString() ?? "—";
        var bandText = _element.Category switch
        {
            Category.Lanthanide => "Lanthanide",
            Category.Actinide => "Actinide",
            _ => "",
        };
        Write(2, $"  {_element.Symbol}", value,
                  $"   group {grp,-3} period {_element.Period}  block {_element.Block.ToString().ToLowerInvariant()}  {bandText}",
                  dim);

        var weight = _element.IsSynthetic
            ? $"({(int)Math.Round(_element.AtomicWeight)})"
            : _element.AtomicWeight.ToString("F4");
        Write(3, "  atomic weight: ", dim, weight, value);

        Write(4, "  config: ", dim, _element.ElectronConfiguration, value);
    }

    private void Write(int row, string left, CL.VtStyle leftStyle, string right, CL.VtStyle rightStyle)
    {
        if (!TrySetCursorPosition(Viewport, 0, row)) return;
        var mode = Viewport.ColorMode;
        int used = left.Length + right.Length;
        int width = Viewport.Size.Width;
        var pad = used >= width ? "" : new string(' ', width - used);
        Viewport.Write(
            $"{leftStyle.Apply(mode)}{left}" +
            $"{rightStyle.Apply(mode)}{right}{pad}{CL.VtStyle.Reset}");
    }
}
