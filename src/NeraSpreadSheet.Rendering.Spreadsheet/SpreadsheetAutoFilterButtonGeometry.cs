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
    bool IsFiltered,
    bool IsSorted = false,
    bool? SortDescending = null)
{
    public SpreadsheetFilterHeaderState HeaderState =>
        IsFiltered
            ? IsSorted ? SpreadsheetFilterHeaderState.FilteredAndSorted : SpreadsheetFilterHeaderState.Filtered
            : IsSorted ? SpreadsheetFilterHeaderState.Sorted : SpreadsheetFilterHeaderState.None;
}

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
        return GetVisibleButtons(
            worksheet.Tables,
            worksheet.AutoFilter,
            layout,
            theme);
    }

    /// <summary>
    /// Computes the visible combined button stream from filter metadata only.
    /// This overload is intended for native paint/input paths where capturing
    /// every used cell would make pointer movement proportional to sheet size.
    /// </summary>
    public static IReadOnlyList<SpreadsheetAutoFilterButtonHit>
        GetVisibleButtons(
            IReadOnlyList<SpreadsheetTable> tables,
            WorksheetAutoFilter? worksheetFilter,
            ViewportLayout layout,
            SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(layout);
        theme ??= new SpreadsheetRenderTheme();

        var tableButtons = SpreadsheetTableFilterButtonGeometry
            .GetVisibleButtons(tables, layout, theme);
        var worksheetButtons = SpreadsheetWorksheetFilterButtonGeometry
            .GetVisibleButtons(worksheetFilter, layout, theme);
        var result = new List<SpreadsheetAutoFilterButtonHit>(
            checked(tableButtons.Count + worksheetButtons.Count));
        var tablesById = tables.ToDictionary(
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
                button.IsFiltered,
                button.IsSorted,
                button.SortDescending));
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
                button.IsFiltered,
                button.IsSorted,
                button.SortDescending)));
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
