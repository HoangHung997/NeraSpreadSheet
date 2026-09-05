using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>Finds the next keyboard-reachable cell while skipping hidden axes.</summary>
public static class SpreadsheetVisibleCellNavigation
{
    /// <summary>
    /// Moves by the requested count of visible rows or columns without landing
    /// on a manually hidden worksheet axis entry.
    /// </summary>
    public static CellAddress GetNextVisibleCell(
        Worksheet worksheet,
        CellAddress current,
        int rowDelta,
        int columnDelta)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if ((rowDelta == 0) == (columnDelta == 0))
        {
            throw new ArgumentException(
                "Navigation must move along exactly one worksheet axis.");
        }

        var remaining = Math.Abs(rowDelta != 0 ? rowDelta : columnDelta);
        var rowStep = Math.Sign(rowDelta);
        var columnStep = Math.Sign(columnDelta);
        var result = current;
        while (remaining > 0)
        {
            if (!TryFindNextVisible(
                    worksheet,
                    result,
                    rowStep,
                    columnStep,
                    out var next))
            {
                break;
            }
            result = next;
            remaining--;
        }
        return result;
    }

    private static bool TryFindNextVisible(
        Worksheet worksheet,
        CellAddress current,
        int rowStep,
        int columnStep,
        out CellAddress next)
    {
        if (rowStep != 0)
        {
            for (var row = current.RowIndex + rowStep;
                 row >= 0 && row < SpreadsheetLimits.MaxRows;)
            {
                if (worksheet.Dimensions.TryGetHiddenRowRange(
                        row,
                        out var hiddenRange))
                {
                    row = rowStep > 0
                        ? hiddenRange.End + 1
                        : hiddenRange.Start - 1;
                    continue;
                }
                next = new CellAddress(row, current.ColumnIndex);
                return true;
            }
        }
        else
        {
            for (var column = current.ColumnIndex + columnStep;
                 column >= 0 && column < SpreadsheetLimits.MaxColumns;)
            {
                if (worksheet.Dimensions.TryGetHiddenColumnRange(
                        column,
                        out var hiddenRange))
                {
                    column = columnStep > 0
                        ? hiddenRange.End + 1
                        : hiddenRange.Start - 1;
                    continue;
                }
                if (!worksheet.Dimensions.IsColumnHidden(column))
                {
                    next = new CellAddress(current.RowIndex, column);
                    return true;
                }
                column += columnStep;
            }
        }

        next = current;
        return false;
    }
}
