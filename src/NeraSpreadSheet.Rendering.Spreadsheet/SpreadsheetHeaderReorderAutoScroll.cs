using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public static class SpreadsheetHeaderReorderAutoScroll
{
    public const double DefaultEdgeZone = 48d;
    public const double DefaultMaximumSpeed = 960d;
    private const double GeometryEpsilon = 1e-9;

    public static PointD CalculateVelocity(
        WorksheetAxis axis,
        double pointerX,
        double pointerY,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme,
        double edgeZone = DefaultEdgeZone,
        double maximumSpeed = DefaultMaximumSpeed)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!AreFinite(pointerX, pointerY, fullWidth, fullHeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pointerX),
                "Pointer and host coordinates must be finite.");
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            fullWidth,
            fullHeight,
            theme);
        return CalculateVelocity(
            axis,
            new PointD(pointerX, pointerY),
            new RectD(
                chrome.RowHeaderWidth,
                chrome.ColumnHeaderHeight,
                chrome.BodyWidth,
                chrome.BodyHeight),
            edgeZone,
            maximumSpeed);
    }

    public static PointD CalculateVelocity(
        WorksheetAxis axis,
        PointD location,
        RectD viewportBounds,
        double edgeZone = DefaultEdgeZone,
        double maximumSpeed = DefaultMaximumSpeed)
    {
        if (!Enum.IsDefined(axis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }
        ValidateFinite(location, nameof(location));
        ValidateFinite(viewportBounds, nameof(viewportBounds));
        if (!double.IsFinite(edgeZone) || edgeZone <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeZone));
        }
        if (!double.IsFinite(maximumSpeed) || maximumSpeed <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSpeed));
        }
        if (viewportBounds.Width <= GeometryEpsilon ||
            viewportBounds.Height <= GeometryEpsilon)
        {
            return default;
        }

        return axis == WorksheetAxis.Row
            ? new PointD(
                0d,
                CalculateAxisVelocity(
                    location.Y,
                    viewportBounds.Top,
                    viewportBounds.Bottom,
                    edgeZone,
                    maximumSpeed))
            : new PointD(
                CalculateAxisVelocity(
                    location.X,
                    viewportBounds.Left,
                    viewportBounds.Right,
                    edgeZone,
                    maximumSpeed),
                0d);
    }

    public static PointD CalculateDelta(PointD velocity, TimeSpan elapsed)
    {
        ValidateFinite(velocity, nameof(velocity));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            elapsed,
            TimeSpan.Zero);

        var seconds = elapsed.TotalSeconds;
        if (!double.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        return new PointD(velocity.X * seconds, velocity.Y * seconds);
    }

    public static bool IsZero(PointD velocity) =>
        Math.Abs(velocity.X) <= GeometryEpsilon &&
        Math.Abs(velocity.Y) <= GeometryEpsilon;

    private static double CalculateAxisVelocity(
        double coordinate,
        double start,
        double end,
        double edgeZone,
        double maximumSpeed)
    {
        var leadingLimit = Math.Min(start + edgeZone, end);
        if (coordinate < leadingLimit)
        {
            var strength = Math.Clamp(
                (leadingLimit - coordinate) / edgeZone,
                0d,
                1d);
            return -maximumSpeed * strength * strength;
        }

        var trailingLimit = Math.Max(end - edgeZone, start);
        if (coordinate > trailingLimit)
        {
            var strength = Math.Clamp(
                (coordinate - trailingLimit) / edgeZone,
                0d,
                1d);
            return maximumSpeed * strength * strength;
        }

        return 0d;
    }

    private static void ValidateFinite(PointD point, string parameterName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFinite(RectD bounds, string parameterName)
    {
        if (!double.IsFinite(bounds.X) ||
            !double.IsFinite(bounds.Y) ||
            !double.IsFinite(bounds.Width) ||
            !double.IsFinite(bounds.Height))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static bool AreFinite(params double[] values) =>
        values.All(double.IsFinite);
}
