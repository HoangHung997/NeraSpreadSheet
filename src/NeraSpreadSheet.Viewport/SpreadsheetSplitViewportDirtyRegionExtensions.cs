using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport;

public readonly record struct SpreadsheetSplitDirtyRegion(
    SpreadsheetPaneId PaneId,
    RectD Bounds);

public sealed class SpreadsheetSplitDirtyRegionProjection
{
    public SpreadsheetSplitDirtyRegionProjection(
        bool requiresFullInvalidation,
        SpreadsheetSplitDirtyRegion[] regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        RequiresFullInvalidation = requiresFullInvalidation;
        Regions = regions;
    }

    public bool RequiresFullInvalidation { get; }

    public SpreadsheetSplitDirtyRegion[] Regions { get; }

    public static SpreadsheetSplitDirtyRegionProjection FullInvalidation { get; } =
        new(true, []);

    public static SpreadsheetSplitDirtyRegionProjection Empty { get; } =
        new(false, []);
}

public static class SpreadsheetSplitViewportDirtyRegionExtensions
{
    public static SpreadsheetSplitDirtyRegionProjection ProjectDirtyRange(
        this SpreadsheetSplitViewportEngine engine,
        CellRange range)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var frame = engine.LastFrame;
        if (frame is null)
        {
            return SpreadsheetSplitDirtyRegionProjection.FullInvalidation;
        }

        var worksheet = engine.Session.ActiveWorksheet;
        var expanded = ExpandForMergedCells(worksheet, range);
        var subranges = SplitAtFreezeBoundaries(
            expanded,
            engine.Session.View.FrozenRows,
            engine.Session.View.FrozenColumns);
        var regions = new List<SpreadsheetSplitDirtyRegion>(
            frame.Panes.Count * subranges.Count);
        foreach (var pane in frame.Panes)
        {
            foreach (var subrange in subranges)
            {
                if (!TryProjectSubrange(engine, pane, subrange, out var bounds))
                {
                    return SpreadsheetSplitDirtyRegionProjection.FullInvalidation;
                }
                if (!bounds.IsEmpty)
                {
                    regions.Add(new SpreadsheetSplitDirtyRegion(
                        pane.Pane.PaneId,
                        bounds));
                }
            }
        }

        return regions.Count == 0
            ? SpreadsheetSplitDirtyRegionProjection.Empty
            : new SpreadsheetSplitDirtyRegionProjection(false, [.. regions]);
    }

    private static bool TryProjectSubrange(
        SpreadsheetSplitViewportEngine engine,
        SpreadsheetSplitPaneFrame pane,
        CellRange range,
        out RectD bounds)
    {
        if (!engine.TryGetCellBounds(
                pane.Pane.PaneId,
                range.TopLeft,
                out var first) ||
            !engine.TryGetCellBounds(
                pane.Pane.PaneId,
                range.BottomRight,
                out var last))
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

        var candidate = new RectD(left, top, right - left, bottom - top);
        var clip = GetPaneSubregionClip(engine, pane, range);
        bounds = candidate.Intersect(clip);
        return true;
    }

    private static RectD GetPaneSubregionClip(
        SpreadsheetSplitViewportEngine engine,
        SpreadsheetSplitPaneFrame pane,
        CellRange range)
    {
        var frozenRows = engine.Session.View.FrozenRows;
        var frozenColumns = engine.Session.View.FrozenColumns;
        var layout = pane.ViewportFrame.Layout;
        var frozenRowRange = frozenRows > 0 && range.Bottom < frozenRows;
        var frozenColumnRange = frozenColumns > 0 && range.Right < frozenColumns;
        var left = pane.Pane.Bounds.Left +
            (frozenColumnRange ? 0d : layout.FrozenWidth);
        var top = pane.Pane.Bounds.Top +
            (frozenRowRange ? 0d : layout.FrozenHeight);
        var right = pane.Pane.Bounds.Left +
            (frozenColumnRange ? layout.FrozenWidth : pane.Pane.Bounds.Width);
        var bottom = pane.Pane.Bounds.Top +
            (frozenRowRange ? layout.FrozenHeight : pane.Pane.Bounds.Height);
        return right <= left || bottom <= top
            ? RectD.Empty
            : new RectD(left, top, right - left, bottom - top);
    }

    private static List<CellRange> SplitAtFreezeBoundaries(
        CellRange range,
        int frozenRows,
        int frozenColumns)
    {
        var rows = SplitAxis(range.Top, range.Bottom, frozenRows);
        var columns = SplitAxis(range.Left, range.Right, frozenColumns);
        var result = new List<CellRange>(rows.Count * columns.Count);
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                result.Add(new CellRange(
                    new CellAddress(row.Start, column.Start),
                    new CellAddress(row.End, column.End)));
            }
        }
        return result;
    }

    private static List<AxisInterval> SplitAxis(
        int start,
        int end,
        int frozenCount)
    {
        if (frozenCount <= 0 || start >= frozenCount || end < frozenCount)
        {
            return [new AxisInterval(start, end)];
        }

        return
        [
            new AxisInterval(start, frozenCount - 1),
            new AxisInterval(frozenCount, end),
        ];
    }

    private static CellRange ExpandForMergedCells(
        Worksheet worksheet,
        CellRange range)
    {
        var top = range.Top;
        var left = range.Left;
        var bottom = range.Bottom;
        var right = range.Right;
        var changed = true;
        while (changed)
        {
            changed = false;
            var current = new CellRange(
                new CellAddress(top, left),
                new CellAddress(bottom, right));
            foreach (var merged in worksheet.MergedCells.Ranges)
            {
                if (!merged.Intersects(current))
                {
                    continue;
                }

                var nextTop = Math.Min(top, merged.Top);
                var nextLeft = Math.Min(left, merged.Left);
                var nextBottom = Math.Max(bottom, merged.Bottom);
                var nextRight = Math.Max(right, merged.Right);
                if (nextTop == top &&
                    nextLeft == left &&
                    nextBottom == bottom &&
                    nextRight == right)
                {
                    continue;
                }

                top = nextTop;
                left = nextLeft;
                bottom = nextBottom;
                right = nextRight;
                changed = true;
            }
        }

        return new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right));
    }

    private readonly record struct AxisInterval(int Start, int End);
}
