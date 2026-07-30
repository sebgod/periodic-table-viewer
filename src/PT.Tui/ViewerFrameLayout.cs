using System.Collections.Immutable;
using System.Numerics;
using DIR.Lib;
using PeriodicTable.Tui.Soft;

namespace PeriodicTable.Tui;

/// <summary>
/// How much chrome the frame can afford around the periodic table. Not a preference — <see
/// cref="ViewerFrameLayout"/> costs every admissible shape in TABLE ROWS and takes the winner, so this
/// reports a decision rather than recording one.
///
/// <para>The order is poorest to richest, and the costing relies on that: each step up spends rows the
/// table would otherwise have, so cost is monotone non-increasing in richness.</para>
/// </summary>
public enum ViewerFrameShape
{
    /// <summary>
    /// Both bottom panels on their text path — a five-row detail card with a single-row Unicode electron
    /// configuration, and a five-row chain strip with a single-row Unicode legend. The only shape that
    /// fits a short terminal, and the only one available with no math font.
    /// </summary>
    Compact,

    /// <summary>
    /// The detail card expands to its <c>$$…$$</c> electron-configuration block (11 rows more than
    /// compact); the chain keeps its Unicode legend. Detail goes first because an electron configuration
    /// is worth reading for every element, whereas a decay chain is only interesting for the ~30 that
    /// have one.
    /// </summary>
    DetailMath,

    /// <summary>
    /// Both panels expanded — the chain's <c>$$\ce{…}$$</c> legend on top of the detail block. Nine more
    /// rows again, so this needs a tall terminal (60 rows with the default metrics).
    /// </summary>
    FullMath,
}

/// <summary>
/// The chrome's fixed sizes, in the frame tree's design units.
///
/// <para><b>A design unit is one terminal cell here</b>, deliberately. The shared trees in DIR.Lib carry
/// design-unit scalars precisely so one description can arrange to float pixels or int cells, but this
/// frame has exactly one front-end and every number in it is natively a ROW or COLUMN count — a one-row
/// status bar, a 32-column gutter, a table that is 90x28 cells and does not scale. Authoring those as
/// pixels and dividing back out would only introduce rounding. <see cref="ViewerFrameLayout.Slot{T}"/>
/// stays generic anyway, so nothing here forecloses a second surface.</para>
///
/// <para>They feed the costing as well as the tree, so a surface is always costed with the chrome it will
/// actually draw.</para>
/// </summary>
/// <param name="HeaderRows">Depth of the title bar.</param>
/// <param name="StatusRows">Depth of the status bar.</param>
/// <param name="DetailRowsCompact">Detail card on its text path.</param>
/// <param name="DetailRowsExpanded">Detail card with its display-math electron configuration.</param>
/// <param name="ChainRowsCompact">Decay-chain strip with its Unicode legend.</param>
/// <param name="ChainRowsExpanded">Decay-chain strip with its display-math legend.</param>
/// <param name="TableWidth">The table's natural width. It does NOT scale, which is what makes it a
/// meaningful unit of cost: a column past this buys nothing.</param>
/// <param name="TableHeight">The table's natural height. Same reasoning.</param>
/// <param name="OrbitalWidth">Width the orbital gutter wants.</param>
/// <param name="OrbitalMinWidth">Narrowest the orbital gutter is still worth drawing at. Between this and
/// <paramref name="OrbitalWidth"/> the panel is squeezed rather than dropped.</param>
public readonly record struct ViewerFrameMetrics(
    int HeaderRows,
    int StatusRows,
    int DetailRowsCompact,
    int DetailRowsExpanded,
    int ChainRowsCompact,
    int ChainRowsExpanded,
    int TableWidth,
    int TableHeight,
    int OrbitalWidth,
    int OrbitalMinWidth)
{
    /// <summary>
    /// The real widgets' sizes. Read off the widget types rather than restated, so a panel that grows a
    /// row cannot leave the costing believing the old number — the failure the hand-rolled budget in
    /// <c>Program</c> was one edit away from at all times.
    /// </summary>
    public static ViewerFrameMetrics Widgets { get; } = new(
        HeaderRows: 1,
        StatusRows: 1,
        DetailRowsCompact: DetailPanel.RowsCompact,
        DetailRowsExpanded: DetailPanel.RowsExpanded,
        ChainRowsCompact: SixelDecayChainPanel.RowsCompact,
        ChainRowsExpanded: SixelDecayChainPanel.RowsExpanded,
        TableWidth: PeriodicTableWidget.RenderedWidth,
        TableHeight: PeriodicTableWidget.RenderedHeight,
        OrbitalWidth: OrbitalPanel.DockedWidth,
        OrbitalMinWidth: OrbitalPanel.MinViewportCols);

    /// <summary>Rows the detail card takes in <paramref name="shape"/>.</summary>
    public int DetailRowsFor(ViewerFrameShape shape) =>
        shape == ViewerFrameShape.Compact ? DetailRowsCompact : DetailRowsExpanded;

    /// <summary>Rows the decay-chain strip takes in <paramref name="shape"/>.</summary>
    public int ChainRowsFor(ViewerFrameShape shape) =>
        shape == ViewerFrameShape.FullMath ? ChainRowsExpanded : ChainRowsCompact;
}

/// <summary>
/// The viewer's frame: which shape the chrome takes for a given terminal size, and where each region goes.
///
/// <para><b>Why this exists.</b> The budget it replaces was a chain of hand-written inequalities in
/// <c>Program.RunUi</c> — <c>chromeRows + detailRows + RowsExpanded + TableMinRows</c> and friends — that
/// computed a row split, handed the numbers to <c>Panel.Dock</c>, and then froze. Two consequences, both
/// real: the arithmetic restated sizes the widgets already declare (so a panel growing a row silently
/// broke the budget), and the decision could never be revisited, which its own comment admitted: "a
/// too-narrow terminal just gets no orbital panel until the user restarts". Here the decision is a cost
/// function over the terminal size, and the placement is one declarative tree, so a resize simply
/// re-derives both.</para>
///
/// <para><b>Costed in table rows, saturating.</b> Unlike a chess board, the periodic table does not
/// scale: it is 90x28 cells or it is clipped. So the cost of a shape is the rows it leaves the table,
/// clamped at the table's natural height — surplus above 28 is worthless and must not outrank chrome that
/// would use it, while a shortfall below 28 is real and ranks shapes honestly against each other. The
/// richest shape that reaches the saturation point wins: reaching it means the table is whole, so the
/// rows compact would have left blank are better spent on a legend.</para>
///
/// <para>This type resolves the frame and hands out rects; the caller paints them. It touches DIR.Lib's
/// layout vocabulary and nothing else — no terminal, no Console.Lib — so <see cref="Build"/> can be
/// arranged and asserted in a unit test with no console at all.</para>
/// </summary>
public sealed class ViewerFrameLayout
{
    // Slot keys: every Fill leaf routes its arranged rect to one widget's viewport, so no region is ever
    // positioned by hand (Layout.Content.Fill.Key).
    public const string SlotHeader = "header";
    public const string SlotTable = "table";
    public const string SlotOrbital = "orbital";
    public const string SlotDetail = "detail";
    public const string SlotChain = "chain";
    public const string SlotStatus = "status";

    private readonly ViewerFrameMetrics _metrics;

    /// <param name="columns">Terminal width in cells.</param>
    /// <param name="rows">Terminal height in cells.</param>
    /// <param name="metrics">The chrome's fixed sizes — see <see cref="ViewerFrameMetrics"/>.</param>
    /// <param name="mathCapable">Whether a math font resolved. Both expanded shapes rasterise a
    /// <c>$$…$$</c> block, so without one they would spend rows to draw nothing; the costing must know
    /// that rather than discover it at paint time.</param>
    public ViewerFrameLayout(int columns, int rows, ViewerFrameMetrics metrics, bool mathCapable)
    {
        _metrics = metrics;
        Columns = Math.Max(0, columns);
        Rows = Math.Max(0, rows);
        MathCapable = mathCapable;

        Shape = ChooseShape();

        // Fixed sizing does NOT clamp to the container: DIR.Lib's stack resolves a Fixed child at its
        // stated extent and walks the cursor past the bounds, so an unclamped frame on a very short
        // terminal would place its status bar off-screen. TerminalLayout used to absorb this ("a strip
        // never exceeds remaining cells"); a tree has to say it, so the squeeze happens here and the
        // tree only ever states extents that fit.
        //
        // Squeezed in priority order, richest-value-last: the bars are one row each and name the app and
        // the selection, the detail card is worth reading for every element, and the chain strip is the
        // one that most often has nothing to show. So the chain gives up rows first.
        var left = Rows;
        HeaderRows = Math.Min(_metrics.HeaderRows, left);
        left -= HeaderRows;
        StatusRows = Math.Min(_metrics.StatusRows, left);
        left -= StatusRows;
        DetailRows = Math.Min(_metrics.DetailRowsFor(Shape), left);
        left -= DetailRows;
        ChainRows = Math.Min(_metrics.ChainRowsFor(Shape), left);

        OrbitalColumns = ChooseOrbitalColumns();
    }

    /// <summary>Terminal width the frame was resolved for, in cells.</summary>
    public int Columns { get; }

    /// <summary>Terminal height the frame was resolved for, in cells.</summary>
    public int Rows { get; }

    /// <summary>Whether a math font resolved, gating both expanded shapes.</summary>
    public bool MathCapable { get; }

    /// <summary>The shape the costing chose. See <see cref="ChooseShape"/>.</summary>
    public ViewerFrameShape Shape { get; }

    /// <summary>Rows granted to the title bar.</summary>
    public int HeaderRows { get; }

    /// <summary>Rows granted to the status bar.</summary>
    public int StatusRows { get; }

    /// <summary>Rows granted to the detail card, after the squeeze.</summary>
    public int DetailRows { get; }

    /// <summary>Rows granted to the decay-chain strip, after the squeeze.</summary>
    public int ChainRows { get; }

    /// <summary>Columns granted to the orbital gutter; 0 when the frame has no room for one.</summary>
    public int OrbitalColumns { get; }

    /// <summary>True when the frame has an orbital gutter at all.</summary>
    public bool HasOrbitalPanel => OrbitalColumns > 0;

    /// <summary>
    /// Rows the table is left with in <paramref name="shape"/>, before the table's own centring. This is
    /// the raw quantity the cost saturates.
    /// </summary>
    public int TableRowsFor(ViewerFrameShape shape) => Math.Max(0,
        Rows - _metrics.HeaderRows - _metrics.StatusRows
             - _metrics.DetailRowsFor(shape) - _metrics.ChainRowsFor(shape));

    /// <summary>
    /// What <paramref name="shape"/> costs, in table rows delivered — <see cref="TableRowsFor"/> clamped
    /// at the table's natural height, because the table does not grow into surplus.
    /// </summary>
    public int Cost(ViewerFrameShape shape) => Math.Min(TableRowsFor(shape), _metrics.TableHeight);

    /// <summary>
    /// Costs every admissible shape and takes the richest that costs the table nothing — i.e. the richest
    /// whose cost reaches the saturation point, which is where the table has all the rows it can use.
    ///
    /// <para><b>Compared against the saturation value, not against compact's cost.</b> Cost is monotone
    /// non-increasing in richness (each step up spends rows), so compact always holds the maximum and it
    /// is tempting to take any shape that draws LEVEL with it. That is wrong at the bottom of the range: a
    /// terminal too short to seat the table in any shape ties every shape at zero, and reading that tie as
    /// "the table is whole either way" hands all the remaining rows to the richest chrome — on a 12-row
    /// terminal it gave the detail card ten squeezed rows and the chain none, where compact seats both.
    /// A tie only licenses the richer shape when it is a tie at saturation.</para>
    ///
    /// <para>With <see cref="ViewerFrameMetrics.Widgets"/> this reproduces the thresholds the hand-rolled
    /// budget used — 60 rows for <see cref="ViewerFrameShape.FullMath"/>, 51 for
    /// <see cref="ViewerFrameShape.DetailMath"/> — which is the point: the arithmetic was right, it just
    /// could not be re-run on a resize or tested without a terminal.</para>
    /// </summary>
    private ViewerFrameShape ChooseShape()
    {
        // No font means the panels take their text path, so the extra rows would rasterise nothing.
        if (!MathCapable)
        {
            return ViewerFrameShape.Compact;
        }

        // Richest first; the first one that leaves the table whole wins.
        if (Cost(ViewerFrameShape.FullMath) == _metrics.TableHeight)
        {
            return ViewerFrameShape.FullMath;
        }

        return Cost(ViewerFrameShape.DetailMath) == _metrics.TableHeight
            ? ViewerFrameShape.DetailMath
            : ViewerFrameShape.Compact;
    }

    /// <summary>
    /// Columns for the orbital gutter, costed in table COLUMNS — the same currency as the vertical
    /// decision.
    ///
    /// <para>The gutter appears once the table keeps all 90 of its columns AND the panel clears its own
    /// minimum; between that minimum and its preferred width it is squeezed rather than dropped. The old
    /// test mixed the two constants — it admitted the panel at <c>90 + 2 + MinViewportCols</c> and then
    /// docked <c>DockedWidth</c>, so a 120-column terminal clipped two columns off the table's right
    /// edge to make room. Costing in table columns cannot express that: the table is whole first, and the
    /// gutter takes what is left over.</para>
    /// </summary>
    private int ChooseOrbitalColumns()
    {
        var spare = Columns - _metrics.TableWidth;
        return spare >= _metrics.OrbitalMinWidth ? Math.Min(_metrics.OrbitalWidth, spare) : 0;
    }

    /// <summary>
    /// The frame as ONE declarative <see cref="Layout"/> tree: bars top and bottom, the two panels
    /// stacked above the status bar, and the table taking the Star that is left — beside the orbital
    /// gutter when there is one, which is why that gutter spans only the table's band and not the
    /// panels'.
    /// </summary>
    /// <remarks>
    /// <b>Every leaf states BOTH axes.</b> A <c>Fill</c> leaf has no intrinsic size, so a child that sets
    /// only its height keeps <c>Width</c> at <c>Auto</c>, measures a <c>MinWidth</c> of zero, and is
    /// arranged zero columns wide — the region silently vanishes. <c>RowH</c> / <c>ColW</c> /
    /// <c>Stretch</c> each set both axes at once, which is why they are used here in preference to
    /// spelling out <c>.WStar().HFixed()</c>.
    /// </remarks>
    public Layout.Node Build()
    {
        var table = Layout.Builder.Fill(key: SlotTable).Stretch();

        // The gutter is Fixed and the table Star, so the table absorbs any width past the gutter's
        // preference — and ChooseOrbitalColumns has already guaranteed that leaves the table its 90.
        var band = HasOrbitalPanel
            ? Layout.Builder.HStack(table, Layout.Builder.Fill(key: SlotOrbital).ColW(OrbitalColumns))
                .Stretch()
            : table;

        return Layout.Builder.VStack(
            Layout.Builder.Fill(key: SlotHeader).RowH(HeaderRows),
            band,
            Layout.Builder.Fill(key: SlotDetail).RowH(DetailRows),
            Layout.Builder.Fill(key: SlotChain).RowH(ChainRows),
            Layout.Builder.Fill(key: SlotStatus).RowH(StatusRows));
    }

    /// <summary>
    /// The arranged rect of the <see cref="Layout.Content.Fill"/> leaf carrying <paramref name="key"/>;
    /// empty when this frame has no such slot (no orbital gutter on a narrow terminal). Generic over the
    /// coordinate type so the lookup does not have to be rewritten if a second surface ever arranges the
    /// same tree.
    /// </summary>
    public static Rect<T> Slot<T>(ImmutableArray<Layout.ArrangedNode<T>> arranged, string key)
        where T : INumber<T>
    {
        foreach (var (node, rect) in arranged)
        {
            if (node is Layout.Node.Leaf { Content: Layout.Content.Fill fill } && fill.Key == key)
            {
                return rect;
            }
        }

        return default;
    }
}
