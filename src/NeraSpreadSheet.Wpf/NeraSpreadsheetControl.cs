using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Wpf;

public sealed class ScrollChangedEventArgs : EventArgs
{
    public ScrollChangedEventArgs(ScrollSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetControl : FrameworkElement
{
    private readonly ContinuousScrollController _scrollController = new();
    private TimeSpan? _lastRenderingTime;
    private bool _isFrameLoopAttached;

    public NeraSpreadsheetControl()
    {
        Focusable = true;
        Unloaded += OnUnloaded;
    }

    public Workbook? Workbook { get; set; }

    public Brush Background { get; set; } = Brushes.White;

    public double ContentWidth { get; set; }

    public double ContentHeight { get; set; }

    public double WheelPixelsPerNotch { get; set; } = 96d;

    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var notches = e.Delta / 120d;
        _scrollController.QueueDelta(new ScrollDelta(
            0d,
            -notches * WheelPixelsPerNotch,
            ScrollInputKind.Wheel));
        EnsureFrameLoop();
        e.Handled = true;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        EnsureFrameLoop();
    }

    private void EnsureFrameLoop()
    {
        if (_isFrameLoopAttached)
        {
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        _isFrameLoopAttached = true;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs renderingEventArgs)
        {
            return;
        }

        var currentTime = renderingEventArgs.RenderingTime;
        var elapsed = _lastRenderingTime is null
            ? TimeSpan.FromSeconds(1d / 60d)
            : currentTime - _lastRenderingTime.Value;
        _lastRenderingTime = currentTime;

        var bounds = new ScrollBounds(
            Math.Max(0d, ContentWidth - ActualWidth),
            Math.Max(0d, ContentHeight - ActualHeight));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);

        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.Snapshot));
            InvalidateVisual();
        }

        if (!_scrollController.HasPendingMotion)
        {
            DetachFrameLoop();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachFrameLoop();

    private void DetachFrameLoop()
    {
        if (!_isFrameLoopAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _isFrameLoopAttached = false;
        _lastRenderingTime = null;
    }
}
