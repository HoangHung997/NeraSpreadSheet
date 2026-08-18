using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Viewport;

public sealed record SpreadsheetSplitPaneFrame(
    SpreadsheetPaneLayout Pane,
    SpreadsheetViewportFrame ViewportFrame)
{
    public double ScrollX => ViewportFrame.Layout.ScrollX;
    public double ScrollY => ViewportFrame.Layout.ScrollY;
}

public sealed record SpreadsheetSplitViewportFrame(
    SpreadsheetSplitLayout Layout,
    IReadOnlyList<SpreadsheetSplitPaneFrame> Panes,
    SpreadsheetPaneId ActivePane,
    SpreadsheetPaneScrollBarSet ScrollBars,
    DisplayList DisplayList)
{
    public bool TryGetPane(SpreadsheetPaneId paneId, out SpreadsheetSplitPaneFrame pane)
    {
        foreach (var candidate in Panes)
        {
            if (candidate.Pane.PaneId == paneId)
            {
                pane = candidate;
                return true;
            }
        }

        pane = null!;
        return false;
    }
}

public sealed class SpreadsheetSplitViewportEngine
{
    private readonly SpreadsheetViewportEngine _viewport;
    private SpreadsheetSplitViewportFrame? _lastFrame;

    public SpreadsheetSplitViewportEngine(
        SpreadsheetSession session,
        SpreadsheetViewportCacheOptions? cacheOptions = null,
        ScrollPhysicsOptions? scrollPhysicsOptions = null)
    {
        _viewport = new SpreadsheetViewportEngine(
            session ?? throw new ArgumentNullException(nameof(session)),
            cacheOptions);
        Scroll = new SpreadsheetSplitScrollController(scrollPhysicsOptions);
    }

    public SpreadsheetSession Session => _viewport.Session;
    public SpreadsheetSplitScrollController Scroll { get; }
    public SpreadsheetPaneId ActivePane => Scroll.ActivePane;
    public SpreadsheetSplitViewportFrame? LastFrame => _lastFrame;
    public bool HasPendingScroll => Scroll.HasPendingMotion;

    public PointD GetPaneScroll(SpreadsheetPaneId paneId) => Scroll.GetOffset(paneId);

    public ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) => Scroll.GetSnapshot(paneId);

    public void SetPaneScroll(SpreadsheetPaneId paneId, double scrollX, double scrollY) =>
        Scroll.ScrollTo(paneId, scrollX, scrollY, animated: false);

    public void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double scrollX,
        double scrollY,
        bool animated = false) =>
        Scroll.ScrollTo(paneId, scrollX, scrollY, animated);

    public void QueuePaneScroll(SpreadsheetPaneId paneId, ScrollDelta delta) =>
        Scroll.QueueDelta(paneId, delta);

    public void QueueActivePaneScroll(ScrollDelta delta) => Scroll.QueueActivePaneDelta(delta);

    public void SetActivePane(SpreadsheetPaneId paneId) => Scroll.SetActivePane(paneId);

    public bool AdvanceScrollFrame(TimeSpan elapsed)
    {
        if (_lastFrame is null)
        {
            return false;
        }

        return Scroll.AdvanceFrame(
            elapsed,
            _lastFrame.Layout,
            _viewport.GetContentExtent()).Changed;
    }

    public SpreadsheetSplitViewportFrame Compose(
        SpreadsheetSplitRequest request,
        double overscan = 128d,
        SpreadsheetRenderTheme? theme = null)
    {
        theme ??= new SpreadsheetRenderTheme();
        ValidateTheme(theme);
        var splitLayout = SpreadsheetSplitLayoutEngine.Compute(request);
        var contentExtent = _viewport.GetContentExtent();
        Scroll.AdvanceFrame(TimeSpan.Zero, splitLayout, contentExtent);

        var panes = new List<SpreadsheetSplitPaneFrame>(splitLayout.Panes.Count);
        var builder = new DisplayListBuilder();
        var fullBounds = new RectD(
            0d,
            0d,
            request.ViewportSize.Width,
            request.ViewportSize.Height);
        builder.FillRectangle(fullBounds, theme.Background);
        builder.PushClip(fullBounds);

        foreach (var pane in splitLayout.Panes)
        {
            var requestedScroll = Scroll.GetSnapshot(pane.PaneId);
            var viewportFrame = _viewport.Compose(
                requestedScroll.OffsetX,
                requestedScroll.OffsetY,
                pane.Bounds.Width,
                pane.Bounds.Height,
                overscan,
                theme);
            if (viewportFrame.Layout.ScrollX != requestedScroll.OffsetX ||
                viewportFrame.Layout.ScrollY != requestedScroll.OffsetY)
            {
                Scroll.ScrollTo(
                    pane.PaneId,
                    viewportFrame.Layout.ScrollX,
                    viewportFrame.Layout.ScrollY,
                    animated: false);
            }
            panes.Add(new SpreadsheetSplitPaneFrame(pane, viewportFrame));

            builder.PushClip(pane.Bounds);
            builder.PushTranslation(pane.Bounds.X, pane.Bounds.Y);
            builder.DrawDisplayList(viewportFrame.DisplayList);
            builder.PopTranslation();
            builder.PopClip();
        }

        if (splitLayout.HasVerticalSplit)
        {
            builder.FillRectangle(splitLayout.VerticalSeparator, theme.SplitPaneSeparator);
        }
        if (splitLayout.HasHorizontalSplit)
        {
            builder.FillRectangle(splitLayout.HorizontalSeparator, theme.SplitPaneSeparator);
        }
        if (splitLayout.HasSplitPanes && splitLayout.TryGetPane(ActivePane, out var activePane))
        {
            DrawPaneBorder(builder, activePane.Bounds, theme);
        }

        builder.PopClip();
        var bodyDisplayList = builder.Build();
        var scrollBarStates = new SpreadsheetPaneScrollBarState[panes.Count];
        for (var index = 0; index < panes.Count; index++)
        {
            var pane = panes[index];
            scrollBarStates[index] = new SpreadsheetPaneScrollBarState(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ScrollX,
                pane.ScrollY,
                pane.ViewportFrame.Layout.ContentWidth,
                pane.ViewportFrame.Layout.ContentHeight);
        }
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            splitLayout,
            scrollBarStates,
            theme);
        var displayList = SpreadsheetPaneScrollBarDisplayListComposer.Compose(
            bodyDisplayList,
            scrollBars,
            ActivePane,
            theme);
        _lastFrame = new SpreadsheetSplitViewportFrame(
            splitLayout,
            panes,
            ActivePane,
            scrollBars,
            displayList);
        return _lastFrame;
    }

    public bool TryHitTest(
        double viewportX,
        double viewportY,
        out SpreadsheetPaneId paneId,
        out CellAddress address)
    {
        if (_lastFrame is null || !double.IsFinite(viewportX) || !double.IsFinite(viewportY))
        {
            paneId = default;
            address = default;
            return false;
        }

        var hit = _lastFrame.Layout.HitTest(new PointD(viewportX, viewportY));
        if (hit is not
            {
                RegionKind: SpreadsheetSplitHitRegionKind.Pane,
                PaneId: { } resolvedPane,
            } ||
            !_lastFrame.TryGetPane(resolvedPane, out var paneFrame) ||
            !_viewport.TryHitTest(
                hit.LocalPoint.X,
                hit.LocalPoint.Y,
                paneFrame.ScrollX,
                paneFrame.ScrollY,
                out address))
        {
            paneId = default;
            address = default;
            return false;
        }

        paneId = resolvedPane;
        return true;
    }

    public bool TryHitScrollBar(
        double viewportX,
        double viewportY,
        out SpreadsheetPaneScrollBarHit hit)
    {
        if (_lastFrame is null ||
            !double.IsFinite(viewportX) ||
            !double.IsFinite(viewportY))
        {
            hit = SpreadsheetPaneScrollBarHit.None;
            return false;
        }

        return _lastFrame.ScrollBars.TryHitTest(
            new PointD(viewportX, viewportY),
            out hit);
    }

    public bool TryActivatePaneAt(double viewportX, double viewportY)
    {
        if (_lastFrame is null || !double.IsFinite(viewportX) || !double.IsFinite(viewportY))
        {
            return false;
        }

        var hit = _lastFrame.Layout.HitTest(new PointD(viewportX, viewportY));
        if (hit is not
            {
                RegionKind: SpreadsheetSplitHitRegionKind.Pane,
                PaneId: { } paneId,
            })
        {
            return false;
        }

        return Scroll.SetActivePane(paneId);
    }

    public bool TryGetCellBounds(
        SpreadsheetPaneId paneId,
        CellAddress address,
        out RectD bounds)
    {
        if (_lastFrame is null ||
            !_lastFrame.TryGetPane(paneId, out var paneFrame) ||
            !_viewport.TryGetCellBounds(
                address,
                paneFrame.ScrollX,
                paneFrame.ScrollY,
                out var localBounds))
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = localBounds.Translate(
            paneFrame.Pane.Bounds.X,
            paneFrame.Pane.Bounds.Y);
        return true;
    }

    public SizeD GetContentExtent() => _viewport.GetContentExtent();

    public void ResetPaneScrolls()
    {
        Scroll.Reset();
        _lastFrame = null;
    }

    public void InvalidateMetrics()
    {
        _viewport.InvalidateMetrics();
        _lastFrame = null;
    }

    public void InvalidateSnapshot()
    {
        _viewport.InvalidateSnapshot();
        _lastFrame = null;
    }

    public void ClearDisplayListCache()
    {
        _viewport.ClearDisplayListCache();
        _lastFrame = null;
    }

    private static void DrawPaneBorder(
        DisplayListBuilder builder,
        RectD bounds,
        SpreadsheetRenderTheme theme)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var right = Math.Max(bounds.Left, bounds.Right - theme.ActivePaneStrokeWidth);
        var bottom = Math.Max(bounds.Top, bounds.Bottom - theme.ActivePaneStrokeWidth);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Right, bounds.Top),
            theme.ActivePaneStrokeWidth,
            theme.ActivePaneBorder);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top),
            new PointD(bounds.Left, bounds.Bottom),
            theme.ActivePaneStrokeWidth,
            theme.ActivePaneBorder);
        builder.DrawLine(
            new PointD(right, bounds.Top),
            new PointD(right, bounds.Bottom),
            theme.ActivePaneStrokeWidth,
            theme.ActivePaneBorder);
        builder.DrawLine(
            new PointD(bounds.Left, bottom),
            new PointD(bounds.Right, bottom),
            theme.ActivePaneStrokeWidth,
            theme.ActivePaneBorder);
    }

    private static void ValidateTheme(SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!double.IsFinite(theme.ActivePaneStrokeWidth) || theme.ActivePaneStrokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "ActivePaneStrokeWidth must be finite and positive.");
        }
    }
}
