using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public enum SpreadsheetScrollBarOrientation
{
    Horizontal,
    Vertical,
}

public enum SpreadsheetScrollBarPart
{
    None,
    DecreaseButton,
    TrackBeforeThumb,
    Thumb,
    TrackAfterThumb,
    IncreaseButton,
}

public readonly record struct SpreadsheetPaneScrollBarState(
    SpreadsheetPaneId PaneId,
    RectD PaneBounds,
    double OffsetX,
    double OffsetY,
    double ContentWidth,
    double ContentHeight);

public readonly record struct SpreadsheetPaneScrollBarHit(
    SpreadsheetPaneId PaneId,
    SpreadsheetScrollBarOrientation Orientation,
    SpreadsheetScrollBarPart Part,
    PointD Point)
{
    public static SpreadsheetPaneScrollBarHit None => new(
        default,
        default,
        SpreadsheetScrollBarPart.None,
        default);
}

public readonly record struct SpreadsheetPaneScrollBarLayout(
    SpreadsheetPaneId PaneId,
    SpreadsheetScrollBarOrientation Orientation,
    RectD Bounds,
    RectD DecreaseButtonBounds,
    RectD TrackBounds,
    RectD ThumbBounds,
    RectD IncreaseButtonBounds,
    double Offset,
    double MaximumOffset,
    double ViewportExtent,
    double ContentExtent)
{
    public bool IsScrollable => MaximumOffset > 0d;

    public double TrackTravel => Math.Max(
        0d,
        GetExtent(TrackBounds) - GetExtent(ThumbBounds));

    public double GetOffsetForThumbStart(double thumbStart)
    {
        if (!double.IsFinite(thumbStart))
        {
            throw new ArgumentOutOfRangeException(nameof(thumbStart));
        }
        if (!IsScrollable || TrackTravel <= 0d)
        {
            return 0d;
        }

        var trackStart = GetStart(TrackBounds);
        var normalized = Math.Clamp(
            (thumbStart - trackStart) / TrackTravel,
            0d,
            1d);
        return normalized * MaximumOffset;
    }

    public double GetPageOffset(bool increase, double pageFactor)
    {
        if (!double.IsFinite(pageFactor) || pageFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(pageFactor));
        }

        var delta = ViewportExtent * pageFactor * (increase ? 1d : -1d);
        return Math.Clamp(Offset + delta, 0d, MaximumOffset);
    }

    public double GetLineOffset(bool increase, double lineStep)
    {
        if (!double.IsFinite(lineStep) || lineStep <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(lineStep));
        }

        var delta = lineStep * (increase ? 1d : -1d);
        return Math.Clamp(Offset + delta, 0d, MaximumOffset);
    }

    public SpreadsheetPaneScrollBarPart HitTest(PointD point)
    {
        if (!ContainsHalfOpen(Bounds, point))
        {
            return SpreadsheetScrollBarPart.None;
        }
        if (ContainsHalfOpen(ThumbBounds, point))
        {
            return SpreadsheetScrollBarPart.Thumb;
        }
        if (ContainsHalfOpen(DecreaseButtonBounds, point))
        {
            return SpreadsheetScrollBarPart.DecreaseButton;
        }
        if (ContainsHalfOpen(IncreaseButtonBounds, point))
        {
            return SpreadsheetScrollBarPart.IncreaseButton;
        }
        if (!ContainsHalfOpen(TrackBounds, point))
        {
            return SpreadsheetScrollBarPart.None;
        }

        var coordinate = Orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? point.X
            : point.Y;
        return coordinate < GetStart(ThumbBounds)
            ? SpreadsheetScrollBarPart.TrackBeforeThumb
            : SpreadsheetScrollBarPart.TrackAfterThumb;
    }

    private double GetStart(RectD bounds) =>
        Orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? bounds.Left
            : bounds.Top;

    private double GetExtent(RectD bounds) =>
        Orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? bounds.Width
            : bounds.Height;

    private static bool ContainsHalfOpen(RectD bounds, PointD point) =>
        !bounds.IsEmpty &&
        point.X >= bounds.Left &&
        point.Y >= bounds.Top &&
        point.X < bounds.Right &&
        point.Y < bounds.Bottom;
}

public sealed record SpreadsheetPaneScrollBarSet(
    IReadOnlyList<SpreadsheetPaneScrollBarLayout> Bars,
    IReadOnlyList<RectD> Corners)
{
    public static SpreadsheetPaneScrollBarSet Empty { get; } = new([], []);

    public bool TryHitTest(PointD point, out SpreadsheetPaneScrollBarHit hit)
    {
        foreach (var bar in Bars)
        {
            var part = bar.HitTest(point);
            if (part == SpreadsheetScrollBarPart.None)
            {
                continue;
            }

            hit = new SpreadsheetPaneScrollBarHit(
                bar.PaneId,
                bar.Orientation,
                part,
                point);
            return true;
        }

        hit = SpreadsheetPaneScrollBarHit.None;
        return false;
    }

    public bool TryGetBar(
        SpreadsheetPaneId paneId,
        SpreadsheetScrollBarOrientation orientation,
        out SpreadsheetPaneScrollBarLayout layout)
    {
        foreach (var candidate in Bars)
        {
            if (candidate.PaneId == paneId &&
                candidate.Orientation == orientation)
            {
                layout = candidate;
                return true;
            }
        }

        layout = default;
        return false;
    }
}

public static class SpreadsheetPaneScrollBarLayoutEngine
{
    private const double GeometryEpsilon = 1e-9;

    public static SpreadsheetPaneScrollBarSet Compute(
        SpreadsheetSplitLayout splitLayout,
        IReadOnlyList<SpreadsheetPaneScrollBarState> paneStates,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(splitLayout);
        ArgumentNullException.ThrowIfNull(paneStates);
        ArgumentNullException.ThrowIfNull(theme);
        if (!theme.ShowSplitPaneScrollBars)
        {
            return SpreadsheetPaneScrollBarSet.Empty;
        }

        ValidateTheme(theme);
        var states = ValidateStates(splitLayout, paneStates);
        var bars = new List<SpreadsheetPaneScrollBarLayout>(
            splitLayout.Panes.Count * 2);
        var corners = new List<RectD>(splitLayout.Panes.Count);
        foreach (var pane in splitLayout.Panes)
        {
            var state = states[pane.PaneId];
            var maximumX = Math.Max(0d, state.ContentWidth - pane.Bounds.Width);
            var maximumY = Math.Max(0d, state.ContentHeight - pane.Bounds.Height);
            var horizontalVisible = CanShow(
                pane.Bounds.Width,
                maximumX,
                theme);
            var verticalVisible = CanShow(
                pane.Bounds.Height,
                maximumY,
                theme);

            if (horizontalVisible)
            {
                var length = Math.Max(
                    0d,
                    pane.Bounds.Width -
                    (verticalVisible ? theme.ScrollBarThickness : 0d));
                var bounds = new RectD(
                    pane.Bounds.Left,
                    Math.Max(
                        pane.Bounds.Top,
                        pane.Bounds.Bottom - theme.ScrollBarThickness),
                    length,
                    Math.Min(theme.ScrollBarThickness, pane.Bounds.Height));
                bars.Add(CreateBar(
                    pane.PaneId,
                    SpreadsheetScrollBarOrientation.Horizontal,
                    bounds,
                    state.OffsetX,
                    maximumX,
                    pane.Bounds.Width,
                    state.ContentWidth,
                    theme));
            }

            if (verticalVisible)
            {
                var length = Math.Max(
                    0d,
                    pane.Bounds.Height -
                    (horizontalVisible ? theme.ScrollBarThickness : 0d));
                var bounds = new RectD(
                    Math.Max(
                        pane.Bounds.Left,
                        pane.Bounds.Right - theme.ScrollBarThickness),
                    pane.Bounds.Top,
                    Math.Min(theme.ScrollBarThickness, pane.Bounds.Width),
                    length);
                bars.Add(CreateBar(
                    pane.PaneId,
                    SpreadsheetScrollBarOrientation.Vertical,
                    bounds,
                    state.OffsetY,
                    maximumY,
                    pane.Bounds.Height,
                    state.ContentHeight,
                    theme));
            }

            if (horizontalVisible && verticalVisible)
            {
                corners.Add(new RectD(
                    Math.Max(
                        pane.Bounds.Left,
                        pane.Bounds.Right - theme.ScrollBarThickness),
                    Math.Max(
                        pane.Bounds.Top,
                        pane.Bounds.Bottom - theme.ScrollBarThickness),
                    Math.Min(theme.ScrollBarThickness, pane.Bounds.Width),
                    Math.Min(theme.ScrollBarThickness, pane.Bounds.Height)));
            }
        }

        return new SpreadsheetPaneScrollBarSet(bars, corners);
    }

    private static SpreadsheetPaneScrollBarLayout CreateBar(
        SpreadsheetPaneId paneId,
        SpreadsheetScrollBarOrientation orientation,
        RectD bounds,
        double requestedOffset,
        double maximumOffset,
        double viewportExtent,
        double contentExtent,
        SpreadsheetRenderTheme theme)
    {
        var length = orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? bounds.Width
            : bounds.Height;
        var buttonExtent = Math.Min(
            theme.ScrollBarButtonExtent,
            Math.Max(0d, length / 3d));
        var trackExtent = Math.Max(0d, length - (buttonExtent * 2d));
        var thumbExtent = Math.Clamp(
            trackExtent * Math.Min(1d, viewportExtent / contentExtent),
            Math.Min(theme.ScrollBarMinimumThumbExtent, trackExtent),
            trackExtent);
        var offset = Math.Clamp(requestedOffset, 0d, maximumOffset);
        var travel = Math.Max(0d, trackExtent - thumbExtent);
        var normalized = maximumOffset <= 0d ? 0d : offset / maximumOffset;
        var thumbStart = buttonExtent + (travel * normalized);

        RectD decrease;
        RectD track;
        RectD thumb;
        RectD increase;
        if (orientation == SpreadsheetScrollBarOrientation.Horizontal)
        {
            decrease = new RectD(
                bounds.Left,
                bounds.Top,
                buttonExtent,
                bounds.Height);
            track = new RectD(
                bounds.Left + buttonExtent,
                bounds.Top,
                trackExtent,
                bounds.Height);
            thumb = new RectD(
                bounds.Left + thumbStart,
                bounds.Top,
                thumbExtent,
                bounds.Height);
            increase = new RectD(
                bounds.Right - buttonExtent,
                bounds.Top,
                buttonExtent,
                bounds.Height);
        }
        else
        {
            decrease = new RectD(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                buttonExtent);
            track = new RectD(
                bounds.Left,
                bounds.Top + buttonExtent,
                bounds.Width,
                trackExtent);
            thumb = new RectD(
                bounds.Left,
                bounds.Top + thumbStart,
                bounds.Width,
                thumbExtent);
            increase = new RectD(
                bounds.Left,
                bounds.Bottom - buttonExtent,
                bounds.Width,
                buttonExtent);
        }

        return new SpreadsheetPaneScrollBarLayout(
            paneId,
            orientation,
            bounds,
            decrease,
            track,
            thumb,
            increase,
            offset,
            maximumOffset,
            viewportExtent,
            contentExtent);
    }

    private static Dictionary<SpreadsheetPaneId, SpreadsheetPaneScrollBarState>
        ValidateStates(
            SpreadsheetSplitLayout splitLayout,
            IReadOnlyList<SpreadsheetPaneScrollBarState> paneStates)
    {
        var states = new Dictionary<SpreadsheetPaneId, SpreadsheetPaneScrollBarState>();
        foreach (var state in paneStates)
        {
            ValidateState(state);
            if (!states.TryAdd(state.PaneId, state))
            {
                throw new ArgumentException(
                    $"Pane '{state.PaneId}' has more than one scroll-bar state.",
                    nameof(paneStates));
            }
        }

        foreach (var pane in splitLayout.Panes)
        {
            if (!states.TryGetValue(pane.PaneId, out var state))
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' does not have scroll-bar state.",
                    nameof(paneStates));
            }
            if (state.PaneBounds != pane.Bounds)
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' scroll-bar bounds do not match the split layout.",
                    nameof(paneStates));
            }
        }

        if (states.Count != splitLayout.Panes.Count)
        {
            throw new ArgumentException(
                "Scroll-bar state contains panes outside the split layout.",
                nameof(paneStates));
        }
        return states;
    }

    private static void ValidateState(SpreadsheetPaneScrollBarState state)
    {
        if (!Enum.IsDefined(state.PaneId))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        if (!double.IsFinite(state.OffsetX) || state.OffsetX < 0d ||
            !double.IsFinite(state.OffsetY) || state.OffsetY < 0d ||
            !double.IsFinite(state.ContentWidth) || state.ContentWidth <= 0d ||
            !double.IsFinite(state.ContentHeight) || state.ContentHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Scroll offsets and content extents must be finite and valid.");
        }
    }

    private static bool CanShow(
        double paneExtent,
        double maximumOffset,
        SpreadsheetRenderTheme theme) =>
        maximumOffset > GeometryEpsilon &&
        paneExtent >=
            (theme.ScrollBarButtonExtent * 2d) +
            theme.ScrollBarMinimumThumbExtent;

    private static void ValidateTheme(SpreadsheetRenderTheme theme)
    {
        if (!double.IsFinite(theme.ScrollBarThickness) ||
            theme.ScrollBarThickness <= 0d ||
            !double.IsFinite(theme.ScrollBarButtonExtent) ||
            theme.ScrollBarButtonExtent <= 0d ||
            !double.IsFinite(theme.ScrollBarMinimumThumbExtent) ||
            theme.ScrollBarMinimumThumbExtent <= 0d ||
            !double.IsFinite(theme.ScrollBarLineStep) ||
            theme.ScrollBarLineStep <= 0d ||
            !double.IsFinite(theme.ScrollBarPageFactor) ||
            theme.ScrollBarPageFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                "Scroll-bar dimensions and movement settings must be finite and positive.");
        }
    }
}

public static class SpreadsheetPaneScrollBarDisplayListComposer
{
    public static DisplayList Compose(
        DisplayList body,
        SpreadsheetPaneScrollBarSet scrollBars,
        SpreadsheetPaneId activePane,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(scrollBars);
        ArgumentNullException.ThrowIfNull(theme);
        if (!theme.ShowSplitPaneScrollBars || scrollBars.Bars.Count == 0)
        {
            return body;
        }

        var builder = new DisplayListBuilder();
        builder.DrawDisplayList(body);
        foreach (var corner in scrollBars.Corners)
        {
            builder.FillRectangle(corner, theme.ScrollBarCorner);
        }
        foreach (var bar in scrollBars.Bars)
        {
            DrawBar(builder, bar, activePane, theme);
        }
        return builder.Build();
    }

    private static void DrawBar(
        DisplayListBuilder builder,
        SpreadsheetPaneScrollBarLayout bar,
        SpreadsheetPaneId activePane,
        SpreadsheetRenderTheme theme)
    {
        builder.FillRectangle(bar.Bounds, theme.ScrollBarBackground);
        builder.FillRectangle(bar.TrackBounds, theme.ScrollBarTrack);
        builder.FillRectangle(
            bar.DecreaseButtonBounds,
            theme.ScrollBarButtonBackground);
        builder.FillRectangle(
            bar.IncreaseButtonBounds,
            theme.ScrollBarButtonBackground);
        builder.FillRectangle(
            bar.ThumbBounds,
            bar.PaneId == activePane
                ? theme.ScrollBarActiveThumb
                : theme.ScrollBarThumb);
        DrawOutline(builder, bar.Bounds, theme.ScrollBarBorder, theme.ScrollBarStrokeWidth);
        DrawOutline(builder, bar.ThumbBounds, theme.ScrollBarBorder, theme.ScrollBarStrokeWidth);
        DrawArrow(builder, bar, decrease: true, theme);
        DrawArrow(builder, bar, decrease: false, theme);
    }

    private static void DrawOutline(
        DisplayListBuilder builder,
        RectD bounds,
        ColorRgba color,
        double strokeWidth)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var right = Math.Max(bounds.Left, bounds.Right - strokeWidth);
        var bottom = Math.Max(bounds.Top, bounds.Bottom - strokeWidth);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Right, bounds.Top),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Left, bounds.Bottom),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(right, bounds.Top),
            new PointD(right, bounds.Bottom),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(bounds.Left, bottom),
            new PointD(bounds.Right, bottom),
            strokeWidth,
            color);
    }

    private static void DrawArrow(
        DisplayListBuilder builder,
        SpreadsheetPaneScrollBarLayout bar,
        bool decrease,
        SpreadsheetRenderTheme theme)
    {
        var bounds = decrease
            ? bar.DecreaseButtonBounds
            : bar.IncreaseButtonBounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        var centerX = bounds.Left + (bounds.Width / 2d);
        var centerY = bounds.Top + (bounds.Height / 2d);
        var radius = Math.Max(
            1d,
            Math.Min(bounds.Width, bounds.Height) * 0.22d);
        if (bar.Orientation == SpreadsheetScrollBarOrientation.Horizontal)
        {
            var tipX = centerX + (decrease ? -radius : radius);
            var baseX = centerX + (decrease ? radius : -radius);
            builder.DrawLine(
                new PointD(baseX, centerY - radius),
                new PointD(tipX, centerY),
                theme.ScrollBarStrokeWidth,
                theme.ScrollBarGlyph);
            builder.DrawLine(
                new PointD(tipX, centerY),
                new PointD(baseX, centerY + radius),
                theme.ScrollBarStrokeWidth,
                theme.ScrollBarGlyph);
        }
        else
        {
            var tipY = centerY + (decrease ? -radius : radius);
            var baseY = centerY + (decrease ? radius : -radius);
            builder.DrawLine(
                new PointD(centerX - radius, baseY),
                new PointD(centerX, tipY),
                theme.ScrollBarStrokeWidth,
                theme.ScrollBarGlyph);
            builder.DrawLine(
                new PointD(centerX, tipY),
                new PointD(centerX + radius, baseY),
                theme.ScrollBarStrokeWidth,
                theme.ScrollBarGlyph);
        }
    }
}
