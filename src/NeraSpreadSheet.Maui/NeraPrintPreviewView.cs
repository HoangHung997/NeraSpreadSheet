using Microsoft.Maui.Controls;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Skia-backed MAUI viewport for the platform-neutral print-preview session.
/// It renders only visible/overscan pages and never creates one native element
/// per worksheet cell or document page.
/// </summary>
public sealed class NeraPrintPreviewView : SKCanvasView, IDisposable
{
    public static readonly BindableProperty SessionProperty =
        BindableProperty.Create(
            nameof(Session),
            typeof(SpreadsheetPrintPreviewSession),
            typeof(NeraPrintPreviewView),
            default(SpreadsheetPrintPreviewSession),
            propertyChanged: static (bindable, _, _) =>
            {
                var view = (NeraPrintPreviewView)bindable;
                view._lastFrame = null;
                view.UpdateViewportSize();
                view.InvalidateSurface();
            });

    private readonly SkiaDisplayListRenderer _renderer = new();
    private readonly SKPaint _paperPaint = new()
    {
        Color = SKColors.White,
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    private readonly SKPaint _shadowPaint = new()
    {
        Color = new SKColor(0, 0, 0, 90),
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    private readonly SKPaint _borderPaint = new()
    {
        Color = new SKColor(148, 148, 148),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        IsAntialias = false,
    };

    private SpreadsheetPrintPreviewFrame? _lastFrame;
    private SKPoint _lastPanPoint;
    private bool _isPanning;
    private double _pinchStartZoom = 1d;
    private Point _pinchAnchor;
    private bool _disposed;

    public NeraPrintPreviewView()
    {
        IgnorePixelScaling = true;
        EnableTouchEvents = true;
        AutomationId = "NeraPrintPreview";
        SemanticProperties.SetDescription(this, "Nera print preview");
        SemanticProperties.SetHint(
            this,
            "Drag to pan and pinch to zoom the visible print pages.");
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);
        SizeChanged += OnPreviewSizeChanged;
    }

    public SpreadsheetPrintPreviewSession? Session
    {
        get => (SpreadsheetPrintPreviewSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public SpreadsheetPrintPreviewFrame? LastFrame => _lastFrame;

    public double Zoom => Session?.Zoom ?? 1d;

    public double OffsetX => Session?.OffsetX ?? 0d;

    public double OffsetY => Session?.OffsetY ?? 0d;

    public event EventHandler? ZoomChanged;

    public event EventHandler? ScrollChanged;

    public void SetZoom(
        double zoom,
        double anchorViewportX = 0d,
        double anchorViewportY = 0d)
    {
        RequireSession().SetZoom(
            zoom,
            anchorViewportX,
            anchorViewportY);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public void SetColumns(int columns)
    {
        RequireSession().SetColumns(columns);
        InvalidateSurface();
    }

    public void ScrollTo(double offsetXDips, double offsetYDips)
    {
        RequireSession().ScrollTo(offsetXDips, offsetYDips);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public void ScrollBy(double deltaXDips, double deltaYDips)
    {
        RequireSession().ScrollBy(deltaXDips, deltaYDips);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public bool TryHitTestPage(
        double viewportX,
        double viewportY,
        out SpreadsheetPrintPreviewPageSlot page,
        out PointD pagePoint)
    {
        if (Session is null)
        {
            page = default;
            pagePoint = default;
            return false;
        }
        return Session.TryHitTest(
            viewportX,
            viewportY,
            out page,
            out pagePoint);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(48, 49, 52));
        if (!_disposed &&
            Session is { } session &&
            e.Info.Width > 0 &&
            e.Info.Height > 0)
        {
            session.SetViewportSize(e.Info.Width, e.Info.Height);
            var frame = session.Compose();
            _lastFrame = frame;
            foreach (var page in frame.Pages)
            {
                var slot = page.Slot;
                var left = slot.BoundsDips.X - frame.Layout.OffsetXDips;
                var top = slot.BoundsDips.Y - frame.Layout.OffsetYDips;
                var bounds = new SKRect(
                    checked((float)left),
                    checked((float)top),
                    checked((float)(left + slot.BoundsDips.Width)),
                    checked((float)(top + slot.BoundsDips.Height)));
                if (bounds.Right <= 0f || bounds.Bottom <= 0f ||
                    bounds.Left >= e.Info.Width ||
                    bounds.Top >= e.Info.Height)
                {
                    continue;
                }

                var shadow = bounds;
                shadow.Offset(4f, 5f);
                canvas.DrawRect(shadow, _shadowPaint);
                canvas.DrawRect(bounds, _paperPaint);
                var saveCount = canvas.Save();
                try
                {
                    canvas.ClipRect(
                        bounds,
                        SKClipOperation.Intersect,
                        antialias: false);
                    canvas.Translate(bounds.Left, bounds.Top);
                    canvas.Scale(
                        checked((float)frame.Layout.Zoom),
                        checked((float)frame.Layout.Zoom));
                    _renderer.Render(canvas, page.DisplayList);
                }
                finally
                {
                    canvas.RestoreToCount(saveCount);
                }
                canvas.DrawRect(bounds, _borderPaint);
            }
        }
        else
        {
            _lastFrame = null;
        }
        base.OnPaintSurface(e);
    }

    protected override void OnTouch(SKTouchEventArgs e)
    {
        if (!_disposed && Session is not null)
        {
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _isPanning = true;
                    _lastPanPoint = e.Location;
                    e.Handled = true;
                    break;
                case SKTouchAction.Moved when _isPanning:
                    ScrollBy(
                        _lastPanPoint.X - e.Location.X,
                        _lastPanPoint.Y - e.Location.Y);
                    _lastPanPoint = e.Location;
                    e.Handled = true;
                    break;
                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    _isPanning = false;
                    e.Handled = true;
                    break;
            }
        }
        base.OnTouch(e);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        _isPanning = false;
        base.OnHandlerChanging(args);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        SizeChanged -= OnPreviewSizeChanged;
        foreach (var recognizer in GestureRecognizers
                     .OfType<PinchGestureRecognizer>()
                     .ToArray())
        {
            recognizer.PinchUpdated -= OnPinchUpdated;
        }
        _renderer.Dispose();
        _paperPaint.Dispose();
        _shadowPaint.Dispose();
        _borderPaint.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnPinchUpdated(
        object? sender,
        PinchGestureUpdatedEventArgs e)
    {
        if (_disposed || Session is not { } session)
        {
            return;
        }

        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinchStartZoom = session.Zoom;
                _pinchAnchor = new Point(
                    e.ScaleOrigin.X * Math.Max(0d, Width),
                    e.ScaleOrigin.Y * Math.Max(0d, Height));
                break;
            case GestureStatus.Running:
                SetZoom(
                    Math.Clamp(
                        _pinchStartZoom * e.Scale,
                        0.05d,
                        8d),
                    _pinchAnchor.X,
                    _pinchAnchor.Y);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                break;
        }
    }

    private void OnPreviewSizeChanged(object? sender, EventArgs e)
    {
        UpdateViewportSize();
        InvalidateSurface();
    }

    private void UpdateViewportSize()
    {
        Session?.SetViewportSize(
            Math.Max(0d, Width),
            Math.Max(0d, Height));
    }

    private SpreadsheetPrintPreviewSession RequireSession() =>
        Session ?? throw new InvalidOperationException(
            "Assign a print-preview session before changing the viewport.");
}
