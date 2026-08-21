namespace NeraSpreadSheet.Core;

public readonly record struct FilteredRowSpan
{
    public FilteredRowSpan(int startRowIndex, int endRowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startRowIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            endRowIndex,
            startRowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            endRowIndex,
            SpreadsheetLimits.MaxRows);
        StartRowIndex = startRowIndex;
        EndRowIndex = endRowIndex;
    }

    public int StartRowIndex { get; }

    public int EndRowIndex { get; }

    public int RowCount => checked(EndRowIndex - StartRowIndex + 1);
}

public static class WorksheetSnapshotFilterProjectionExtensions
{
    public static IReadOnlyList<FilteredRowSpan> GetFilteredOutRowSpans(
        this WorksheetSnapshot worksheet,
        int maximumRowsToEvaluate = SpreadsheetLimits.MaxRows)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumRowsToEvaluate);

        var filteredTables = worksheet.Tables
            .Where(static table =>
                table.AutoFilter is { Columns.Count: > 0 } &&
                table.DataRange is not null)
            .OrderBy(static table => table.Range.Top)
            .ThenBy(static table => table.Range.Left)
            .ToArray();
        var worksheetFilter = worksheet.AutoFilter;
        var worksheetFilterRows =
            worksheetFilter?.DataRange?.RowCount ?? 0;
        var requestedRows = filteredTables.Sum(static table =>
            (long)table.DataRange!.Value.RowCount) +
            worksheetFilterRows;
        if (requestedRows > maximumRowsToEvaluate)
        {
            throw new InvalidOperationException(
                $"Filtered-row projection requires evaluating {requestedRows} rows, " +
                $"which exceeds the configured limit of {maximumRowsToEvaluate}.");
        }

        var spans = new List<FilteredRowSpan>();
        if (worksheetFilter is
            {
                Columns.Count: > 0,
                DataRange: { } worksheetDataRange,
            })
        {
            AppendFilteredRows(
                spans,
                worksheetDataRange,
                rowIndex => worksheetFilter.IsRowVisible(
                    worksheet,
                    rowIndex));
        }

        foreach (var table in filteredTables)
        {
            var dataRange = table.DataRange!.Value;
            AppendFilteredRows(
                spans,
                dataRange,
                rowIndex => table.IsRowVisible(
                    worksheet,
                    rowIndex));
        }

        return spans;
    }

    private static void AppendFilteredRows(
        List<FilteredRowSpan> spans,
        CellRange dataRange,
        Func<int, bool> isRowVisible)
    {
        ArgumentNullException.ThrowIfNull(isRowVisible);
            int? spanStart = null;
            for (var rowIndex = dataRange.Top;
                 rowIndex <= dataRange.Bottom;
                 rowIndex++)
            {
                if (!isRowVisible(rowIndex))
                {
                    spanStart ??= rowIndex;
                    continue;
                }

                if (spanStart is int start)
                {
                    AppendSpan(spans, start, rowIndex - 1);
                    spanStart = null;
                }
            }

            if (spanStart is int remainingStart)
            {
                AppendSpan(spans, remainingStart, dataRange.Bottom);
            }
    }

    private static void AppendSpan(
        List<FilteredRowSpan> spans,
        int startRowIndex,
        int endRowIndex)
    {
        if (spans.Count > 0 &&
            spans[^1].EndRowIndex + 1 >= startRowIndex)
        {
            var previous = spans[^1];
            spans[^1] = new FilteredRowSpan(
                previous.StartRowIndex,
                Math.Max(previous.EndRowIndex, endRowIndex));
            return;
        }

        spans.Add(new FilteredRowSpan(startRowIndex, endRowIndex));
    }
}
