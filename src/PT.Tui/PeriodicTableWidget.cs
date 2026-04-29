using PeriodicTable.Tui.Soft;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Wide-form periodic table rendered as an 18×9 grid of 4×3 cells (72×28
/// terminal cells including the gap between the main grid and the f-block).
/// Owns the selection cursor; arrow keys move to the next non-blank cell in
/// each direction, jumping into / out of the f-block as appropriate.
/// </summary>
public sealed class PeriodicTableWidget : CL.Widget
{
    // 5 cols × 3 rows per element cell. Mass strings render to 4 chars; the
    // 5th column gives one cell of horizontal padding so adjacent cells'
    // numbers don't run together (e.g. "39.140.145.0..." → "39.1 40.1 45.0").
    public const int CellWidth = 5;
    public const int CellHeight = 3;
    public const int FBlockGap = 1;

    public const int RenderedWidth = Layout.Columns * CellWidth;                         // 72
    public const int RenderedHeight = Layout.TotalRows * CellHeight + FBlockGap;         // 28

    private Element _selected;
    private (int Col, int Row) _origin;

    public PeriodicTableWidget(CL.ITerminalViewport viewport)
        : base(viewport)
    {
        _selected = Elements.ByAtomicNumber[1];
    }

    public Element Selected => _selected;

    /// <summary>Subscribe to be notified when the selected element changes.</summary>
    public event Action<Element>? SelectionChanged;

    public override void Render()
    {
        var mode = Viewport.ColorMode;

        // Center within the viewport when there's slack on either axis.
        int extraW = Math.Max(0, Viewport.Size.Width - RenderedWidth);
        int extraH = Math.Max(0, Viewport.Size.Height - RenderedHeight);
        _origin = (extraW / 2, extraH / 2);

        // No pre-clear pass: every cell writes its full background + content,
        // so the previous frame's content is always overwritten in place. A
        // separate clear pass produced a visible blank flash on selection
        // changes (cells go to spaces, then back to content one-by-one).
        // The only blank cells in the rendered area (period 1 cols 2–17,
        // periods 2–3 cols 3–12, the f-block gap row, the placeholder margins
        // around the lanthanide/actinide rows) are blanked on the very first
        // render by VirtualTerminal.Clear() / EnterAlternateScreen and never
        // re-rendered, so they stay clean.

        // F-block placeholders ("57–71" / "89–103") at column 3, periods 6/7.
        var ph6 = ToTerminal(3, 6);
        var ph7 = ToTerminal(3, 7);
        RenderFBlockPlaceholder(ph6.Col, ph6.Row, "57", "71", mode);
        RenderFBlockPlaceholder(ph7.Col, ph7.Row, "89", "103", mode);

        // Element cells (main grid + f-block rows). Selection cursor is
        // expressed via reverse-video styling on the cell (see RenderElement).
        foreach (var e in Elements.All)
        {
            var (g, p) = Layout.CellOf(e);
            var (col, row) = ToTerminal(g, p);
            RenderElement(col, row, e, isSelected: ReferenceEquals(e, _selected), mode);
        }
    }

    private (int Col, int Row) ToTerminal(int gridCol, int gridRow)
    {
        int col = _origin.Col + (gridCol - 1) * CellWidth;
        int row = _origin.Row + (gridRow - 1) * CellHeight;
        if (gridRow >= Layout.FBlockRow1) row += FBlockGap;
        return (col, row);
    }

    private void RenderElement(int col, int row, Element e, bool isSelected, CL.ColorMode mode)
    {
        var fg = CategoryFg(e.Category);
        var bg = isSelected ? CL.SgrColor.White : CL.SgrColor.Black;
        var fgEff = isSelected ? CL.SgrColor.Black : fg;
        var style = new CL.VtStyle(fgEff, bg);

        // Atomic-number style: dimmer for synthetic, brighter for selected.
        var numStyle = isSelected
            ? new CL.VtStyle(CL.SgrColor.Black, bg)
            : new CL.VtStyle(e.IsSynthetic ? CL.SgrColor.BrightBlack : CL.SgrColor.BrightWhite, bg);

        // Mass style: dim for synthetic to flag the parenthesised form.
        var massStyle = isSelected
            ? new CL.VtStyle(CL.SgrColor.Black, bg)
            : new CL.VtStyle(e.IsSynthetic ? CL.SgrColor.BrightBlack : fg, bg);

        var lines = new SoftLine[]
        {
            // Row 0: atomic number, top-left.
            new([new SoftSpan(e.AtomicNumber.ToString(), numStyle)], HAlign.Left),
            // Row 1: symbol, centered, in category color.
            new([new SoftSpan(e.Symbol, style)], HAlign.Center),
            // Row 2: atomic mass, centered.
            new([new SoftSpan(FormatMass(e), massStyle)], HAlign.Center),
        };

        var soft = new SoftText(CellWidth, CellHeight, lines);
        SoftRenderer.Render(Viewport, col, row, soft, mode, background: new CL.VtStyle(fgEff, bg));
    }

    private void RenderFBlockPlaceholder(int col, int row, string from, string to, CL.ColorMode mode)
    {
        var style = new CL.VtStyle(CL.SgrColor.BrightBlack, CL.SgrColor.Black);
        var lines = new SoftLine[]
        {
            new([new SoftSpan("*", style)], HAlign.Center),
            new([new SoftSpan(from, style)], HAlign.Center),
            new([new SoftSpan(to, style)], HAlign.Center),
        };
        var soft = new SoftText(CellWidth, CellHeight, lines);
        SoftRenderer.Render(Viewport, col, row, soft, mode);
    }

    private static string FormatMass(Element e)
    {
        var w = e.AtomicWeight;
        // 4-char field. Synthetic mass numbers are integers; we mark them by
        // colour (dim) rather than parens so they always fit.
        if (e.IsSynthetic) return ((int)Math.Round(w)).ToString().PadLeft(4);
        if (w < 10)   return w.ToString("F2");                  // "1.01"
        if (w < 100)  return w.ToString("F1").PadRight(4);      // "12.0"
        return ((int)Math.Round(w)).ToString().PadLeft(4);      // " 238"
    }

    private static CL.SgrColor CategoryFg(Category c) => c switch
    {
        Category.AlkaliMetal         => CL.SgrColor.BrightRed,
        Category.AlkalineEarthMetal  => CL.SgrColor.BrightYellow,
        Category.TransitionMetal     => CL.SgrColor.White,
        Category.PostTransitionMetal => CL.SgrColor.BrightCyan,
        Category.Metalloid           => CL.SgrColor.Green,
        Category.ReactiveNonmetal    => CL.SgrColor.BrightGreen,
        Category.NobleGas            => CL.SgrColor.BrightMagenta,
        Category.Lanthanide          => CL.SgrColor.Magenta,
        Category.Actinide            => CL.SgrColor.BrightBlue,
        Category.Unknown             => CL.SgrColor.BrightBlack,
        _ => CL.SgrColor.White,
    };

    public bool HandleKey(ConsoleKey key, ConsoleModifiers _)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow:    return Move(0, -1);
            case ConsoleKey.DownArrow:  return Move(0, +1);
            case ConsoleKey.LeftArrow:  return Move(-1, 0);
            case ConsoleKey.RightArrow: return Move(+1, 0);
            case ConsoleKey.Home:       return Select(Elements.ByAtomicNumber[1]);
            case ConsoleKey.End:        return Select(Elements.ByAtomicNumber[118]);
            default: return false;
        }
    }

    public bool HandleMouse(CL.MouseEvent ev)
    {
        if (ev.IsRelease || ev.Button != 0) return false;
        var hit = HitTest(ev.X, ev.Y);
        if (hit is null) return false;
        var (col, row) = hit.Value;
        return ElementAtTerminal(col, row) is { } el && Select(el);
    }

    private bool Move(int dCol, int dRow)
    {
        var (gc, gr) = Layout.CellOf(_selected);
        // Up to 20 steps to escape blank rows / jump across f-block gap.
        for (int step = 0; step < Layout.Columns + Layout.TotalRows; step++)
        {
            gc += dCol; gr += dRow;
            // Wrap moves on f-block gap: down from main row 7 → actinide row.
            if (gr is < 1 or > Layout.TotalRows) return false;
            if (gc is < 1 or > Layout.Columns) return false;
            if (Layout.IsFBlockPlaceholder(gc, gr)) continue;
            if (gr <= Layout.MainRows && Layout.IsBlankMainCell(gc, gr)) continue;
            if (gr >= Layout.FBlockRow1 && (gc < 3 || gc > 17)) continue;
            if (FindAt(gc, gr) is { } el) return Select(el);
        }
        return false;
    }

    private static Element? FindAt(int gridCol, int gridRow)
    {
        foreach (var e in Elements.All)
        {
            var (c, r) = Layout.CellOf(e);
            if (c == gridCol && r == gridRow) return e;
        }
        return null;
    }

    private Element? ElementAtTerminal(int col, int row)
    {
        col -= _origin.Col;
        row -= _origin.Row;
        if (col < 0 || row < 0) return null;
        // Inverse of ToTerminal.
        int gridCol = col / CellWidth + 1;
        int rowOffset = row;
        int gridRow;
        if (rowOffset >= Layout.MainRows * CellHeight + FBlockGap)
        {
            int fRow = (rowOffset - Layout.MainRows * CellHeight - FBlockGap) / CellHeight;
            gridRow = Layout.MainRows + 1 + fRow;
        }
        else
        {
            gridRow = rowOffset / CellHeight + 1;
            if (gridRow > Layout.MainRows) return null; // in the gap
        }
        if (gridCol is < 1 or > Layout.Columns) return null;
        if (gridRow is < 1 or > Layout.TotalRows) return null;
        return FindAt(gridCol, gridRow);
    }

    private bool Select(Element e)
    {
        if (ReferenceEquals(e, _selected)) return false;
        _selected = e;
        SelectionChanged?.Invoke(e);
        return true;
    }
}
