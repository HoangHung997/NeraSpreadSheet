using System.Diagnostics;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// GPU-backed MAUI spreadsheet surface. A single native SKGLView consumes the same
/// workbook/viewport/display-list model as the desktop hosts; no control is created per cell.
/// </summary>
public sealed class NeraSpreadsheetView : SKGLView, IDisposable
{
    public const double MinimumZoom = 0.25d;
    public const double MaximumZoom = 4d;
    public const string NativeRenderingSurface = "SKGLView/PlatformHandlerOwned";

    public static readonly BindableProperty WorkbookProperty = BindableProperty.Create(
        nameof(Workbook),
        typeof(Workbook),
        typeof(NeraSpreadsheetView),
        default(Workbook),
        propertyChanged: OnWorkbookChanged);

    private readonly ContinuousScrollController _scroll = new();
    private readonly SkiaDisplayListRenderer _renderer = new();
    private readonly NeraGpuContextLifecycle _gpuLifecycle = new();
    private readonly NeraSpreadsheetInputController _input;
    private readonly NeraSpreadsheetAnalyticsTouchRouter _analyticsTouch;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetAnalyticsViewportInteractionController? _analyticsInput;
    private Worksheet? _subscribedWorksheet;
    private ViewportLayout? _lastLayout;
    private SpreadsheetRenderTheme _renderTheme = new() { ShowHeaders = true };
    private double _lastSurfaceWidth;
    private double _lastSurfaceHeight;
    private double _lastBodyWidth;
    private double _lastBodyHeight;
    private double _zoom = 1d;
    private long _lastPaintTimestamp = Stopwatch.GetTimestamp();
    private bool _disposed;

    public NeraSpreadsheetView()
    {
        _input = new NeraSpreadsheetInputController(
            CaptureInputState,
            GetInputChrome,
            () => WheelPixelsPerNotch,
            (offsetX, offsetY) => ScrollTo(offsetX, offsetY, animated: false),
            ApplyInputZoom,
            QueueInputWheel,
            SelectAt,
            MinimumZoom,
            MaximumZoom);
        _analyticsTouch = new NeraSpreadsheetAnalyticsTouchRouter(
            () => _analyticsInput,
            () => _lastLayout,
            _input.CancelAll,
            InvalidateSurface);
        IgnorePixelScaling = true;
        EnableTouchEvents = true;
        HasRenderLoop = false;
        NeraSpreadsheetAnalyticsAccessibilityBridge.Attach(this);
    }

    public Workbook? Workbook
    {
        get => (Workbook?)GetValue(WorkbookProperty);
        set => SetValue(WorkbookProperty, value);
    }

    public SpreadsheetSession? Session => _session;

    public ScrollSnapshot ScrollSnapshot => _scroll.Snapshot;

    public double Zoom
    {
        get => _zoom;
        set => ZoomTo(value, _lastSurfaceWidth / 2d, _lastSurfaceHeight / 2d);
    }

    public double OverscanPixels { get; set; } = 128d;

    public double WheelPixelsPerNotch { get; set; } = 96d;

    public SpreadsheetRenderTheme RenderTheme
    {
        get => _renderTheme;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _renderTheme = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateSurface();
        }
    }

    public int CachedTypefaceCount => _renderer.CachedTypefaceCount;

    public NeraGpuContextDiagnostics GpuContextDiagnostics =>
        _gpuLifecycle.Diagnostics;

    public NeraSpreadsheetInputDiagnostics InputDiagnostics =>
        _input.Diagnostics;

    public IReadOnlyList<SpreadsheetAnalyticsAccessibleNode>
        AnalyticsAccessibilityNodes => GetAnalyticsAccessibilityNodes();

    public event EventHandler? ZoomChanged;

    public event EventHandler? ScrollChanged;

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY))
        {
            throw new ArgumentOutOfRangeException(nameof(offsetX), "Scroll offsets must be finite.");
        }

        var bounds = GetScrollBounds();
        _scroll.ScrollTo(
            Math.Clamp(offsetX, 0d, bounds.MaximumX),
            Math.Clamp(offsetY, 0d, bounds.MaximumY),
            animated);
        HasRenderLoop = animated && _scroll.HasPendingMotion;
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public void ZoomTo(double zoom, double screenAnchorX, double screenAnchorY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(zoom))
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }
        if (!double.IsFinite(screenAnchorX) || !double.IsFinite(screenAnchorY))
        {
            throw new ArgumentOutOfRangeException(nameof(screenAnchorX), "Zoom anchor must be finite.");
        }

        var nextZoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (Math.Abs(nextZoom - _zoom) <= 1e-9)
        {
            return;
        }

        var before = _scroll.Snapshot;
        var oldChrome = GetChromeMetrics(_zoom);
        var documentX = before.OffsetX + Math.Max(0d, (screenAnchorX / _zoom) - oldChrome.RowHeaderWidth);
        var documentY = before.OffsetY + Math.Max(0d, (screenAnchorY / _zoom) - oldChrome.ColumnHeaderHeight);
        _zoom = nextZoom;
        var newChrome = GetChromeMetrics(_zoom);
        var nextX = documentX - Math.Max(0d, (screenAnchorX / _zoom) - newChrome.RowHeaderWidth);
        var nextY = documentY - Math.Max(0d, (screenAnchorY / _zoom) - newChrome.ColumnHeaderHeight);
        ScrollTo(Math.Max(0d, nextX), Math.Max(0d, nextY), animated: false);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetView()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _analyticsTouch.CancelAll();
        _input.CancelAll();
        _lastLayout = null;
        _zoom = 1d;
        _scroll.Reset();
        HasRenderLoop = false;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public bool TryHitTest(double screenX, double screenY, out CellAddress address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_viewport is null || _lastSurfaceWidth <= 0d || _lastSurfaceHeight <= 0d)
        {
            address = default;
            return false;
        }

        var fullWidth = _lastSurfaceWidth / _zoom;
        var fullHeight = _lastSurfaceHeight / _zoom;
        var hit = SpreadsheetChromeGeometry.HitTest(
            screenX / _zoom,
            screenY / _zoom,
            fullWidth,
            fullHeight,
            _renderTheme);
        if (hit.Region != SpreadsheetChromeRegion.Body)
        {
            address = default;
            return false;
        }

        var scroll = _scroll.Snapshot;
        return _viewport.TryHitTest(
            hit.BodyX,
            hit.BodyY,
            scroll.OffsetX,
            scroll.OffsetY,
            out address);
    }

    public IReadOnlyList<SpreadsheetAnalyticsAccessibleNode>
        GetAnalyticsAccessibilityNodes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_analyticsInput is null || _lastLayout is null || _session is null)
        {
            return [];
        }

        return _analyticsInput.GetAccessibilityNodes(
            _lastLayout,
            ResolveAnalyticsName);
    }

    protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        var context = GRContext;
        var gpuFrame = default(NeraGpuFrameToken);
        if (!_disposed)
        {
            if (context is null)
            {
                _gpuLifecycle.NotifyContextLost();
            }
            else
            {
                gpuFrame = _gpuLifecycle.BeginFrame(context);
            }
        }

        var renderSucceeded = false;
        var successfulFrameTransitionRejected = false;
        try
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(ToSkColor(_renderTheme.Background));
            _lastSurfaceWidth = e.Info.Width;
            _lastSurfaceHeight = e.Info.Height;
            _lastLayout = null;

            if (!_disposed &&
                _session is not null &&
                e.Info.Width > 0 &&
                e.Info.Height > 0)
            {
                EnsureWorksheetSubscription();
                var fullWidth = e.Info.Width / _zoom;
                var fullHeight = e.Info.Height / _zoom;
                var chrome = SpreadsheetChromeGeometry.Calculate(
                    fullWidth,
                    fullHeight,
                    _renderTheme);
                _lastBodyWidth = chrome.BodyWidth;
                _lastBodyHeight = chrome.BodyHeight;
                AdvanceAnimatedScroll();

                if (chrome.BodyWidth > 0d && chrome.BodyHeight > 0d)
                {
                    var viewport = EnsureViewport();
                    var scroll = _scroll.Snapshot;
                    var frame = viewport.Compose(
                        scroll.OffsetX,
                        scroll.OffsetY,
                        chrome.BodyWidth,
                        chrome.BodyHeight,
                        ValidateOverscan(OverscanPixels),
                        _renderTheme);
                    _lastLayout = frame.Layout;
                    var displayList = SpreadsheetChromeDisplayListComposer.Compose(
                        frame.DisplayList,
                        frame.Layout,
                        _session.Selection.Capture(),
                        _renderTheme);

                    canvas.Save();
                    canvas.Scale((float)_zoom);
                    try
                    {
                        _renderer.Render(canvas, displayList);
                    }
                    finally
                    {
                        canvas.Restore();
                    }
                }
            }
            renderSucceeded = true;
        }
        finally
        {
            if (gpuFrame.IsValid)
            {
                var transitioned = renderSucceeded
                    ? _gpuLifecycle.TryCompleteFrame(gpuFrame)
                    : _gpuLifecycle.TryFailFrame(gpuFrame);
                successfulFrameTransitionRejected =
                    renderSucceeded && !transitioned;
            }
        }

        if (successfulFrameTransitionRejected)
        {
            throw new InvalidOperationException(
                "The MAUI GPU context changed before the Nera frame completed.");
        }

        base.OnPaintSurface(e);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        _analyticsTouch.CancelAll();
        _input.CancelAll();
        _lastLayout = null;
        if (args.OldHandler is not null &&
            !ReferenceEquals(args.OldHandler, args.NewHandler))
        {
            _gpuLifecycle.NotifyContextLost();
        }
        base.OnHandlerChanging(args);
    }

    protected override void OnTouch(SKTouchEventArgs e)
    {
        if (!_disposed)
        {
            ProcessTouchInput(e);
        }
        base.OnTouch(e);
    }

    internal void ProcessTouchInput(SKTouchEventArgs input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);

        var width = Math.Max(
            0d,
            _lastSurfaceWidth > 0d ? _lastSurfaceWidth : Width);
        var height = Math.Max(
            0d,
            _lastSurfaceHeight > 0d ? _lastSurfaceHeight : Height);
        var fullWidth = width / _zoom;
        var fullHeight = height / _zoom;
        var logicalX = input.Location.X / _zoom;
        var logicalY = input.Location.Y / _zoom;
        var hit = SpreadsheetChromeGeometry.HitTest(
            logicalX,
            logicalY,
            fullWidth,
            fullHeight,
            _renderTheme);
        var bodyPoint = new PointD(hit.BodyX, hit.BodyY);
        if (_analyticsTouch.Process(
                input,
                bodyPoint,
                hit.Region == SpreadsheetChromeRegion.Body))
        {
            input.Handled = true;
            return;
        }

        _input.Process(input);
        input.Handled = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _analyticsTouch.CancelAll();
        NeraSpreadsheetAnalyticsAccessibilityBridge.Detach(this);
        _disposed = true;
        _input.Dispose();
        _gpuLifecycle.Dispose();
        DetachSession();
        _renderer.Dispose();
        HasRenderLoop = false;
        GC.SuppressFinalize(this);
    }

    private static void OnWorkbookChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is NeraSpreadsheetView view)
        {
            view.SetWorkbookCore((Workbook?)newValue);
        }
    }

    private void SetWorkbookCore(Workbook? workbook)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _analyticsTouch.CancelAll();
        _input.CancelAll();
        _lastLayout = null;
        if (ReferenceEquals(_session?.Workbook, workbook))
        {
            return;
        }

        DetachSession();
        _session = workbook is null ? null : new SpreadsheetSession(workbook);
        _viewport = _session is null ? null : new SpreadsheetViewportEngine(_session);
        _analyticsInput = _viewport is null
            ? null
            : new SpreadsheetAnalyticsViewportInteractionController(_viewport);
        _scroll.Reset();
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
            _session.Selection.Changed += OnVisualStateChanged;
            _session.View.Changed += OnVisualStateChanged;
            _session.Analytics.Changed += OnVisualStateChanged;
            _session.AnalyticsPlacements.Changed += OnVisualStateChanged;
            _session.AnalyticsInteraction.Changed += OnVisualStateChanged;
            EnsureWorksheetSubscription();
        }
        InvalidateSurface();
    }

    private void DetachSession()
    {
        DetachWorksheetSubscription();
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnVisualStateChanged;
            _session.View.Changed -= OnVisualStateChanged;
            _session.Analytics.Changed -= OnVisualStateChanged;
            _session.AnalyticsPlacements.Changed -= OnVisualStateChanged;
            _session.AnalyticsInteraction.Changed -= OnVisualStateChanged;
        }
        _session = null;
        _viewport = null;
        _analyticsInput = null;
        _lastLayout = null;
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (ReferenceEquals(worksheet, _subscribedWorksheet))
        {
            return;
        }

        DetachWorksheetSubscription();
        _subscribedWorksheet = worksheet;
        if (worksheet is not null)
        {
            worksheet.CellsChanged += OnVisualStateChanged;
            worksheet.Dimensions.Changed += OnDimensionsChanged;
        }
    }

    private void DetachWorksheetSubscription()
    {
        if (_subscribedWorksheet is null)
        {
            return;
        }
        _subscribedWorksheet.CellsChanged -= OnVisualStateChanged;
        _subscribedWorksheet.Dimensions.Changed -= OnDimensionsChanged;
        _subscribedWorksheet = null;
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        _analyticsTouch.CancelAll();
        _input.CancelAll();
        _lastLayout = null;
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _scroll.Reset();
        InvalidateSurface();
    }

    private void OnDimensionsChanged(object? sender, EventArgs e)
    {
        _lastLayout = null;
        _viewport?.InvalidateMetrics();
        InvalidateSurface();
    }

    private void OnVisualStateChanged(object? sender, EventArgs e) => InvalidateSurface();

    private SpreadsheetViewportEngine EnsureViewport() =>
        _viewport ?? throw new InvalidOperationException("A workbook is required before viewport composition.");

    private string? ResolveAnalyticsName(SpreadsheetAnalyticsItemKey item)
    {
        if (_session is null)
        {
            return null;
        }

        var worksheet = _session.ActiveWorksheet;
        return item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart =>
                _session.Analytics.GetCharts(worksheet)
                    .FirstOrDefault(value => value.Id == item.Id)
                    ?.Name,
            SpreadsheetAnalyticsItemKind.Pivot =>
                _session.Analytics.GetPivots(worksheet)
                    .FirstOrDefault(value => value.Id == item.Id)
                    ?.Name,
            _ => null,
        };
    }

    private void AdvanceAnimatedScroll()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastPaintTimestamp, now);
        _lastPaintTimestamp = now;
        if (!_scroll.HasPendingMotion)
        {
            HasRenderLoop = false;
            return;
        }

        var result = _scroll.AdvanceFrame(elapsed, GetScrollBounds());
        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
        HasRenderLoop = _scroll.HasPendingMotion;
    }

    private ScrollBounds GetScrollBounds()
    {
        if (_viewport is null)
        {
            return new ScrollBounds(0d, 0d);
        }

        var extent = _viewport.GetContentExtent();
        var bodyWidth = _lastBodyWidth;
        var bodyHeight = _lastBodyHeight;
        if (bodyWidth <= 0d || bodyHeight <= 0d)
        {
            var chrome = GetChromeMetrics(_zoom);
            bodyWidth = chrome.BodyWidth;
            bodyHeight = chrome.BodyHeight;
        }
        return new ScrollBounds(
            Math.Max(0d, extent.Width - Math.Max(0d, bodyWidth)),
            Math.Max(0d, extent.Height - Math.Max(0d, bodyHeight)));
    }

    private SpreadsheetChromeMetrics GetChromeMetrics(double zoom)
    {
        var width = Math.Max(0d, _lastSurfaceWidth > 0d ? _lastSurfaceWidth : Width);
        var height = Math.Max(0d, _lastSurfaceHeight > 0d ? _lastSurfaceHeight : Height);
        return SpreadsheetChromeGeometry.Calculate(
            width / zoom,
            height / zoom,
            _renderTheme);
    }

    private NeraSpreadsheetInputState CaptureInputState()
    {
        var scroll = _scroll.Snapshot;
        return new NeraSpreadsheetInputState(
            _zoom,
            scroll.OffsetX,
            scroll.OffsetY);
    }

    private NeraSpreadsheetInputChrome GetInputChrome(double zoom)
    {
        var chrome = GetChromeMetrics(zoom);
        return new NeraSpreadsheetInputChrome(
            chrome.RowHeaderWidth,
            chrome.ColumnHeaderHeight);
    }

    private void ApplyInputZoom(
        double zoom,
        double offsetX,
        double offsetY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _zoom = zoom;
        ScrollTo(offsetX, offsetY, animated: false);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private void QueueInputWheel(double delta)
    {
        _scroll.QueueDelta(new ScrollDelta(
            0d,
            delta,
            ScrollInputKind.Wheel));
        HasRenderLoop = true;
        InvalidateSurface();
    }

    private void SelectAt(SKPoint screenPoint)
    {
        if (_session is null || _viewport is null || _lastSurfaceWidth <= 0d || _lastSurfaceHeight <= 0d)
        {
            return;
        }

        var fullWidth = _lastSurfaceWidth / _zoom;
        var fullHeight = _lastSurfaceHeight / _zoom;
        var hit = SpreadsheetChromeGeometry.HitTest(
            screenPoint.X / _zoom,
            screenPoint.Y / _zoom,
            fullWidth,
            fullHeight,
            _renderTheme);
        var scroll = _scroll.Snapshot;
        switch (hit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                break;
            case SpreadsheetChromeRegion.RowHeader:
                if (_viewport.TryHitTestRow(hit.BodyY, scroll.OffsetY, out var rowIndex))
                {
                    _session.Selection.SelectRow(rowIndex);
                }
                break;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (_viewport.TryHitTestColumn(hit.BodyX, scroll.OffsetX, out var columnIndex))
                {
                    _session.Selection.SelectColumn(columnIndex);
                }
                break;
            case SpreadsheetChromeRegion.Body:
                if (_viewport.TryHitTest(
                        hit.BodyX,
                        hit.BodyY,
                        scroll.OffsetX,
                        scroll.OffsetY,
                        out var address))
                {
                    _session.Selection.SetActiveCell(address);
                }
                break;
        }
    }

    private static double ValidateOverscan(double value)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new InvalidOperationException("OverscanPixels must be finite and non-negative.");
        }
        return value;
    }

    private static SKColor ToSkColor(ColorRgba color) =>
        new(color.Red, color.Green, color.Blue, color.Alpha);
}
