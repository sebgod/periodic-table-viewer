using Console.Lib;
using PeriodicTable.Tui;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

/// <summary>
/// Verifies the resize path: that re-placing the SAME hosted viewports against a frame resolved for a new
/// terminal size moves every region, and that a slot the new shape drops collapses instead of going stale.
///
/// <para><b>Why this is a unit test and not an inspector script.</b> A console's size cannot be changed
/// from outside the process under ConPTY — the terminal emulator owns the window and
/// <c>SetConsoleScreenBufferSize</c> / <c>mode con</c> are both no-ops against it — and the debug inspector
/// has no resize verb. So the one thing the live TUI cannot demonstrate is the thing the rewrite is FOR.
/// <see cref="FrameHost"/> exists as its own type so it can be driven here instead: it takes a viewport
/// and a frame and touches nothing else.</para>
/// </summary>
public class FrameHostTests
{
    private static readonly string[] AllSlots =
    [
        ViewerFrameLayout.SlotHeader,
        ViewerFrameLayout.SlotTable,
        ViewerFrameLayout.SlotOrbital,
        ViewerFrameLayout.SlotDetail,
        ViewerFrameLayout.SlotChain,
        ViewerFrameLayout.SlotStatus,
    ];

    private static ViewerFrameLayout Frame(int columns, int rows)
        => new(columns, rows, ViewerFrameMetrics.Widgets, mathCapable: true);

    private static (FrameHost Host, Dictionary<string, ITerminalViewport> Slots) Build()
    {
        var host = new FrameHost(new StubViewport());
        var slots = AllSlots.ToDictionary(key => key, host.Host);
        return (host, slots);
    }

    [Fact]
    public void APlacedFrame_PositionsEveryRegionAtItsSlot()
    {
        var (host, slots) = Build();

        host.Place(Frame(202, 63)).ShouldBeTrue("the first placement always counts as a move");

        // FullMath at 63 rows: 1 header + 31 table + 16 detail + 14 chain + 1 status.
        slots[ViewerFrameLayout.SlotHeader].Offset.ShouldBe((0, 0));
        slots[ViewerFrameLayout.SlotTable].Offset.ShouldBe((0, 1));
        slots[ViewerFrameLayout.SlotDetail].Offset.ShouldBe((0, 32));
        slots[ViewerFrameLayout.SlotChain].Offset.ShouldBe((0, 48));
        slots[ViewerFrameLayout.SlotStatus].Offset.ShouldBe((0, 62));

        // The gutter is to the right of the table, spanning only its band.
        slots[ViewerFrameLayout.SlotOrbital].Offset.ShouldBe((170, 1));
        slots[ViewerFrameLayout.SlotOrbital].Size.ShouldBe((32, 31));
        slots[ViewerFrameLayout.SlotTable].Size.ShouldBe((170, 31));
    }

    /// <summary>
    /// Re-placing at the same size must report no movement. This is what stands in for
    /// <c>Panel.Recompute()</c>'s size guard — if it reported <c>true</c> every time, the caller would
    /// <c>Clear()</c> and repaint the whole screen on every pump.
    /// </summary>
    [Fact]
    public void ReplacingAtTheSameSize_ReportsNoMovement()
    {
        var (host, _) = Build();

        host.Place(Frame(202, 63)).ShouldBeTrue();
        host.Place(Frame(202, 63)).ShouldBeFalse();
        host.Place(Frame(202, 63)).ShouldBeFalse();
    }

    /// <summary>
    /// The bug the rewrite exists to fix, in the form a test can state it: the orbital panel used to be
    /// constructed only when the STARTUP width allowed it, and its own comment conceded "a too-narrow
    /// terminal just gets no orbital panel until the user restarts". Now the widget always exists and the
    /// gutter's presence is a property of the current size.
    /// </summary>
    [Fact]
    public void GrowingPastTheGuttersThreshold_GivesTheOrbitalPanelAViewport_WithNoRestart()
    {
        var (host, slots) = Build();
        var orbital = slots[ViewerFrameLayout.SlotOrbital];

        host.Place(Frame(100, 63));
        orbital.Size.ShouldBe((0, 0), "100 columns leaves under MinViewportCols past the table");

        host.Place(Frame(140, 63)).ShouldBeTrue();
        orbital.Size.ShouldBe((32, 31));
    }

    /// <summary>
    /// And the reverse. A slot the new shape drops must collapse to nothing — if the placement were driven
    /// from the arranged tree instead of from the hosts, that host would simply be skipped and its
    /// viewport would stay parked where it was, leaving a live orbital gutter painting over the table.
    /// </summary>
    [Fact]
    public void ShrinkingBelowTheGuttersThreshold_CollapsesItsViewportRatherThanLeavingItStale()
    {
        var (host, slots) = Build();
        var orbital = slots[ViewerFrameLayout.SlotOrbital];

        host.Place(Frame(140, 63));
        orbital.Size.ShouldBe((32, 31));

        host.Place(Frame(100, 63)).ShouldBeTrue();
        orbital.Size.ShouldBe((0, 0));
        orbital.Offset.ShouldBe((0, 0));

        // ...and the table takes the columns back.
        slots[ViewerFrameLayout.SlotTable].Size.ShouldBe((100, 31));
    }

    /// <summary>
    /// A shape change re-budgets the panels' rows under the same widgets. 63 rows affords FullMath; 55
    /// affords only DetailMath, which hands the chain strip its compact height.
    /// </summary>
    [Fact]
    public void AShapeChange_RebudgetsThePanelRowsOfTheSameWidgets()
    {
        var (host, slots) = Build();
        var detail = slots[ViewerFrameLayout.SlotDetail];
        var chain = slots[ViewerFrameLayout.SlotChain];

        host.Place(Frame(140, 63));
        detail.Size.Height.ShouldBe(DetailPanel.RowsExpanded);
        chain.Size.Height.ShouldBe(Tui.Soft.SixelDecayChainPanel.RowsExpanded);

        host.Place(Frame(140, 55)).ShouldBeTrue();
        detail.Size.Height.ShouldBe(DetailPanel.RowsExpanded);
        chain.Size.Height.ShouldBe(Tui.Soft.SixelDecayChainPanel.RowsCompact);

        host.Place(Frame(140, 45)).ShouldBeTrue();
        detail.Size.Height.ShouldBe(DetailPanel.RowsCompact);
        chain.Size.Height.ShouldBe(Tui.Soft.SixelDecayChainPanel.RowsCompact);
    }

    /// <summary>
    /// Every placement, at every size, leaves every viewport inside the terminal. Combined with
    /// <c>ViewerFrameLayoutTests</c>'s arrange-level version of the same check, this covers the composition
    /// as well as the tree — <see cref="TerminalViewport"/> stores the geometry it is handed verbatim.
    /// </summary>
    [Fact]
    public void NoPlacementEverPutsAViewportOutsideTheTerminal()
    {
        var (host, slots) = Build();

        for (var rows = 0; rows <= 70; rows += 1)
        {
            foreach (var columns in (int[])[0, 40, 90, 118, 121, 140, 202])
            {
                host.Place(Frame(columns, rows));
                foreach (var (key, vp) in slots)
                {
                    var (col, row) = vp.Offset;
                    var (w, h) = vp.Size;
                    (col + w).ShouldBeLessThanOrEqualTo(columns, $"{key} at {columns}x{rows}");
                    (row + h).ShouldBeLessThanOrEqualTo(rows, $"{key} at {columns}x{rows}");
                }
            }
        }
    }

    /// <summary>
    /// Root viewport for the hosts. <see cref="FrameHost"/> only ever reads geometry off the children it
    /// creates and calls <c>UpdateGeometry</c> on them, so nothing here needs to do anything — but the
    /// interface has to be satisfied, and a stub is what makes the resize path drivable at all.
    /// </summary>
    private sealed class StubViewport : ITerminalViewport
    {
        public (int Column, int Row) Offset => (0, 0);

        public (int Width, int Height) Size => (int.MaxValue, int.MaxValue);

        public TermCell CellSize => new(10, 20);

        public Stream OutputStream => Stream.Null;

        public void SetCursorPosition(int left, int top) { }

        public void Write(string text) { }

        public void WriteLine(string? text = null) { }

        public void Flush() { }
    }
}
