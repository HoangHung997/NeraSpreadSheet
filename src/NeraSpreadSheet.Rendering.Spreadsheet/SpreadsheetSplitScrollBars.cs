using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public enum SpreadsheetScrollBarAxis
{
    Horizontal,
    Vertical,
}

public enum SpreadsheetScrollBarHitKind
{
    None,
    Thumb,
    TrackBeforeThumb,
    TrackAfterThumb,
}

public sealed record SpreadsheetSplitScrollBarStyle
{
    public bool IsVisible { get; init; } = true;

    public double Thickness { get; init; } = 12d;

    public double Margin { get; init; } = 2d;

    public double MinimumThumbLength { get; init; } = 28d;

    public double MinimumTrackLength { get; init; } = 24d;

    public double HitSlop { get; init; } = 2d;

    public double PageFactor { get; init; } = 0.9d;

    public ColorRgba TrackColor { get; init; } = new(232, 232, 232, 224);

    public ColorRgba ThumbColor { get; init; } = new(148, 148, 148, 232);

    public ColorRgba ActiveThumbColor { get; init; } = new(78, 124, 183, 240);

    public ColorRgba BorderColor { get; init; } = new(112, 112, 112, 224);
}

public readonly record struct SpreadsheetSplitPaneScrollBarState(
    SpreadsheetPaneId PaneId,
    RectD PaneBounds,
    double OffsetX,
    double OffsetY,
    double ContentWidth,
    double ContentHeight);

public readonly record struct SpreadsheetSplitScrollBar(
    SpreadsheetPaneId PaneId,
    SpreadsheetScrollBarAxis Axis,
    RectD TrackBounds,
    RectD ThumbBounds,
    double Offset,
    double MaximumOffset,
    double ViewportExtent,
    double ContentExtent)
{
    public bool IsScrollable => MaximumOffset > 0d;

    public double TrackStart => Axis == SpreadsheetScrollBarAxis.Horizontal
        ? TrackBounds.Left
        : TrackBounds.Top;

    public double TrackLength => Axis == SpreadsheetScrollBarAxis.Horizontal
        ? TrackBounds.Width
        : TrackBounds.Height;

    public double ThumbStart => Axis == SpreadsheetScrollBarAxis.Horizontal
        ? ThumbBounds.Left
        : ThumbBounds.Top;

    public double ThumbLength => Axis == SpreadsheetScrollBarAxis.Horizontal
        ? ThumbBounds.Width
        : ThumbBounds.Height;
}

public readonly record struct SpreadsheetSplitScrollBarHit(
    SpreadsheetPaneId PaneId,
    SpreadsheetScrollBarAxis Axis,
    SpreadsheetScrollBarHitKind Kind,
    SpreadsheetSplitScrollBar ScrollBar,
    PointD Point)
{
    public bool IsHit => Kind != SpreadsheetScrollBarHitKind.None;
}

public sealed class SpreadsheetSplitScrollBarLayout
{
    private readonly SpreadsheetSplitScrollBar[] _scrollBars;

    internal SpreadsheetSplitScrollBarLayout(
        SizeD viewportSize,
        SpreadsheetSplitScrollBarStyle style,
        SpreadsheetSplitScrollBar[] scrollBars)
    {
        ViewportSize = viewportSize;
        Style = style;
        _scrollBars = scrollBars;
    }

    public SizeD ViewportSize { get; }

    public SpreadsheetSplitScrollBarStyle Style { get; }

    public IReadOnlyList<SpreadsheetSplitScrollBar> ScrollBars => _scrollBars;

    public int Count => _scrollBars.Length;

    public bool TryGet(
        SpreadsheetPaneId paneId,
        SpreadsheetScrollBarAxis axis,
        out SpreadsheetSplitScrollBar scrollBar)
    {
        foreach (var candidate in _scrollBars)
        {
            if (candidate.PaneId == paneId && candidate.Axis == axis)
            {
                scrollBar = candidate;
                return true;
            }
        }

        scrollBar = default;
        return false;
    }

    public SpreadsheetSplitScrollBarHit HitTest(PointD point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            return default;
        }

        var hitSlop = Style.HitSlop;
        foreach (var scrollBar in _scrollBars)
        {
            if (Contains(Inflate(scrollBar.ThumbBounds, hitSlop), point))
            {
                return new SpreadsheetSplitScrollBarHit(
                    scrollBar.PaneId,
                    scrollBar.Axis,
                    SpreadsheetScrollBarHitKind.Thumb,
                    scrollBar,
                    point);
            }
        }

        foreach (var scrollBar in _scrollBars)
        {
            if (!Contains(Inflate(scrollBar.TrackBounds, hitSlop), point))
            {
                continue;
            }

            var coordinate = scrollBar.Axis == SpreadsheetScrollBarAxis.Horizontal
                ? point.X
                : point.Y;
            var kind = coordinate < scrollBar.ThumbStart
                ? SpreadsheetScrollBarHitKind.TrackBeforeThumb
                : SpreadsheetScrollBarHitKind.TrackAfterThumb;
            return new SpreadsheetSplitScrollBarHit(
                scrollBar.PaneId,
                scrollBar.Axis,
                kind,
                scrollBar,
                point);
        }

        return default;
    }

    private static bool Contains(RectD bounds, PointD point) =>
        !bounds.IsEmpty &&
        point.X >= bounds.Left && point.X < bounds.Right &&
        point.Y >= bounds.Top && point.Y < bounds.Bottom;

    private static RectD Inflate(RectD bounds, double amount)
    {
        if (bounds.IsEmpty || amount <= 0d)
        {
            return bounds;
        }

        return new RectD(
            bounds.X - amount,
            bounds.Y - amount,
            bounds.Width + (amount * 2d),
            bounds.Height + (amount * 2d));
    }
}

public static class SpreadsheetSplitScrollBarGeometry
{
    private const double GeometryEpsilon = 1e-9;

    public static SpreadsheetSplitScrollBarLayout Create(
        SizeD viewportSize,
        IReadOnlyList<SpreadsheetSplitPaneScrollBarState> panes,
        SpreadsheetSplitScrollBarStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(panes);
        style ??= new SpreadsheetSplitScrollBarStyle();
        Validate(viewportSize, panes, style);
        if (!style.IsVisible)
        {
            return new SpreadsheetSplitScrollBarLayout(viewportSize, style, []);
        }

        var result = new List<SpreadsheetSplitScrollBar>(panes.Count * 2);
        foreach (var pane in panes)
        {
            var maximumX = Math.Max(0d, pane.ContentWidth - pane.PaneBounds.Width);
            var maximumY = Math.Max(0d, pane.ContentHeight - pane.PaneBounds.Height);
            var hasHorizontal = maximumX > GeometryEpsilon;
            var hasVertical = maximumY > GeometryEpsilon;

            var reservedRight = hasVertical
                ? style.Thickness + style.Margin
                : 0d;
            var reservedBottom = hasHorizontal
                ? style.Thickness + style.Margin
                : 0d;

            if (hasHorizontal)
            {
                var track = new RectD(
                    pane.PaneBounds.Left + style.Margin,
                    pane.PaneBounds.Bottom - style.Margin - style.Thickness,
                    Math.Max(
                        0d,
                        pane.PaneBounds.Width -
                        (style.Margin * 2d) -
                        reservedRight),
                    style.Thickness);
                if (track.Width >= style.MinimumTrackLength)
                {
                    result.Add(CreateBar(
                        pane.PaneId,
                        SpreadsheetScrollBarAxis.Horizontal,
                        track,
                        pane.OffsetX,
                        maximumX,
                        pane.PaneBounds.Width,
                        pane.ContentWidth,
                        style.MinimumThumbLength));
                }
            }

            if (hasVertical)
            {
                var track = new RectD(
                    pane.PaneBounds.Right - style.Margin - style.Thickness,
                    pane.PaneBounds.Top + style.Margin,
                    style.Thickness,
                    Math.Max(
                        0d,
                        pane.PaneBounds.Height -
                        (style.Margin * 2d) -
                        reservedBottom));
                if (track.Height >= style.MinimumTrackLength)
                {
                    result.Add(CreateBar(
                        pane.PaneId,
                        SpreadsheetScrollBarAxis.Vertical,
                        track,
                        pane.OffsetY,
                        maximumY,
                        pane.PaneBounds.Height,
                        pane.ContentHeight,
                        style.MinimumThumbLength));
                }
            }
        }

        return new SpreadsheetSplitScrollBarLayout(
            viewportSize,
            style,
            [.. result]);
    }

    public static double GetOffsetFromThumb(
        SpreadsheetSplitScrollBar scrollBar,
        double pointerCoordinate,
        double grabOffset)
    {
        ValidateFinite(pointerCoordinate, nameof(pointerCoordinate));
        ValidateFinite(grabOffset, nameof(grabOffset));
        if (scrollBar.MaximumOffset <= 0d)
        {
            return 0d;
        }

        var travel = Math.Max(0d, scrollBar.TrackLength - scrollBar.ThumbLength);
        if (travel <= GeometryEpsilon)
        {
            return 0d;
        }

        var thumbStart = Math.Clamp(
            pointerCoordinate - grabOffset,
            scrollBar.TrackStart,
            scrollBar.TrackStart + travel);
        var ratio = (thumbStart - scrollBar.TrackStart) / travel;
        return Math.Clamp(
            ratio * scrollBar.MaximumOffset,
            0d,
            scrollBar.MaximumOffset);
    }

    public static double GetPagedOffset(
        SpreadsheetSplitScrollBarHit hit,
        double pageFactor)
    {
        if (!double.IsFinite(pageFactor) || pageFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(pageFactor));
        }
        if (!hit.IsHit || hit.Kind == SpreadsheetScrollBarHitKind.Thumb)
        {
            return hit.ScrollBar.Offset;
        }

        var delta = hit.ScrollBar.ViewportExtent * pageFactor;
        var next = hit.Kind == SpreadsheetScrollBarHitKind.TrackBeforeThumb
            ? hit.ScrollBar.Offset - delta
            : hit.ScrollBar.Offset + delta;
        return Math.Clamp(next, 0d, hit.ScrollBar.MaximumOffset);
    }

    private static SpreadsheetSplitScrollBar CreateBar(
        SpreadsheetPaneId paneId,
        SpreadsheetScrollBarAxis axis,
        RectD track,
        double requestedOffset,
        double maximumOffset,
        double viewportExtent,
        double contentExtent,
        double minimumThumbLength)
    {
        var offset = Math.Clamp(requestedOffset, 0d, maximumOffset);
        var trackLength = axis == SpreadsheetScrollBarAxis.Horizontal
            ? track.Width
            : track.Height;
        var proportionalLength = trackLength *
            Math.Clamp(viewportExtent / contentExtent, 0d, 1d);
        var thumbLength = Math.Clamp(
            Math.Max(minimumThumbLength, proportionalLength),
            0d,
            trackLength);
        var travel = Math.Max(0d, trackLength - thumbLength);
        var ratio = maximumOffset <= GeometryEpsilon
            ? 0d
            : offset / maximumOffset;
        var thumbStart = (axis == SpreadsheetScrollBarAxis.Horizontal
            ? track.Left
            : track.Top) + (travel * ratio);
        var thumb = axis == SpreadsheetScrollBarAxis.Horizontal
            ? new RectD(thumbStart, track.Top, thumbLength, track.Height)
            : new RectD(track.Left, thumbStart, track.Width, thumbLength);

        return new SpreadsheetSplitScrollBar(
            paneId,
            axis,
            track,
            thumb,
            offset,
            maximumOffset,
            viewportExtent,
            contentExtent);
    }

    private static void Validate(
        SizeD viewportSize,
        IReadOnlyList<SpreadsheetSplitPaneScrollBarState> panes,
        SpreadsheetSplitScrollBarStyle style)
    {
        if (!double.IsFinite(viewportSize.Width) || viewportSize.Width < 0d ||
            !double.IsFinite(viewportSize.Height) || viewportSize.Height < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportSize));
        }

        ValidatePositive(style.Thickness, nameof(style.Thickness));
        ValidateNonNegative(style.Margin, nameof(style.Margin));
        ValidatePositive(style.MinimumThumbLength, nameof(style.MinimumThumbLength));
        ValidatePositive(style.MinimumTrackLength, nameof(style.MinimumTrackLength));
        ValidateNonNegative(style.HitSlop, nameof(style.HitSlop));
        ValidatePositive(style.PageFactor, nameof(style.PageFactor));

        var paneIds = new HashSet<SpreadsheetPaneId>();
        foreach (var pane in panes)
        {
            if (!Enum.IsDefined(pane.PaneId))
            {
                throw new ArgumentOutOfRangeException(nameof(panes));
            }
            if (!paneIds.Add(pane.PaneId))
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' appears more than once.",
                    nameof(panes));
            }
            ValidateBounds(pane.PaneBounds, nameof(panes));
            ValidateNonNegative(pane.OffsetX, nameof(panes));
            ValidateNonNegative(pane.OffsetY, nameof(panes));
            ValidateNonNegative(pane.ContentWidth, nameof(panes));
            ValidateNonNegative(pane.ContentHeight, nameof(panes));
        }
    }

    private static void ValidateBounds(RectD bounds, string parameterName)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
            !double.IsFinite(bounds.Width) || bounds.Width < 0d ||
            !double.IsFinite(bounds.Height) || bounds.Height < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public static class SpreadsheetSplitScrollBarDisplayListComposer
{
    public static DisplayList Compose(
        DisplayList body,
        SpreadsheetSplitScrollBarLayout layout,
        SpreadsheetPaneId activePane)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Count == 0)
        {
            return body;
        }

        var style = layout.Style;
        var builder = new DisplayListBuilder();
        builder.Append(body);
        builder.PushClip(new RectD(
            0d,
            0d,
            layout.ViewportSize.Width,
            layout.ViewportSize.Height));
        foreach (var scrollBar in layout.ScrollBars)
        {
            builder.FillRectangle(scrollBar.TrackBounds, style.TrackColor);
            builder.FillRectangle(
                scrollBar.ThumbBounds,
                scrollBar.PaneId == activePane
                    ? style.ActiveThumbColor
                    : style.ThumbColor);
            DrawBorder(builder, scrollBar.ThumbBounds, style.BorderColor);
        }
        builder.PopClip();
        return builder.Build();
    }

    private static void DrawBorder(
        DisplayListBuilder builder,
        RectD bounds,
        ColorRgba color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Right, bounds.Top),
            1d,
            color);
        builder.DrawLine(
            new PointD(bounds.Right - 1d, bounds.Top),
            new PointD(bounds.Right - 1d, bounds.Bottom),
            1d,
            color);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Bottom - 1d),
            new PointD(bounds.Right, bounds.Bottom - 1d),
            1d,
            color);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Left, bounds.Bottom),
            1d,
            color);
    }
}
