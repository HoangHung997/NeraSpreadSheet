using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

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
    private readonly Dictionary<SpreadsheetPaneId, PointD> _scrollOffsets = [];
    private SpreadsheetSplitViewportFrame? _lastFrame;

    public SpreadsheetSplitViewportEngine(
        SpreadsheetSession session,
        SpreadsheetViewportCacheOptions? cacheOptions = null)
    {
        _viewport = new SpreadsheetViewportEngine(
            session ?? throw new ArgumentNullException(nameof(session)),
            cacheOptions);
    }

    public SpreadsheetSession Session => _viewport.Session;
    public SpreadsheetPaneId ActivePane { get; private set; } = SpreadsheetPaneId.TopLeft;
    public SpreadsheetSplitViewportFrame? LastFrame => _lastFrame;

    public PointD GetPaneScroll(SpreadsheetPaneId paneId) =>
        _scrollOffsets.GetValueOrDefault(paneId);

    public void SetPaneScroll(SpreadsheetPaneId paneId, double scrollX, double scrollY)
    {
        ValidatePaneId(paneId);
        if (!double.IsFinite(scrollX) || scrollX < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(scrollX));
        }
        if (!double.IsFinite(scrollY) || scrollY < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(scrollY));
        }

        _scrollOffsets[paneId] = new PointD(scrollX, scrollY);
    }

    public void SetActivePane(SpreadsheetPaneId paneId)
    {
        ValidatePaneId(paneId);
        ActivePane = paneId;
    }

    public SpreadsheetSplitViewportFrame Compose(
        SpreadsheetSplitRequest request,
        double overscan = 128d,
        SpreadsheetRenderTheme? theme = null)
    {
        theme ??= new SpreadsheetRenderTheme();
        ValidateTheme(theme);
        var splitLayout = SpreadsheetSplitLayoutEngine.Compute(request);
        if (!splitLayout.TryGetPane(ActivePane, out _))
        {
            ActivePane = SpreadsheetPaneId.TopLeft;
        }

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
            var requestedScroll = GetPaneScroll(pane.PaneId);
            var viewportFrame = _viewport.Compose(
                requestedScroll.X,
                requestedScroll.Y,
                pane.Bounds.Width,
                pane.Bounds.Height,
                overscan,
                theme);
            _scrollOffsets[pane.PaneId] = new PointD(
                viewportFrame.Layout.ScrollX,
                viewportFrame.Layout.ScrollY);
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
        _lastFrame = new SpreadsheetSplitViewportFrame(
            splitLayout,
            panes,
            ActivePane,
            builder.Build());
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

        ActivePane = paneId;
        return true;
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

    private static void ValidatePaneId(SpreadsheetPaneId paneId)
    {
        if (!Enum.IsDefined(paneId))
        {
            throw new ArgumentOutOfRangeException(nameof(paneId));
        }
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
