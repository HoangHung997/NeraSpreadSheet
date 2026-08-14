using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.WinForms;

public sealed class ScrollChangedEventArgs : EventArgs
{
    public ScrollChangedEventArgs(ScrollSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetControl : Control
{
    private readonly ContinuousScrollController _scrollController = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private DateTime _lastFrameUtc = DateTime.UtcNow;

    public NeraSpreadsheetControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.White;
        TabStop = true;
        _frameTimer = new System.Windows.Forms.Timer { Interval = 8 };
        _frameTimer.Tick += OnFrameTick;
    }

    public Workbook? Workbook { get; set; }

    public double ContentWidth { get; set; }

    public double ContentHeight { get; set; }

    public double WheelPixelsPerNotch { get; set; } = 96d;

    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var notches = e.Delta / 120d;
        _scrollController.QueueDelta(new ScrollDelta(
            0d,
            -notches * WheelPixelsPerNotch,
            ScrollInputKind.Wheel));
        StartFrameLoop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        base.OnPaint(e);
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        StartFrameLoop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Tick -= OnFrameTick;
            _frameTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StartFrameLoop()
    {
        _lastFrameUtc = DateTime.UtcNow;
        _frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastFrameUtc;
        _lastFrameUtc = now;

        var bounds = new ScrollBounds(
            Math.Max(0d, ContentWidth - ClientSize.Width),
            Math.Max(0d, ContentHeight - ClientSize.Height));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);

        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.Snapshot));
            Invalidate();
        }

        if (!_scrollController.HasPendingMotion)
        {
            _frameTimer.Stop();
        }
    }
}
