using NeraSpreadSheet.Core;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetSplitHeaderResizeHandle(
    SpreadsheetPaneId PaneId,
    SpreadsheetHeaderResizeHandle HeaderHandle)
{
    public WorksheetAxis Axis => HeaderHandle.Axis;

    public int Index => HeaderHandle.Index;

    public double EdgeCoordinate => HeaderHandle.EdgeCoordinate;

    public double OriginalSize => HeaderHandle.OriginalSize;
}

public static class SpreadsheetSplitHeaderResizeGeometry
{
    private const double GeometryEpsilon = 1e-9;

    public static bool TryHitResizeHandle(
        double x,
        double y,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        out SpreadsheetSplitHeaderResizeHandle handle,
        double tolerance = SpreadsheetHeaderResizeGeometry.DefaultHitTolerance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(paneLayouts);
        if (!theme.ShowHeaders ||
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(tolerance) ||
            tolerance < 0d ||
            x < 0d ||
            y < 0d ||
            x >= fullWidth ||
            y >= fullHeight)
        {
            handle = default;
            return false;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            fullWidth,
            fullHeight,
            theme);
        if (x < chrome.RowHeaderWidth && y >= chrome.ColumnHeaderHeight)
        {
            return TryFindRowHandle(
                y - chrome.ColumnHeaderHeight,
                chrome.ColumnHeaderHeight,
                paneLayouts,
                tolerance,
                out handle);
        }

        if (y < chrome.ColumnHeaderHeight && x >= chrome.RowHeaderWidth)
        {
            return TryFindColumnHandle(
                x - chrome.RowHeaderWidth,
                chrome.RowHeaderWidth,
                paneLayouts,
                tolerance,
                out handle);
        }

        handle = default;
        return false;
    }

    public static double CalculateSize(
        SpreadsheetSplitHeaderResizeHandle handle,
        double pointerX,
        double pointerY) =>
        SpreadsheetHeaderResizeGeometry.CalculateSize(
            handle.HeaderHandle,
            pointerX,
            pointerY);

    private static bool TryFindRowHandle(
        double bodyY,
        double headerOriginY,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        double tolerance,
        out SpreadsheetSplitHeaderResizeHandle handle)
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

            if (TryFindAxisHandle(
                bodyY - pane.Bounds.Top,
                headerOriginY + pane.Bounds.Top,
                pane.ViewportLayout.Rows,
                WorksheetAxis.Row,
                tolerance,
                out var headerHandle))
            {
                handle = new SpreadsheetSplitHeaderResizeHandle(
                    pane.PaneId,
                    headerHandle);
                return true;
            }
        }

        handle = default;
        return false;
    }

    private static bool TryFindColumnHandle(
        double bodyX,
        double headerOriginX,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        double tolerance,
        out SpreadsheetSplitHeaderResizeHandle handle)
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

            if (TryFindAxisHandle(
                bodyX - pane.Bounds.Left,
                headerOriginX + pane.Bounds.Left,
                pane.ViewportLayout.Columns,
                WorksheetAxis.Column,
                tolerance,
                out var headerHandle))
            {
                handle = new SpreadsheetSplitHeaderResizeHandle(
                    pane.PaneId,
                    headerHandle);
                return true;
            }
        }

        handle = default;
        return false;
    }

    private static bool TryFindAxisHandle(
        double localCoordinate,
        double headerOrigin,
        IReadOnlyList<AxisSlot> slots,
        WorksheetAxis axis,
        double tolerance,
        out SpreadsheetHeaderResizeHandle handle)
    {
        var bestDistance = double.PositiveInfinity;
        var best = default(AxisSlot);
        foreach (var slot in slots)
        {
            var distance = Math.Abs(localCoordinate - slot.End);
            if (distance > tolerance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = slot;
        }

        if (!double.IsFinite(bestDistance))
        {
            handle = default;
            return false;
        }

        handle = new SpreadsheetHeaderResizeHandle(
            axis,
            best.Index,
            headerOrigin + best.End,
            best.Size);
        return true;
    }

    private static void ValidatePaneLayout(
        SpreadsheetSplitPaneChromeLayout pane,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(pane.ViewportLayout);
        if (Math.Abs(pane.Bounds.Width - pane.ViewportLayout.ViewportSize.Width) > GeometryEpsilon ||
            Math.Abs(pane.Bounds.Height - pane.ViewportLayout.ViewportSize.Height) > GeometryEpsilon)
        {
            throw new ArgumentException(
                $"Pane '{pane.PaneId}' viewport size does not match its bounds.",
                nameof(paneLayouts));
        }
    }
}
