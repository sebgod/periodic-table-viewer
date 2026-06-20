using System.Collections.Immutable;
using Console.Lib;
using DIR.Lib;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Wide-form periodic table rendered as an 18x9 grid of 5x3 cells (90x28
/// terminal cells including the gap between the main grid and the f-block).
/// Owns the selection cursor; arrow keys move to the next non-blank cell in
/// each direction, jumping into / out of the f-block as appropriate.
///
/// The table is built as a declarative <see cref="Layout.Node"/> tree (a vertical
/// stack of two 18-column <see cref="Layout.Node.Grid"/>s with a gap row between)
/// and painted via the surface-agnostic <see cref="CL.CellLayout"/> cell painter.
/// Click hit-testing reuses the SAME arranged tree (<see cref="CL.CellLayout.HitTest"/>),
/// so the drawn cell rect IS the hit region -- no separate forward/inverse cell
/// arithmetic that can drift. Keyboard navigation still works on the logical
/// (col,row) grid via <see cref="ElementGrid"/>.
/// </summary>
public sealed class PeriodicTableWidget : CL.Widget
{
    // 5 cols x 3 rows per element cell. Mass strings render to 4 chars; the
    // 5th column gives one cell of horizontal padding so adjacent cells'
    // numbers don't run together (e.g. "39.140.145.0..." -> "39.1 40.1 45.0").
    public const int CellWidth = 5;
    public const int CellHeight = 3;
    public const int FBlockGap = 1;

    public const int RenderedWidth = ElementGrid.Columns * CellWidth;                         // 90
    public const int RenderedHeight = ElementGrid.TotalRows * CellHeight + FBlockGap;          // 28

    private static readonly CL.CellMeasureContext MeasureCtx = new();

    private Element _selected;

    /// <summary>Last arranged tree from <see cref="Render"/>, reused for click hit-testing.</summary>
    private ImmutableArray<Layout.ArrangedNode<int>> _arranged = ImmutableArray<Layout.ArrangedNode<int>>.Empty;

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
        // Center within the viewport when there's slack on either axis.
        int extraW = Math.Max(0, Viewport.Size.Width - RenderedWidth);
        int extraH = Math.Max(0, Viewport.Size.Height - RenderedHeight);
        var bounds = new Rect<int>(extraW / 2, extraH / 2, RenderedWidth, RenderedHeight);

        _arranged = Layout.Engine.Arrange(BuildTree(_selected), bounds, MeasureCtx);

        // No pre-clear pass: every element/placeholder cell paints its full
        // background + glyphs, overwriting the previous frame in place (a separate
        // clear pass produced a visible blank flash on selection changes). Blank
        // cells are transparent Box leaves, so the painter draws nothing for them
        // -- they stay clean from the initial VirtualTerminal.Clear() /
        // EnterAlternateScreen and are never re-rendered.
        CL.CellLayout.Paint(Viewport, _arranged);
    }

    // -----------------------------------------------------------------------
    // Tree construction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the periodic-table layout tree for a given selection. Static + viewport-free so it is unit
    /// testable: a test can <see cref="Layout.Engine.Arrange{T}"/> the result and assert element cell rects +
    /// <see cref="CL.CellLayout.HitTest"/> mappings without standing up a terminal.
    /// </summary>
    internal static Layout.Node BuildTree(Element selected)
    {
        var main = new Layout.Node[ElementGrid.Columns * ElementGrid.MainRows];
        var fblock = new Layout.Node[ElementGrid.Columns * 2];
        for (var i = 0; i < main.Length; i++) main[i] = BlankCell();
        for (var i = 0; i < fblock.Length; i++) fblock[i] = BlankCell();

        // F-block placeholders ("*" + "57"/"71", "89"/"103") at column 3, periods 6/7.
        main[CellIndex(3, 6)] = PlaceholderCell("57", "71");
        main[CellIndex(3, 7)] = PlaceholderCell("89", "103");

        // Place each element at its (col,row); the selection cursor is the
        // reverse-video styling baked into the cell's colours.
        foreach (var e in Elements.All)
        {
            var (col, row) = ElementGrid.CellOf(e);
            var cell = ElementCell(e, isSelected: ReferenceEquals(e, selected));
            if (row <= ElementGrid.MainRows)
            {
                main[(row - 1) * ElementGrid.Columns + (col - 1)] = cell;
            }
            else
            {
                fblock[(row - ElementGrid.FBlockRow1) * ElementGrid.Columns + (col - 1)] = cell;
            }
        }

        return new Layout.Node.Stack(
        [
            new Layout.Node.Grid(ElementGrid.Columns, [.. main])
            {
                Width = Layout.Sizing.Star(),
                Height = Layout.Sizing.Fixed(ElementGrid.MainRows * CellHeight),
            },
            // One-cell gap separating the main grid from the f-block rows.
            new Layout.Node.Leaf(new Layout.Content.Box(RenderedWidth, FBlockGap))
            {
                Width = Layout.Sizing.Star(),
                Height = Layout.Sizing.Fixed(FBlockGap),
            },
            new Layout.Node.Grid(ElementGrid.Columns, [.. fblock])
            {
                Width = Layout.Sizing.Star(),
                Height = Layout.Sizing.Fixed(2 * CellHeight),
            },
        ], Layout.Axis.Vertical)
        {
            Width = Layout.Sizing.Fixed(RenderedWidth),
            Height = Layout.Sizing.Fixed(RenderedHeight),
        };
    }

    private static int CellIndex(int col, int row) => (row - 1) * ElementGrid.Columns + (col - 1);

    private static Layout.Node ElementCell(Element e, bool isSelected)
    {
        var black = CL.SgrColor.Black.ToRgba();
        var bg = isSelected ? CL.SgrColor.White.ToRgba() : black;
        var categoryFg = CategoryFg(e.Category).ToRgba();

        // Atomic-number: dimmer for synthetic, brighter otherwise; black when selected.
        var numFg = isSelected ? black : (e.IsSynthetic ? CL.SgrColor.BrightBlack : CL.SgrColor.BrightWhite).ToRgba();
        // Symbol: category colour. Mass: dim for synthetic (flags the integer form).
        var symFg = isSelected ? black : categoryFg;
        var massFg = isSelected ? black : (e.IsSynthetic ? CL.SgrColor.BrightBlack.ToRgba() : categoryFg);

        return new Layout.Node.Stack(
        [
            CellLine(e.AtomicNumber.ToString(), numFg, TextAlign.Near),   // row 0: number, top-left
            CellLine(e.Symbol, symFg, TextAlign.Center),                  // row 1: symbol, centered
            CellLine(FormatMass(e), massFg, TextAlign.Center),            // row 2: mass, centered
        ], Layout.Axis.Vertical)
        {
            Width = Layout.Sizing.Fixed(CellWidth),
            Height = Layout.Sizing.Fixed(CellHeight),
            Background = bg,
            Hit = new HitResult.ListItemHit("Element", e.AtomicNumber),
        };
    }

    private static Layout.Node PlaceholderCell(string from, string to)
    {
        var fg = CL.SgrColor.BrightBlack.ToRgba();
        return new Layout.Node.Stack(
        [
            CellLine("*", fg, TextAlign.Center),
            CellLine(from, fg, TextAlign.Center),
            CellLine(to, fg, TextAlign.Center),
        ], Layout.Axis.Vertical)
        {
            Width = Layout.Sizing.Fixed(CellWidth),
            Height = Layout.Sizing.Fixed(CellHeight),
            Background = CL.SgrColor.Black.ToRgba(),
        };
    }

    // Star width so each line fills the cell and HAlign can center within it;
    // Auto height resolves to one terminal row (the cell-measure oracle).
    private static Layout.Node CellLine(string text, RGBAColor32 fg, TextAlign hAlign) =>
        new Layout.Node.Leaf(new Layout.Content.Text(text) { Color = fg, HAlign = hAlign, VAlign = TextAlign.Center })
        {
            Width = Layout.Sizing.Star(),
            Height = Layout.Sizing.Auto,
        };

    // Transparent spacer: the painter skips it (zero alpha), so blank grid cells
    // are never drawn over -- preserving the no-flash, paint-in-place behaviour.
    private static Layout.Node BlankCell() => new Layout.Node.Leaf(new Layout.Content.Box(CellWidth, CellHeight));

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

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

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

    /// <summary>Programmatically select an element by atomic number; e.g. from a decay-chain click.</summary>
    public bool SelectByZ(int z)
        => Elements.ByAtomicNumber.TryGetValue(z, out var e) && Select(e);

    public bool HandleMouse(CL.MouseEvent ev)
    {
        if (ev.IsRelease || ev.Button != 0) return false;
        // Translate the click to viewport-local cells, then map it back to an
        // element through the arranged tree's auto-bound hit regions.
        if (HitTest(ev.X, ev.Y) is not { } local) return false;
        return CL.CellLayout.HitTest(_arranged, local.Col, local.Row) is HitResult.ListItemHit { ListId: "Element", Index: var z }
            && SelectByZ(z);
    }

    private bool Move(int dCol, int dRow)
    {
        var (gc, gr) = ElementGrid.CellOf(_selected);
        // Up to 20 steps to escape blank rows / jump across f-block gap.
        for (int step = 0; step < ElementGrid.Columns + ElementGrid.TotalRows; step++)
        {
            gc += dCol; gr += dRow;
            // Wrap moves on f-block gap: down from main row 7 -> actinide row.
            if (gr is < 1 or > ElementGrid.TotalRows) return false;
            if (gc is < 1 or > ElementGrid.Columns) return false;
            if (ElementGrid.IsFBlockPlaceholder(gc, gr)) continue;
            if (gr <= ElementGrid.MainRows && ElementGrid.IsBlankMainCell(gc, gr)) continue;
            if (gr >= ElementGrid.FBlockRow1 && (gc < 3 || gc > 17)) continue;
            if (FindAt(gc, gr) is { } el) return Select(el);
        }
        return false;
    }

    private static Element? FindAt(int gridCol, int gridRow)
    {
        foreach (var e in Elements.All)
        {
            var (c, r) = ElementGrid.CellOf(e);
            if (c == gridCol && r == gridRow) return e;
        }
        return null;
    }

    private bool Select(Element e)
    {
        if (ReferenceEquals(e, _selected)) return false;
        _selected = e;
        SelectionChanged?.Invoke(e);
        return true;
    }
}
