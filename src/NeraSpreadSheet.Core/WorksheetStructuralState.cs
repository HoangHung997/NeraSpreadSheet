namespace NeraSpreadSheet.Core;

internal sealed record WorksheetStructuralState(
    KeyValuePair<CellAddress, CellData>[] Cells,
    KeyValuePair<int, double>[] RowHeights,
    KeyValuePair<int, double>[] ColumnWidths,
    CellRange[] MergedCells);
