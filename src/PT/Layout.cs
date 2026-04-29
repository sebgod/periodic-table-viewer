namespace PeriodicTable;

/// <summary>
/// Maps elements to (column, row) cells on the standard wide-form periodic
/// table. Both axes are 1-based and follow the visible layout, not the
/// underlying group/period numbers (which differ for the f-block).
///
/// Layout: 18 columns × 9 rows total.
///   Rows 1–7  → main grid (periods 1–7).
///   Row 8     → lanthanide series (Z=57–71) at columns 3–17.
///   Row 9     → actinide series (Z=89–103) at columns 3–17.
///
/// La (57) and Ac (89) live only in the f-block rows. The cells at
/// (col=3, row=6) and (col=3, row=7) in the main grid are rendered as
/// placeholders ("57–71" / "89–103") by the widget — there is no element
/// at those grid positions.
/// </summary>
public static class Layout
{
    public const int Columns = 18;
    public const int MainRows = 7;
    public const int FBlockRow1 = 8;  // lanthanides
    public const int FBlockRow2 = 9;  // actinides
    public const int TotalRows = 9;

    /// <summary>(col, row) where this element should be drawn. Both 1-based.</summary>
    public static (int Col, int Row) CellOf(Element e) =>
        e.Category switch
        {
            Category.Lanthanide => (3 + (e.FBlockIndex ?? 0), FBlockRow1),
            Category.Actinide   => (3 + (e.FBlockIndex ?? 0), FBlockRow2),
            _ => (e.Group ?? throw new InvalidOperationException(
                    $"Element {e.Symbol} has null Group but is not in f-block"),
                e.Period),
        };

    /// <summary>True if this main-grid cell is the f-block placeholder ("*" / "**").</summary>
    public static bool IsFBlockPlaceholder(int col, int row) =>
        col == 3 && (row == 6 || row == 7);

    /// <summary>True if this main-grid cell is permanently blank (e.g. period 2 cols 3–12).</summary>
    public static bool IsBlankMainCell(int col, int row)
    {
        if (row < 1 || row > MainRows || col < 1 || col > Columns) return true;
        return row switch
        {
            1 => col is not (1 or 18),
            2 or 3 => col is >= 3 and <= 12,
            _ => false,
        };
    }
}
