using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout;

public enum SpreadsheetPaneId
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public enum SpreadsheetSplitHitRegionKind
{
    None,
    Pane,
    VerticalSeparator,
    HorizontalSeparator,
    SeparatorIntersection,
}

public readonly record struct SpreadsheetSplitRequest(
    SizeD ViewportSize,
    double? SplitX = null,
    double? SplitY = null,
    double SeparatorThickness = 4d,
    double MinimumPaneExtent = 48d);

public readonly record struct SpreadsheetPaneLayout(
    SpreadsheetPaneId PaneId,
    RectD Bounds)
{
    public PointD ToLocal(PointD viewportPoint) => new(
        viewportPoint.X - Bounds.X,
        viewportPoint.Y - Bounds.Y);
}

public readonly record struct SpreadsheetSplitHitTest(
    SpreadsheetSplitHitRegionKind RegionKind,
    SpreadsheetPaneId? PaneId,
    PointD LocalPoint)
{
    public static SpreadsheetSplitHitTest None => new(
        SpreadsheetSplitHitRegionKind.None,
        null,
        default);
}

public sealed record SpreadsheetSplitLayout(
    SizeD ViewportSize,
    double SeparatorThickness,
    double? SplitX,
    double? SplitY,
    RectD VerticalSeparator,
    RectD HorizontalSeparator,
    IReadOnlyList<SpreadsheetPaneLayout> Panes)
{
    public bool HasVerticalSplit => !VerticalSeparator.IsEmpty;
    public bool HasHorizontalSplit => !HorizontalSeparator.IsEmpty;
    public bool HasSplitPanes => HasVerticalSplit || HasHorizontalSplit;

    public bool TryGetPane(SpreadsheetPaneId paneId, out SpreadsheetPaneLayout pane)
    {
        foreach (var candidate in Panes)
        {
            if (candidate.PaneId == paneId)
            {
                pane = candidate;
                return true;
            }
        }

        pane = default;
        return false;
    }

    public SpreadsheetSplitHitTest HitTest(PointD viewportPoint)
    {
        if (!IsInsideViewport(viewportPoint))
        {
            return SpreadsheetSplitHitTest.None;
        }

        var inVerticalSeparator = HasVerticalSplit && ContainsHalfOpen(VerticalSeparator, viewportPoint);
        var inHorizontalSeparator = HasHorizontalSplit && ContainsHalfOpen(HorizontalSeparator, viewportPoint);
        if (inVerticalSeparator && inHorizontalSeparator)
        {
            return new SpreadsheetSplitHitTest(
                SpreadsheetSplitHitRegionKind.SeparatorIntersection,
                null,
                default);
        }
        if (inVerticalSeparator)
        {
            return new SpreadsheetSplitHitTest(
                SpreadsheetSplitHitRegionKind.VerticalSeparator,
                null,
                default);
        }
        if (inHorizontalSeparator)
        {
            return new SpreadsheetSplitHitTest(
                SpreadsheetSplitHitRegionKind.HorizontalSeparator,
                null,
                default);
        }

        foreach (var pane in Panes)
        {
            if (!ContainsHalfOpen(pane.Bounds, viewportPoint))
            {
                continue;
            }

            return new SpreadsheetSplitHitTest(
                SpreadsheetSplitHitRegionKind.Pane,
                pane.PaneId,
                pane.ToLocal(viewportPoint));
        }

        return SpreadsheetSplitHitTest.None;
    }

    private bool IsInsideViewport(PointD point) =>
        point.X >= 0d &&
        point.Y >= 0d &&
        point.X < ViewportSize.Width &&
        point.Y < ViewportSize.Height;

    private static bool ContainsHalfOpen(RectD bounds, PointD point) =>
        point.X >= bounds.Left &&
        point.Y >= bounds.Top &&
        point.X < bounds.Right &&
        point.Y < bounds.Bottom;
}

public static class SpreadsheetSplitLayoutEngine
{
    public static SpreadsheetSplitLayout Compute(SpreadsheetSplitRequest request)
    {
        ValidateRequest(request);
        var width = request.ViewportSize.Width;
        var height = request.ViewportSize.Height;
        var splitX = ResolveSplit(
            request.SplitX,
            width,
            request.SeparatorThickness,
            request.MinimumPaneExtent);
        var splitY = ResolveSplit(
            request.SplitY,
            height,
            request.SeparatorThickness,
            request.MinimumPaneExtent);
        var verticalSeparator = splitX is { } x
            ? new RectD(x, 0d, request.SeparatorThickness, height)
            : RectD.Empty;
        var horizontalSeparator = splitY is { } y
            ? new RectD(0d, y, width, request.SeparatorThickness)
            : RectD.Empty;
        var panes = BuildPanes(
            width,
            height,
            splitX,
            splitY,
            request.SeparatorThickness);

        return new SpreadsheetSplitLayout(
            request.ViewportSize,
            request.SeparatorThickness,
            splitX,
            splitY,
            verticalSeparator,
            horizontalSeparator,
            panes);
    }

    private static IReadOnlyList<SpreadsheetPaneLayout> BuildPanes(
        double width,
        double height,
        double? splitX,
        double? splitY,
        double separatorThickness)
    {
        if (splitX is null && splitY is null)
        {
            return [new SpreadsheetPaneLayout(
                SpreadsheetPaneId.TopLeft,
                new RectD(0d, 0d, width, height))];
        }

        if (splitX is { } vertical && splitY is null)
        {
            return
            [
                new SpreadsheetPaneLayout(
                    SpreadsheetPaneId.TopLeft,
                    new RectD(0d, 0d, vertical, height)),
                new SpreadsheetPaneLayout(
                    SpreadsheetPaneId.TopRight,
                    new RectD(
                        vertical + separatorThickness,
                        0d,
                        Math.Max(0d, width - vertical - separatorThickness),
                        height)),
            ];
        }

        if (splitX is null && splitY is { } horizontal)
        {
            return
            [
                new SpreadsheetPaneLayout(
                    SpreadsheetPaneId.TopLeft,
                    new RectD(0d, 0d, width, horizontal)),
                new SpreadsheetPaneLayout(
                    SpreadsheetPaneId.BottomLeft,
                    new RectD(
                        0d,
                        horizontal + separatorThickness,
                        width,
                        Math.Max(0d, height - horizontal - separatorThickness))),
            ];
        }

        var resolvedX = splitX.GetValueOrDefault();
        var resolvedY = splitY.GetValueOrDefault();
        var rightWidth = Math.Max(0d, width - resolvedX - separatorThickness);
        var bottomHeight = Math.Max(0d, height - resolvedY - separatorThickness);
        return
        [
            new SpreadsheetPaneLayout(
                SpreadsheetPaneId.TopLeft,
                new RectD(0d, 0d, resolvedX, resolvedY)),
            new SpreadsheetPaneLayout(
                SpreadsheetPaneId.TopRight,
                new RectD(
                    resolvedX + separatorThickness,
                    0d,
                    rightWidth,
                    resolvedY)),
            new SpreadsheetPaneLayout(
                SpreadsheetPaneId.BottomLeft,
                new RectD(
                    0d,
                    resolvedY + separatorThickness,
                    resolvedX,
                    bottomHeight)),
            new SpreadsheetPaneLayout(
                SpreadsheetPaneId.BottomRight,
                new RectD(
                    resolvedX + separatorThickness,
                    resolvedY + separatorThickness,
                    rightWidth,
                    bottomHeight)),
        ];
    }

    private static double? ResolveSplit(
        double? requested,
        double viewportExtent,
        double separatorThickness,
        double minimumPaneExtent)
    {
        if (requested is null || viewportExtent < (minimumPaneExtent * 2d) + separatorThickness)
        {
            return null;
        }

        return Math.Clamp(
            requested.Value,
            minimumPaneExtent,
            viewportExtent - separatorThickness - minimumPaneExtent);
    }

    private static void ValidateRequest(SpreadsheetSplitRequest request)
    {
        if (request.SplitX is { } splitX && !double.IsFinite(splitX))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "SplitX must be finite when specified.");
        }
        if (request.SplitY is { } splitY && !double.IsFinite(splitY))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "SplitY must be finite when specified.");
        }
        if (!double.IsFinite(request.SeparatorThickness) || request.SeparatorThickness <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "SeparatorThickness must be finite and positive.");
        }
        if (!double.IsFinite(request.MinimumPaneExtent) || request.MinimumPaneExtent <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MinimumPaneExtent must be finite and positive.");
        }
    }
}
