using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetWorksheetFilterButtonHit(
    CellRange FilterRange,
    int ColumnOffset,
    int WorksheetColumnIndex,
    CellAddress HeaderCell,
    RectD Bounds,
    bool IsFiltered);

public static class SpreadsheetWorksheetFilterButtonGeometry
{
    public static IReadOnlyList<SpreadsheetWorksheetFilterButtonHit>
        GetVisibleButtons(
            WorksheetSnapshot worksheet,
            ViewportLayout layout,
            SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        return GetVisibleButtons(worksheet.AutoFilter, layout, theme);
    }

    /// <summary>
    /// Computes visible direct-worksheet AutoFilter buttons from filter
    /// metadata only, without capturing cell contents.
    /// </summary>
    public static IReadOnlyList<SpreadsheetWorksheetFilterButtonHit>
        GetVisibleButtons(
            WorksheetAutoFilter? filter,
            ViewportLayout layout,
            SpreadsheetRenderTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        theme ??= new SpreadsheetRenderTheme();
        if (!theme.ShowTableFilterButtons ||
            filter is null ||
            !filter.HasHeaderRow)
        {
            return Array.Empty<SpreadsheetWorksheetFilterButtonHit>();
        }

        var rowSlots = layout.Rows
            .GroupBy(static slot => slot.Index)
            .ToDictionary(
                static group => group.Key,
                static group => group.First());
        if (!rowSlots.TryGetValue(filter.Range.Top, out var row))
        {
            return Array.Empty<SpreadsheetWorksheetFilterButtonHit>();
        }

        var columnSlots = layout.Columns
            .GroupBy(static slot => slot.Index)
            .ToDictionary(
                static group => group.Key,
                static group => group.First());
        var filteredColumns = filter.Columns
            .Select(static column => column.ColumnOffset)
            .ToHashSet();
        var viewport = new RectD(
            0d,
            0d,
            layout.ViewportSize.Width,
            layout.ViewportSize.Height);
        var result =
            new List<SpreadsheetWorksheetFilterButtonHit>(
                filter.Range.ColumnCount);
        for (var columnOffset = 0;
             columnOffset < filter.Range.ColumnCount;
             columnOffset++)
        {
            var worksheetColumn = checked(
                filter.Range.Left + columnOffset);
            if (!columnSlots.TryGetValue(
                    worksheetColumn,
                    out var column))
            {
                continue;
            }

            var extent = Math.Min(
                theme.TableFilterButtonExtent,
                Math.Min(
                    Math.Max(
                        0d,
                        row.Size -
                        (2d * theme.TableFilterButtonMargin)),
                    Math.Max(
                        0d,
                        column.Size -
                        (2d * theme.TableFilterButtonMargin))));
            if (extent < theme.TableFilterButtonMinimumExtent)
            {
                continue;
            }

            var bounds = new RectD(
                column.End -
                extent -
                theme.TableFilterButtonMargin,
                row.Start + ((row.Size - extent) / 2d),
                extent,
                extent).Intersect(viewport);
            if (bounds.IsEmpty)
            {
                continue;
            }

            result.Add(new SpreadsheetWorksheetFilterButtonHit(
                filter.Range,
                columnOffset,
                worksheetColumn,
                new CellAddress(
                    filter.Range.Top,
                    worksheetColumn),
                bounds,
                filteredColumns.Contains(columnOffset)));
        }

        return result;
    }

    public static bool TryHitTest(
        WorksheetSnapshot worksheet,
        ViewportLayout layout,
        double viewportX,
        double viewportY,
        SpreadsheetRenderTheme? theme,
        out SpreadsheetWorksheetFilterButtonHit hit)
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
