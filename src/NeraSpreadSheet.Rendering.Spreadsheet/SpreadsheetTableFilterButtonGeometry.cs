using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetTableFilterButtonHit(
    Guid TableId,
    Guid ColumnId,
    CellAddress HeaderCell,
    RectD Bounds,
    bool IsFiltered);

public static class SpreadsheetTableFilterButtonGeometry
{
    public static IReadOnlyList<SpreadsheetTableFilterButtonHit> GetVisibleButtons(
        WorksheetSnapshot worksheet,
        ViewportLayout layout,
        SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        return GetVisibleButtons(worksheet.Tables, layout, theme);
    }

    /// <summary>
    /// Computes visible Table filter buttons from Table metadata only. Native
    /// hosts use this overload so paint and pointer input never clone all used
    /// worksheet cells merely to position header chrome.
    /// </summary>
    public static IReadOnlyList<SpreadsheetTableFilterButtonHit> GetVisibleButtons(
        IReadOnlyList<SpreadsheetTable> tables,
        ViewportLayout layout,
        SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(layout);
        theme ??= new SpreadsheetRenderTheme();
        if (!theme.ShowTableFilterButtons)
        {
            return Array.Empty<SpreadsheetTableFilterButtonHit>();
        }

        var rowSlots = layout.Rows
            .GroupBy(static slot => slot.Index)
            .ToDictionary(
                static group => group.Key,
                static group => group.First());
        var columnSlots = layout.Columns
            .GroupBy(static slot => slot.Index)
            .ToDictionary(
                static group => group.Key,
                static group => group.First());
        var viewport = new RectD(
            0d,
            0d,
            layout.ViewportSize.Width,
            layout.ViewportSize.Height);
        var result = new List<SpreadsheetTableFilterButtonHit>();

        foreach (var table in tables)
        {
            if (!table.HasHeaders ||
                !rowSlots.TryGetValue(table.Range.Top, out var row))
            {
                continue;
            }

            var filteredColumns = table.AutoFilter?.Columns
                .Select(static column => column.ColumnId)
                .ToHashSet() ?? [];
            for (var index = 0; index < table.Columns.Count; index++)
            {
                var worksheetColumn = table.Range.Left + index;
                if (!columnSlots.TryGetValue(
                        worksheetColumn,
                        out var column))
                {
                    continue;
                }

                var extent = Math.Min(
                    theme.TableFilterButtonExtent,
                    Math.Min(
                        Math.Max(0d, row.Size - (2d * theme.TableFilterButtonMargin)),
                        Math.Max(0d, column.Size - (2d * theme.TableFilterButtonMargin))));
                if (extent < theme.TableFilterButtonMinimumExtent)
                {
                    continue;
                }

                var bounds = new RectD(
                    column.End - extent - theme.TableFilterButtonMargin,
                    row.Start + ((row.Size - extent) / 2d),
                    extent,
                    extent).Intersect(viewport);
                if (bounds.IsEmpty)
                {
                    continue;
                }

                var tableColumn = table.Columns[index];
                result.Add(new SpreadsheetTableFilterButtonHit(
                    table.Id,
                    tableColumn.Id,
                    new CellAddress(table.Range.Top, worksheetColumn),
                    bounds,
                    filteredColumns.Contains(tableColumn.Id)));
            }
        }

        return result;
    }

    public static bool TryHitTest(
        WorksheetSnapshot worksheet,
        ViewportLayout layout,
        double viewportX,
        double viewportY,
        SpreadsheetRenderTheme? theme,
        out SpreadsheetTableFilterButtonHit hit)
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
