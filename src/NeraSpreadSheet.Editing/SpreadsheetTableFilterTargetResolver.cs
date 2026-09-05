using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public readonly record struct SpreadsheetTableFilterTarget(
    Guid TableId,
    Guid ColumnId,
    string TableName,
    string ColumnName,
    CellRange TableRange,
    int WorksheetColumnIndex);

/// <summary>
/// Resolves a worksheet address to the Table column whose filter presenter
/// should open. Overlap is already forbidden by the Core Table model.
/// </summary>
public static class SpreadsheetTableFilterTargetResolver
{
    public static bool TryResolveActiveTableFilterTarget(
        this SpreadsheetSession session,
        out SpreadsheetTableFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.TryResolveTableFilterTarget(
            session.Selection.ActiveCell,
            out target);
    }

    public static bool TryResolveTableFilterTarget(
        this SpreadsheetSession session,
        CellAddress address,
        out SpreadsheetTableFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (var table in session.ActiveWorksheet.Tables)
        {
            if (!table.Range.Contains(address))
            {
                continue;
            }

            var columnIndex = address.ColumnIndex - table.Range.Left;
            if (columnIndex < 0 || columnIndex >= table.Columns.Count)
            {
                continue;
            }

            var column = table.Columns[columnIndex];
            target = new SpreadsheetTableFilterTarget(
                table.Id,
                column.Id,
                table.Name,
                column.Name,
                table.Range,
                address.ColumnIndex);
            return true;
        }

        target = default;
        return false;
    }
}
