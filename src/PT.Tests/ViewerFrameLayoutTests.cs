using System.Collections.Immutable;
using Console.Lib;
using DIR.Lib;
using PeriodicTable.Tui;
using PeriodicTable.Tui.Soft;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

/// <summary>
/// Verifies <see cref="ViewerFrameLayout"/> — the costed shape and the declarative frame tree that
/// replaced <c>Program</c>'s hand-computed dock budget.
///
/// <para>The point of the rewrite is that this file can exist at all: the old budget lived inside
/// <c>RunUi</c> between a live <c>VirtualTerminal</c> and a chain of <c>Panel.Dock</c> calls, so its
/// thresholds could only be checked by launching the TUI at a given window size and looking. The shape is
/// now a pure function of the terminal size and the tree arranges through the surface-agnostic engine, so
/// every decision below is asserted with no console in the process.</para>
/// </summary>
public class ViewerFrameLayoutTests
{
    private const int WideEnoughForOrbital = 140;

    private static ViewerFrameLayout Frame(int columns, int rows, bool mathCapable = true)
        => new(columns, rows, ViewerFrameMetrics.Widgets, mathCapable);

    private static ImmutableArray<Layout.ArrangedNode<int>> Arrange(ViewerFrameLayout frame)
        => Layout.Engine.Arrange(
            frame.Build(),
            new Rect<int>(0, 0, frame.Columns, frame.Rows),
            CellMeasureContext.CellAuthored);

    private static Rect<int> Slot(ViewerFrameLayout frame, string key)
        => ViewerFrameLayout.Slot(Arrange(frame), key);

    /// <summary>Slot keys the given frame's shape actually declares.</summary>
    private static IEnumerable<string> DeclaredSlots(ViewerFrameLayout frame)
    {
        yield return ViewerFrameLayout.SlotHeader;
        yield return ViewerFrameLayout.SlotTable;
        yield return ViewerFrameLayout.SlotDetail;
        yield return ViewerFrameLayout.SlotChain;
        yield return ViewerFrameLayout.SlotStatus;
        if (frame.HasOrbitalPanel)
        {
            yield return ViewerFrameLayout.SlotOrbital;
        }
    }

    /// <summary>
    /// The thresholds the hand-rolled budget used, now derived rather than written down: 2 chrome rows +
    /// 16 detail + 5 chain + 28 table == 51 for the detail block, and + 9 more for the chain legend == 60.
    /// The arithmetic was never the problem — it could just neither be re-run on resize nor tested.
    /// </summary>
    [Theory]
    [InlineData(39, ViewerFrameShape.Compact)]      // table already short of its 28 rows; nothing to spend
    [InlineData(40, ViewerFrameShape.Compact)]      // compact saturates here
    [InlineData(50, ViewerFrameShape.Compact)]      // ...and the detail block would still cost the table
    [InlineData(51, ViewerFrameShape.DetailMath)]   // 1 + 1 + 16 + 5 + 28
    [InlineData(59, ViewerFrameShape.DetailMath)]
    [InlineData(60, ViewerFrameShape.FullMath)]     // 1 + 1 + 16 + 14 + 28
    [InlineData(200, ViewerFrameShape.FullMath)]
    public void Shape_ReproducesTheBudgetThresholds(int rows, ViewerFrameShape expected)
        => Frame(WideEnoughForOrbital, rows).Shape.ShouldBe(expected);

    /// <summary>
    /// A terminal too short to seat the table in ANY shape must still choose compact.
    ///
    /// <para>This is the bug in the obvious formulation of the rule. Cost is monotone in richness, so
    /// compact always holds the maximum and "take any shape that draws level with compact" looks
    /// equivalent to "take the richest that leaves the table whole" — until the table gets no rows in any
    /// shape and every candidate ties at zero. Read as a tie at saturation, that hands every remaining row
    /// to the richest chrome: at 12 rows FullMath took ten squeezed rows for the detail card and left the
    /// chain none, where compact seats both panels whole.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(30)]
    public void OnASurfaceTooShortForTheTable_TheShapeStaysCompact(int rows)
        => Frame(WideEnoughForOrbital, rows).Shape.ShouldBe(ViewerFrameShape.Compact);

    /// <summary>
    /// Both expanded shapes exist to rasterise a <c>$$…$$</c> block. With no font to rasterise from, the
    /// panels take their text path, so spending the rows would buy blank space at any height.
    /// </summary>
    [Theory]
    [InlineData(40)]
    [InlineData(51)]
    [InlineData(60)]
    [InlineData(200)]
    public void WithoutAMathFont_EveryHeightStaysCompact(int rows)
        => Frame(WideEnoughForOrbital, rows, mathCapable: false).Shape.ShouldBe(ViewerFrameShape.Compact);

    [Theory]
    [InlineData(60, DetailPanel.RowsExpanded, SixelDecayChainPanel.RowsExpanded)]
    [InlineData(51, DetailPanel.RowsExpanded, SixelDecayChainPanel.RowsCompact)]
    [InlineData(40, DetailPanel.RowsCompact, SixelDecayChainPanel.RowsCompact)]
    public void PanelRows_FollowTheChosenShape(int rows, int expectedDetail, int expectedChain)
    {
        var frame = Frame(WideEnoughForOrbital, rows);
        frame.DetailRows.ShouldBe(expectedDetail);
        frame.ChainRows.ShouldBe(expectedChain);

        // And the tree agrees with the properties — the two could drift, since Build reads them back.
        Slot(frame, ViewerFrameLayout.SlotDetail).Height.ShouldBe(expectedDetail);
        Slot(frame, ViewerFrameLayout.SlotChain).Height.ShouldBe(expectedChain);
    }

    /// <summary>
    /// Cost saturates at the table's natural height and never rises with richness. Both halves are load
    /// bearing for <c>ChooseShape</c>: saturation is what stops surplus rows outranking chrome that would
    /// use them, and monotonicity is what makes "the richest shape that draws level with compact" a
    /// well-defined rule rather than a search.
    /// </summary>
    [Fact]
    public void Cost_SaturatesAtTheTableHeight_AndNeverRisesWithRichness()
    {
        var tall = Frame(WideEnoughForOrbital, 200);
        tall.Cost(ViewerFrameShape.Compact).ShouldBe(PeriodicTableWidget.RenderedHeight);
        tall.Cost(ViewerFrameShape.FullMath).ShouldBe(PeriodicTableWidget.RenderedHeight);
        tall.TableRowsFor(ViewerFrameShape.Compact).ShouldBeGreaterThan(PeriodicTableWidget.RenderedHeight);

        for (var rows = 0; rows <= 120; rows++)
        {
            var frame = Frame(WideEnoughForOrbital, rows);
            frame.Cost(ViewerFrameShape.DetailMath)
                .ShouldBeLessThanOrEqualTo(frame.Cost(ViewerFrameShape.Compact), $"{rows} rows");
            frame.Cost(ViewerFrameShape.FullMath)
                .ShouldBeLessThanOrEqualTo(frame.Cost(ViewerFrameShape.DetailMath), $"{rows} rows");
        }
    }

    /// <summary>
    /// Every slot the shape declares arranges to a real rect. This is the guard against the trap the
    /// shared-layout work documents: a <c>Fill</c> leaf has no intrinsic size, so a leaf that states one
    /// axis and leaves the other <c>Auto</c> measures zero on it and the region silently disappears.
    /// </summary>
    [Theory]
    [InlineData(140, 60)]   // FullMath, with an orbital gutter
    [InlineData(140, 51)]   // DetailMath
    [InlineData(140, 40)]   // Compact
    [InlineData(122, 60)]   // narrowest terminal that still affords the gutter its preferred width
    [InlineData(100, 45)]   // no gutter
    [InlineData(90, 30)]    // table exactly its own width, and short
    public void EverySlotTheShapeDeclares_ArrangesNonDegenerate(int columns, int rows)
    {
        var frame = Frame(columns, rows);
        var arranged = Arrange(frame);

        foreach (var key in DeclaredSlots(frame))
        {
            var slot = ViewerFrameLayout.Slot(arranged, key);
            slot.Width.ShouldBeGreaterThan(0, key);
            slot.Height.ShouldBeGreaterThan(0, key);
        }
    }

    /// <summary>A shape without an orbital gutter has no such leaf, so the lookup reports an empty rect —
    /// which is how <c>Program</c> expresses "this panel is not on screen" now that the widget is always
    /// constructed.</summary>
    [Fact]
    public void WithNoRoomForTheGutter_TheOrbitalSlotIsAbsentFromTheTree()
    {
        var frame = Frame(100, 60);
        frame.HasOrbitalPanel.ShouldBeFalse();
        Slot(frame, ViewerFrameLayout.SlotOrbital).ShouldBe(default);
    }

    [Fact]
    public void Regions_StackHeaderTableDetailChainStatus()
    {
        const int rows = 60;
        var frame = Frame(WideEnoughForOrbital, rows);
        var arranged = Arrange(frame);

        var header = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotHeader);
        var table = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotTable);
        var detail = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotDetail);
        var chain = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotChain);
        var status = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotStatus);

        header.Y.ShouldBe(0);
        table.Y.ShouldBe(header.Y + header.Height);
        detail.Y.ShouldBe(table.Y + table.Height);
        chain.Y.ShouldBe(detail.Y + detail.Height);
        status.Y.ShouldBe(chain.Y + chain.Height);
        (status.Y + status.Height).ShouldBe(rows);
    }

    /// <summary>
    /// The orbital gutter spans only the table's band, which is why it is a child of that band's HStack
    /// rather than a sibling of the whole stack. If it were the latter it would run down the side of the
    /// detail card and the chain strip too.
    /// </summary>
    [Fact]
    public void OrbitalGutter_SpansTheTableBandOnly()
    {
        var frame = Frame(WideEnoughForOrbital, 60);
        var arranged = Arrange(frame);

        var table = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotTable);
        var orbital = ViewerFrameLayout.Slot(arranged, ViewerFrameLayout.SlotOrbital);

        orbital.Y.ShouldBe(table.Y);
        orbital.Height.ShouldBe(table.Height);
        orbital.X.ShouldBe(table.X + table.Width);
        (orbital.X + orbital.Width).ShouldBe(WideEnoughForOrbital);
    }

    /// <summary>
    /// The gutter appears once the table keeps all 90 columns AND the panel clears its own minimum, and
    /// between that minimum and its preferred width it is squeezed rather than dropped.
    /// </summary>
    [Theory]
    [InlineData(117, 0)]    // one column short of the panel's minimum -> no gutter at all
    [InlineData(118, 28)]   // squeezed to exactly its minimum
    [InlineData(121, 31)]
    [InlineData(122, 32)]   // first width that affords the preferred size
    [InlineData(200, 32)]   // and it never grows past it; the surplus goes to the table
    public void OrbitalGutter_TakesOnlyWhatIsLeftOverTheTable(int columns, int expected)
        => Frame(columns, 60).OrbitalColumns.ShouldBe(expected);

    /// <summary>
    /// The bug the old admission test had: it allowed the panel at <c>90 + 2 + MinViewportCols == 120</c>
    /// columns and then docked <c>DockedWidth == 32</c>, so a 120- or 121-column terminal clipped the
    /// table's right-hand columns to make room. Costing the gutter in table columns cannot express that.
    /// </summary>
    [Fact]
    public void OrbitalGutter_NeverCostsTheTableAColumn()
    {
        for (var columns = 0; columns <= 240; columns++)
        {
            var frame = Frame(columns, 60);
            if (!frame.HasOrbitalPanel)
            {
                continue;
            }

            Slot(frame, ViewerFrameLayout.SlotTable).Width
                .ShouldBeGreaterThanOrEqualTo(PeriodicTableWidget.RenderedWidth, $"{columns} columns");
        }
    }

    /// <summary>
    /// Nothing is ever placed off the surface, at any size.
    ///
    /// <para>This is the property the constructor's squeeze exists to hold, and it is not free: DIR.Lib's
    /// stack resolves a <c>Fixed</c> child at its stated extent whatever the container's size, walking the
    /// cursor past the bounds rather than clamping. (<c>TerminalLayout</c> used to absorb that — "a strip
    /// never exceeds remaining cells" — and a tree has no such backstop.) Heights below the 12 rows of
    /// compact chrome are exactly where it bites.</para>
    /// </summary>
    [Fact]
    public void NoRegionIsEverPlacedOffScreen_EvenBelowTheChromesOwnHeight()
    {
        for (var rows = 0; rows <= 70; rows++)
        {
            var frame = Frame(WideEnoughForOrbital, rows);
            foreach (var (_, rect) in Arrange(frame))
            {
                (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(rows, $"{rows} rows");
                (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(WideEnoughForOrbital, $"{rows} rows");
            }
        }
    }

    /// <summary>
    /// Squeeze order. The bars name the app and the selection and cost a row each; the detail card is
    /// worth reading for every element; the chain strip is the one that most often has nothing to show —
    /// so the chain is what pays when the chrome does not fit.
    /// </summary>
    [Fact]
    public void WhenTheChromeDoesNotFit_TheChainGivesUpRowsBeforeTheDetailCard()
    {
        // Compact chrome is 1 + 1 + 5 + 5 == 12 rows. At 10 the card must still be whole.
        var frame = Frame(WideEnoughForOrbital, 10);
        frame.HeaderRows.ShouldBe(1);
        frame.StatusRows.ShouldBe(1);
        frame.DetailRows.ShouldBe(DetailPanel.RowsCompact);
        frame.ChainRows.ShouldBe(10 - 1 - 1 - DetailPanel.RowsCompact);

        // Past the card itself, the card starts paying and the chain is gone.
        var tiny = Frame(WideEnoughForOrbital, 4);
        tiny.DetailRows.ShouldBe(2);
        tiny.ChainRows.ShouldBe(0);
    }

    /// <summary>
    /// A degenerate surface resolves rather than throwing or producing negative extents — the arrange
    /// happens before the first paint, and a terminal can report 0x0 while it is being set up.
    /// </summary>
    [Fact]
    public void AZeroSizedSurface_ResolvesToAnEmptyFrame()
    {
        var frame = Frame(0, 0);
        frame.Shape.ShouldBe(ViewerFrameShape.Compact);
        frame.HasOrbitalPanel.ShouldBeFalse();
        frame.HeaderRows.ShouldBe(0);
        frame.StatusRows.ShouldBe(0);

        foreach (var (_, rect) in Arrange(frame))
        {
            rect.Width.ShouldBeGreaterThanOrEqualTo(0);
            rect.Height.ShouldBeGreaterThanOrEqualTo(0);
        }
    }
}
