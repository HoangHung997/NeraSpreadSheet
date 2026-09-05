using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetSplitHeaderReorderSource(
    SpreadsheetPaneId PaneId,
    WorksheetAxis Axis,
    int Index,
    RectD HeaderBounds);

public readonly record struct SpreadsheetSplitHeaderReorderDropTarget(
    SpreadsheetPaneId PaneId,
    WorksheetAxisMove Move,
    double EdgeCoordinate,
    RectD PreviewBounds)
{
    public WorksheetAxis Axis => Move.Axis;

    public int DestinationBoundary => Move.DestinationBoundary;

    public bool IsNoOp => Move.IsNoOp;
}

public static class SpreadsheetSplitHeaderReorderGeometry
{
    public const double DefaultDragThreshold = 5d;
    public const double DefaultPreviewThickness = 3d;
    private const double GeometryEpsilon = 1e-9;

    public static bool HasExceededDragThreshold(
        PointD start,
        PointD current,
        double threshold = DefaultDragThreshold)
    {
        ValidateFinite(start, nameof(start));
        ValidateFinite(current, nameof(current));
        if (!double.IsFinite(threshold) || threshold < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        var deltaX = current.X - start.X;
        var deltaY = current.Y - start.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) >= threshold * threshold;
    }

    public static bool TryHitSource(
        double x,
        double y,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        out SpreadsheetSplitHeaderReorderSource source,
        double resizeTolerance = SpreadsheetHeaderResizeGeometry.DefaultHitTolerance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(paneLayouts);
        if (!theme.ShowHeaders ||
            !AreFinite(x, y, fullWidth, fullHeight, resizeTolerance) ||
            resizeTolerance < 0d ||
            x < 0d ||
            y < 0d ||
            x >= fullWidth ||
            y >= fullHeight)
        {
            source = default;
            return false;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            fullWidth,
            fullHeight,
            theme);
        if (x < chrome.RowHeaderWidth && y >= chrome.ColumnHeaderHeight)
        {
            return TryHitRowSource(
                y - chrome.ColumnHeaderHeight,
                chrome,
                paneLayouts,
                resizeTolerance,
                out source);
        }
        if (y < chrome.ColumnHeaderHeight && x >= chrome.RowHeaderWidth)
        {
            return TryHitColumnSource(
                x - chrome.RowHeaderWidth,
                chrome,
                paneLayouts,
                resizeTolerance,
                out source);
        }

        source = default;
        return false;
    }

    public static bool TryGetDropTarget(
        WorksheetAxis axis,
        int sourceIndex,
        int count,
        double pointerX,
        double pointerY,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        out SpreadsheetSplitHeaderReorderDropTarget target,
        double previewThickness = DefaultPreviewThickness)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(paneLayouts);
        if (!theme.ShowHeaders ||
            !AreFinite(
                pointerX,
                pointerY,
                fullWidth,
                fullHeight,
                previewThickness) ||
            previewThickness <= 0d ||
            !Enum.IsDefined(axis))
        {
            target = default;
            return false;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            fullWidth,
            fullHeight,
            theme);
        if (axis == WorksheetAxis.Row)
        {
            if (!TryResolveRowBoundary(
                    pointerY - chrome.ColumnHeaderHeight,
                    paneLayouts,
                    out var paneId,
                    out var boundary,
                    out var bodyEdge))
            {
                target = default;
                return false;
            }

            var move = new WorksheetAxisMove(
                axis,
                sourceIndex,
                count,
                boundary);
            var edge = chrome.ColumnHeaderHeight + bodyEdge;
            target = new SpreadsheetSplitHeaderReorderDropTarget(
                paneId,
                move,
                edge,
                new RectD(
                    0d,
                    edge - (previewThickness / 2d),
                    fullWidth,
                    previewThickness));
            return true;
        }

        if (!TryResolveColumnBoundary(
                pointerX - chrome.RowHeaderWidth,
                paneLayouts,
                out var columnPaneId,
                out var columnBoundary,
                out var bodyColumnEdge))
        {
            target = default;
            return false;
        }

        var columnMove = new WorksheetAxisMove(
            axis,
            sourceIndex,
            count,
            columnBoundary);
        var columnEdge = chrome.RowHeaderWidth + bodyColumnEdge;
        target = new SpreadsheetSplitHeaderReorderDropTarget(
            columnPaneId,
            columnMove,
            columnEdge,
            new RectD(
                columnEdge - (previewThickness / 2d),
                0d,
                previewThickness,
                fullHeight));
        return true;
    }

    private static bool TryHitRowSource(
        double bodyY,
        SpreadsheetChromeMetrics chrome,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        double resizeTolerance,
        out SpreadsheetSplitHeaderReorderSource source)
    {
        foreach (var pane in paneLayouts)
        {
            ValidatePaneLayout(pane, paneLayouts);
            if (Math.Abs(pane.Bounds.Left) > GeometryEpsilon ||
                bodyY < pane.Bounds.Top ||
                bodyY >= pane.Bounds.Bottom)
            {
                continue;
            }

            var local = bodyY - pane.Bounds.Top;
            if (!TryFindInteriorSlot(
                    pane.ViewportLayout.Rows,
                    local,
                    resizeTolerance,
                    out var slot))
            {
                source = default;
                return false;
            }

            source = new SpreadsheetSplitHeaderReorderSource(
                pane.PaneId,
                WorksheetAxis.Row,
                slot.Index,
                new RectD(
                    0d,
                    chrome.ColumnHeaderHeight + pane.Bounds.Top + slot.Start,
                    chrome.RowHeaderWidth,
                    slot.Size));
            return true;
        }

        source = default;
        return false;
    }

    private static bool TryHitColumnSource(
        double bodyX,
        SpreadsheetChromeMetrics chrome,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        double resizeTolerance,
        out SpreadsheetSplitHeaderReorderSource source)
    {
        foreach (var pane in paneLayouts)
        {
            ValidatePaneLayout(pane, paneLayouts);
            if (Math.Abs(pane.Bounds.Top) > GeometryEpsilon ||
                bodyX < pane.Bounds.Left ||
                bodyX >= pane.Bounds.Right)
            {
                continue;
            }

            var local = bodyX - pane.Bounds.Left;
            if (!TryFindInteriorSlot(
                    pane.ViewportLayout.Columns,
                    local,
                    resizeTolerance,
                    out var slot))
            {
                source = default;
                return false;
            }

            source = new SpreadsheetSplitHeaderReorderSource(
                pane.PaneId,
                WorksheetAxis.Column,
                slot.Index,
                new RectD(
                    chrome.RowHeaderWidth + pane.Bounds.Left + slot.Start,
                    0d,
                    slot.Size,
                    chrome.ColumnHeaderHeight));
            return true;
        }

        source = default;
        return false;
    }

    private static bool TryResolveRowBoundary(
        double bodyY,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        out SpreadsheetPaneId paneId,
        out int boundary,
        out double bodyEdge)
    {
        if (!TryResolveEdgePane(
                bodyY,
                paneLayouts,
                requireLeftEdge: true,
                out var pane))
        {
            paneId = default;
            boundary = default;
            bodyEdge = default;
            return false;
        }

        var local = Math.Clamp(
            bodyY - pane.Bounds.Top,
            0d,
            pane.Bounds.Height);
        if (!TryResolveBoundary(
                pane.ViewportLayout.Rows,
                local,
                out boundary,
                out var localEdge))
        {
            paneId = default;
            bodyEdge = default;
            return false;
        }

        paneId = pane.PaneId;
        bodyEdge = pane.Bounds.Top + localEdge;
        return true;
    }

    private static bool TryResolveColumnBoundary(
        double bodyX,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        out SpreadsheetPaneId paneId,
        out int boundary,
        out double bodyEdge)
    {
        if (!TryResolveEdgePane(
                bodyX,
                paneLayouts,
                requireLeftEdge: false,
                out var pane))
        {
            paneId = default;
            boundary = default;
            bodyEdge = default;
            return false;
        }

        var local = Math.Clamp(
            bodyX - pane.Bounds.Left,
            0d,
            pane.Bounds.Width);
        if (!TryResolveBoundary(
                pane.ViewportLayout.Columns,
                local,
                out boundary,
                out var localEdge))
        {
            paneId = default;
            bodyEdge = default;
            return false;
        }

        paneId = pane.PaneId;
        bodyEdge = pane.Bounds.Left + localEdge;
        return true;
    }

    private static bool TryResolveEdgePane(
        double coordinate,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        bool requireLeftEdge,
        out SpreadsheetSplitPaneChromeLayout pane)
    {
        SpreadsheetSplitPaneChromeLayout? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var candidate in paneLayouts)
        {
            ValidatePaneLayout(candidate, paneLayouts);
            var touchesRequiredEdge = requireLeftEdge
                ? Math.Abs(candidate.Bounds.Left) <= GeometryEpsilon
                : Math.Abs(candidate.Bounds.Top) <= GeometryEpsilon;
            if (!touchesRequiredEdge)
            {
                continue;
            }

            var start = requireLeftEdge
                ? candidate.Bounds.Top
                : candidate.Bounds.Left;
            var end = requireLeftEdge
                ? candidate.Bounds.Bottom
                : candidate.Bounds.Right;
            if (coordinate >= start && coordinate < end)
            {
                pane = candidate;
                return true;
            }

            var distance = coordinate < start
                ? start - coordinate
                : coordinate - end;
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        pane = nearest!;
        return nearest is not null;
    }

    private static bool TryFindInteriorSlot(
        IReadOnlyList<AxisSlot> slots,
        double coordinate,
        double resizeTolerance,
        out AxisSlot slot)
    {
        foreach (var candidate in slots)
        {
            if (coordinate < candidate.Start || coordinate >= candidate.End)
            {
                continue;
            }

            var distanceToStart = coordinate - candidate.Start;
            var distanceToEnd = candidate.End - coordinate;
            if (distanceToStart <= resizeTolerance ||
                distanceToEnd <= resizeTolerance)
            {
                slot = default;
                return false;
            }

            slot = candidate;
            return true;
        }

        slot = default;
        return false;
    }

    private static bool TryResolveBoundary(
        IReadOnlyList<AxisSlot> slots,
        double coordinate,
        out int boundary,
        out double edge)
    {
        if (slots.Count == 0)
        {
            boundary = default;
            edge = default;
            return false;
        }

        var first = slots[0];
        if (coordinate <= first.Start)
        {
            boundary = first.Index;
            edge = first.Start;
            return true;
        }

        foreach (var slot in slots)
        {
            if (coordinate >= slot.End)
            {
                continue;
            }

            if (coordinate < slot.Start + (slot.Size / 2d))
            {
                boundary = slot.Index;
                edge = slot.Start;
            }
            else
            {
                boundary = checked(slot.Index + 1);
                edge = slot.End;
            }
            return true;
        }

        var last = slots[^1];
        boundary = checked(last.Index + 1);
        edge = last.End;
        return true;
    }

    private static void ValidatePaneLayout(
        SpreadsheetSplitPaneChromeLayout pane,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(pane.ViewportLayout);
        if (Math.Abs(
                pane.Bounds.Width -
                pane.ViewportLayout.ViewportSize.Width) > GeometryEpsilon ||
            Math.Abs(
                pane.Bounds.Height -
                pane.ViewportLayout.ViewportSize.Height) > GeometryEpsilon)
        {
            throw new ArgumentException(
                $"Pane '{pane.PaneId}' viewport size does not match its bounds.",
                nameof(paneLayouts));
        }
    }

    private static void ValidateFinite(PointD point, string parameterName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static bool AreFinite(params double[] values) =>
        values.All(double.IsFinite);
}

public static class SpreadsheetHeaderReorderPreviewDisplayListComposer
{
    public static DisplayList Compose(
        DisplayList body,
        SpreadsheetSplitHeaderReorderDropTarget? target,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(theme);
        if (target is not { } dropTarget)
        {
            return body;
        }

        var builder = new DisplayListBuilder();
        builder.Append(body);
        builder.FillRectangle(
            dropTarget.PreviewBounds,
            dropTarget.IsNoOp
                ? theme.HeaderBorder
                : theme.ActivePaneBorder);
        return builder.Build();
    }
}
