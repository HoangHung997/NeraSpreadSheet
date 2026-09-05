namespace NeraSpreadSheet.Core;

internal sealed record WorksheetStructuralState(
    KeyValuePair<CellAddress, CellData>[] Cells,
    KeyValuePair<int, double>[] RowHeights,
    KeyValuePair<int, double>[] ColumnWidths,
    WorksheetAxisInterval[] HiddenRows,
    WorksheetAxisInterval[] HiddenColumns,
    CellRange[] MergedCells,
    WorksheetAxisStyleSpan[] RowStyleSpans,
    WorksheetAxisStyleSpan[] ColumnStyleSpans,
    long NextAxisStyleSequence,
    ConditionalFormattingRule[] ConditionalFormattingRules,
    DataValidationRule[] DataValidationRules,
    SpreadsheetTable[] Tables,
    WorksheetAutoFilter? AutoFilter);
