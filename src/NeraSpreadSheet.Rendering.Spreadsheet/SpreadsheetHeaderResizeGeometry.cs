using NeraSpreadSheet.Core;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetHeaderResizeHandle(
    WorksheetAxis Axis,
    int Index,
    double EdgeCoordinate,
    double OriginalSize);

public static class SpreadsheetHeaderResizeGeometry
{
    public const double DefaultHitTolerance = 4d;

    public static bool TryHitResizeHandle(
        double x,
        double y,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme,
        ViewportLayout layout,
        out SpreadsheetHeaderResizeHandle handle,
        double tolerance = DefaultHitTolerance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(layout);
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

        var chrome = SpreadsheetChromeGeometry.Calculate(fullWidth, fullHeight, theme);
        if (x < chrome.RowHeaderWidth && y >= chrome.ColumnHeaderHeight)
        {
            return TryFindRowHandle(
                y - chrome.ColumnHeaderHeight,
                chrome.ColumnHeaderHeight,
                layout.Rows,
                tolerance,
                out handle);
        }

        if (y < chrome.ColumnHeaderHeight && x >= chrome.RowHeaderWidth)
        {
            return TryFindColumnHandle(
                x - chrome.RowHeaderWidth,
                chrome.RowHeaderWidth,
                layout.Columns,
                tolerance,
                out handle);
        }

        handle = default;
        return false;
    }

    public static double CalculateSize(
        SpreadsheetHeaderResizeHandle handle,
        double pointerX,
        double pointerY)
    {
        if (!double.IsFinite(pointerX) || !double.IsFinite(pointerY))
        {
            throw new ArgumentOutOfRangeException(nameof(pointerX), "Pointer coordinates must be finite.");
        }

        var pointerCoordinate = handle.Axis == WorksheetAxis.Row ? pointerY : pointerX;
        return Math.Max(0d, handle.OriginalSize + pointerCoordinate - handle.EdgeCoordinate);
    }

    private static bool TryFindRowHandle(
        double bodyY,
        double headerOriginY,
        IReadOnlyList<AxisSlot> rows,
        double tolerance,
        out SpreadsheetHeaderResizeHandle handle)
    {
        var bestDistance = double.PositiveInfinity;
        var best = default(AxisSlot);
        foreach (var row in rows)
        {
            var distance = Math.Abs(bodyY - row.End);
            if (distance > tolerance || distance >= bestDistance)
            {
                continue;
            }
            bestDistance = distance;
            best = row;
        }

        if (!double.IsFinite(bestDistance))
        {
            handle = default;
            return false;
        }

        handle = new SpreadsheetHeaderResizeHandle(
            WorksheetAxis.Row,
            best.Index,
            headerOriginY + best.End,
            best.Size);
        return true;
    }

    private static bool TryFindColumnHandle(
        double bodyX,
        double headerOriginX,
        IReadOnlyList<AxisSlot> columns,
        double tolerance,
        out SpreadsheetHeaderResizeHandle handle)
    {
        var bestDistance = double.PositiveInfinity;
        var best = default(AxisSlot);
        foreach (var column in columns)
        {
            var distance = Math.Abs(bodyX - column.End);
            if (distance > tolerance || distance >= bestDistance)
            {
                continue;
            }
            bestDistance = distance;
            best = column;
        }

        if (!double.IsFinite(bestDistance))
        {
            handle = default;
            return false;
        }

        handle = new SpreadsheetHeaderResizeHandle(
            WorksheetAxis.Column,
            best.Index,
            headerOriginX + best.End,
            best.Size);
        return true;
    }
}
