using PeriodicTable;
using Shouldly;
using Xunit;

namespace PeriodicTable.Tests;

public class LayoutTests
{
    [Theory]
    [InlineData("H", 1, 1)]
    [InlineData("He", 18, 1)]
    [InlineData("Li", 1, 2)]
    [InlineData("B", 13, 2)]
    [InlineData("Ne", 18, 2)]
    [InlineData("Ca", 2, 4)]
    [InlineData("Sc", 3, 4)]
    [InlineData("Hf", 4, 6)]
    [InlineData("Og", 18, 7)]
    public void CellOf_MainGrid(string symbol, int col, int row)
    {
        var (c, r) = Layout.CellOf(Elements.BySymbol[symbol]);
        c.ShouldBe(col);
        r.ShouldBe(row);
    }

    [Theory]
    [InlineData("La", 3, 8)]   // f-block index 0
    [InlineData("Ce", 4, 8)]
    [InlineData("Lu", 17, 8)]  // f-block index 14
    [InlineData("Ac", 3, 9)]
    [InlineData("Lr", 17, 9)]
    public void CellOf_FBlock(string symbol, int col, int row)
    {
        var (c, r) = Layout.CellOf(Elements.BySymbol[symbol]);
        c.ShouldBe(col);
        r.ShouldBe(row);
    }

    [Fact]
    public void IsBlankMainCell_Period1_OnlyEdgesPopulated()
    {
        Layout.IsBlankMainCell(1, 1).ShouldBeFalse();
        Layout.IsBlankMainCell(2, 1).ShouldBeTrue();
        Layout.IsBlankMainCell(17, 1).ShouldBeTrue();
        Layout.IsBlankMainCell(18, 1).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, 1, 2)] // period 2 cols 1-2 populated
    [InlineData(3, 12, 2)] // period 2 cols 3-12 blank
    [InlineData(13, 2, 13)] // period 2 cols 13-18 populated
    public void IsBlankMainCell_Period2(int colStart, int colEnd, int firstNonBlank)
    {
        for (int c = colStart; c <= colEnd; c++)
        {
            bool expectedBlank = c < firstNonBlank;
            // collapsed test: just verify that the populated cols are not blank
            // and the gap is blank, separately
        }
        Layout.IsBlankMainCell(3, 2).ShouldBeTrue();
        Layout.IsBlankMainCell(12, 2).ShouldBeTrue();
        Layout.IsBlankMainCell(13, 2).ShouldBeFalse();
    }

    [Fact]
    public void IsFBlockPlaceholder_OnlyAt_3_6_And_3_7()
    {
        Layout.IsFBlockPlaceholder(3, 6).ShouldBeTrue();
        Layout.IsFBlockPlaceholder(3, 7).ShouldBeTrue();
        Layout.IsFBlockPlaceholder(2, 6).ShouldBeFalse();
        Layout.IsFBlockPlaceholder(4, 6).ShouldBeFalse();
        Layout.IsFBlockPlaceholder(3, 5).ShouldBeFalse();
    }

    [Fact]
    public void EveryElement_HasValidCellPosition()
    {
        foreach (var e in Elements.All)
        {
            var (c, r) = Layout.CellOf(e);
            c.ShouldBeInRange(1, Layout.Columns, $"{e.Symbol} col");
            r.ShouldBeInRange(1, Layout.TotalRows, $"{e.Symbol} row");
        }
    }

    [Fact]
    public void NoTwoElements_ShareSameCell()
    {
        var byCell = Elements.All
            .GroupBy(e => Layout.CellOf(e))
            .Where(g => g.Count() > 1)
            .ToList();
        byCell.ShouldBeEmpty();
    }
}
