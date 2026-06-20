using System.Collections.Immutable;
using System.Linq;
using Console.Lib;
using DIR.Lib;
using PeriodicTable.Tui;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

/// <summary>
/// Verifies that <see cref="PeriodicTableWidget"/>'s declarative <see cref="Layout.Node"/> tree, once
/// arranged by the surface-agnostic engine, places every element at the expected terminal cell and that
/// the cell painter's <see cref="CellLayout.HitTest"/> maps a click back to the right element -- i.e. the
/// drawn rect IS the hit region (no separate forward/inverse cell arithmetic). This is the
/// production-consumer proof for the Console.Lib cell painter.
/// </summary>
public class PeriodicTableLayoutTests
{
    private static ImmutableArray<Layout.ArrangedNode<int>> ArrangeTable(int selectedZ)
    {
        var tree = PeriodicTableWidget.BuildTree(Elements.ByAtomicNumber[selectedZ]);
        var bounds = new Rect<int>(0, 0, PeriodicTableWidget.RenderedWidth, PeriodicTableWidget.RenderedHeight);
        return Layout.Engine.Arrange(tree, bounds, new CellMeasureContext());
    }

    private static Layout.ArrangedNode<int> CellOf(ImmutableArray<Layout.ArrangedNode<int>> arranged, int z) =>
        arranged.First(a => a.Node.Hit is HitResult.ListItemHit { ListId: "Element", Index: var i } && i == z);

    [Theory]
    [InlineData(1, 0, 0)]      // H:  col 1,  row 1
    [InlineData(2, 85, 0)]     // He: col 18, row 1 -> x = 17*5
    [InlineData(3, 0, 3)]      // Li: col 1,  row 2 -> y = 1*3
    [InlineData(118, 85, 18)]  // Og: col 18, row 7 -> x = 85, y = 6*3
    public void MainGridElement_LandsAtExpectedCell(int z, int expectedX, int expectedY)
    {
        var rect = CellOf(ArrangeTable(1), z).Bounds;
        rect.X.ShouldBe(expectedX);
        rect.Y.ShouldBe(expectedY);
        rect.Width.ShouldBe(PeriodicTableWidget.CellWidth);
        rect.Height.ShouldBe(PeriodicTableWidget.CellHeight);
    }

    [Theory]
    [InlineData(57, 10, 22)]   // La: col 3,  f-block row 1 -> x = 2*5, y = 7*3 + gap(1)
    [InlineData(103, 80, 25)]  // Lr: col 17, f-block row 2 -> x = 16*5, y = 22 + 3
    public void FBlockElement_LandsBelowTheGap(int z, int expectedX, int expectedY)
    {
        var rect = CellOf(ArrangeTable(1), z).Bounds;
        rect.X.ShouldBe(expectedX);
        rect.Y.ShouldBe(expectedY);
    }

    [Fact]
    public void EveryElement_HasItsOwnClickableCell()
    {
        var arranged = ArrangeTable(1);
        var hits = arranged
            .Where(a => a.Node.Hit is HitResult.ListItemHit { ListId: "Element" })
            .Select(a => ((HitResult.ListItemHit)a.Node.Hit!).Index)
            .ToHashSet();
        foreach (var e in Elements.All)
        {
            hits.ShouldContain(e.AtomicNumber, $"missing clickable cell for {e.Symbol}");
        }
    }

    [Theory]
    [InlineData(2, 1, 1)]      // inside the H cell  (x 0..4,  y 0..2)
    [InlineData(87, 19, 118)]  // inside the Og cell (x 85..89, y 18..20)
    public void HitTest_MapsCellBackToElement(int col, int row, int expectedZ)
    {
        var hit = CellLayout.HitTest(ArrangeTable(1), col, row);
        hit.ShouldBeOfType<HitResult.ListItemHit>().Index.ShouldBe(expectedZ);
    }

    [Fact]
    public void SelectedElement_GetsADistinctBackground()
    {
        // Selecting an element flips its cell background (reverse-video). The same cell's
        // Background must differ between "He selected" (H normal) and "H selected".
        var hNormal = CellOf(ArrangeTable(2), 1).Node.Background;
        var hSelected = CellOf(ArrangeTable(1), 1).Node.Background;
        hSelected.ShouldNotBe(hNormal);
    }
}
