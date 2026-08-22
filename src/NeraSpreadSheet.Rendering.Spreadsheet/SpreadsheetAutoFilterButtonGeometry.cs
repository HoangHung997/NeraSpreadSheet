using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public enum SpreadsheetAutoFilterButtonOwnerKind
{
    Table,
    Worksheet,
}

public readonly record struct SpreadsheetAutoFilterButtonHit(
    SpreadsheetAutoFilterButtonOwnerKind OwnerKind,
    Guid? TableId,
    Guid? TableColumnId,
    CellRange FilterRange,
    int ColumnOffset,
    int WorksheetColumnIndex,
    CellAddress HeaderCell,
    RectD Bounds,
    bool IsFiltered);

/// <summary>
/// Produces one shared filter-button stream for Table and direct worksheet
/// AutoFilter headers. The two ownership models cannot overlap in the current
/// Core contract, so host hit testing remains deterministic.
/// </summary>
public static class SpreadsheetAutoFilterButtonGeometry
{
    public static IReadOnlyList<SpreadsheetAutoFilterButtonHit>
        GetVisibleButtons(
            WorksheetSnapshot worksheet,
            ViewportLayout layout,
            SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(layout);
        theme ??= new SpreadsheetRenderTheme();

        var tableButtons = SpreadsheetTableFilterButtonGeometry
            .GetVisibleButtons(worksheet, layout, theme);
        var worksheetButtons = SpreadsheetWorksheetFilterButtonGeometry
            .GetVisibleButtons(worksheet, layout, theme);
        var result = new List<SpreadsheetAutoFilterButtonHit>(
            checked(tableButtons.Count + worksheetButtons.Count));
        var tablesById = worksheet.Tables.ToDictionary(
            static table => table.Id);
        foreach (var button in tableButtons)
        {
            if (!tablesById.TryGetValue(button.TableId, out var table))
            {
                continue;
            }
            var columnOffset = checked(
                button.HeaderCell.ColumnIndex - table.Range.Left);
            result.Add(new SpreadsheetAutoFilterButtonHit(
                SpreadsheetAutoFilterButtonOwnerKind.Table,
                button.TableId,
                button.ColumnId,
                table.Range,
                columnOffset,
                button.HeaderCell.ColumnIndex,
                button.HeaderCell,
                button.Bounds,
                button.IsFiltered));
        }
        result.AddRange(worksheetButtons.Select(static button =>
            new SpreadsheetAutoFilterButtonHit(
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet,
                null,
                null,
                button.FilterRange,
                button.ColumnOffset,
                button.WorksheetColumnIndex,
                button.HeaderCell,
                button.Bounds,
                button.IsFiltered)));
        return result;
    }

    public static bool TryHitTest(
        WorksheetSnapshot worksheet,
        ViewportLayout layout,
        double viewportX,
        double viewportY,
        SpreadsheetRenderTheme? theme,
        out SpreadsheetAutoFilterButtonHit hit)
    {
        if (!double.IsFinite(viewportX) ||
            !double.IsFinite(viewportY))
        {
            hit = default;
            return false;
        }

        var point = new PointD(viewportX, viewportY);
        foreach (var candidate in GetVisibleButtons(
                     worksheet,
                     layout,
                     theme))
        {
            if (candidate.Bounds.Contains(point))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }
}
