using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Viewport;

public static class SpreadsheetViewportDirtyRegionExtensions
{
    public static bool TryGetRangeBounds(
        this SpreadsheetViewportEngine engine,
        CellRange range,
        double scrollX,
        double scrollY,
        out RectD bounds)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var expanded = ExpandForMergedCells(engine.Session.ActiveWorksheet, range);
        if (!engine.TryGetCellBounds(expanded.TopLeft, scrollX, scrollY, out var first) ||
            !engine.TryGetCellBounds(expanded.BottomRight, scrollX, scrollY, out var last))
        {
            bounds = RectD.Empty;
            return false;
        }

        var left = Math.Min(first.Left, last.Left);
        var top = Math.Min(first.Top, last.Top);
        var right = Math.Max(first.Right, last.Right);
        var bottom = Math.Max(first.Bottom, last.Bottom);
        if (right <= left || bottom <= top)
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = new RectD(left, top, right - left, bottom - top);
        return true;
    }

    private static CellRange ExpandForMergedCells(Worksheet worksheet, CellRange range)
    {
        var top = range.Top;
        var left = range.Left;
        var bottom = range.Bottom;
        var right = range.Right;
        var changed = true;

        while (changed)
        {
            changed = false;
            var current = new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
            foreach (var merged in worksheet.MergedCells.Ranges)
            {
                if (!merged.Intersects(current))
                {
                    continue;
                }

                var expandedTop = Math.Min(top, merged.Top);
                var expandedLeft = Math.Min(left, merged.Left);
                var expandedBottom = Math.Max(bottom, merged.Bottom);
                var expandedRight = Math.Max(right, merged.Right);
                if (expandedTop == top && expandedLeft == left && expandedBottom == bottom && expandedRight == right)
                {
                    continue;
                }

                top = expandedTop;
                left = expandedLeft;
                bottom = expandedBottom;
                right = expandedRight;
                changed = true;
            }
        }

        return new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
    }
}
