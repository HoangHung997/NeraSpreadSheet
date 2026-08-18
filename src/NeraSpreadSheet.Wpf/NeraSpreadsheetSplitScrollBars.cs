using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

public sealed class NeraSpreadsheetSplitScrollBarController : IDisposable
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly NeraSpreadsheetSplitController _split;
    private NeraSpreadsheetSplitScrollBarAdorner? _adorner;
    private AdornerLayer? _adornerLayer;
    private SpreadsheetSplitScrollBarStyle _style;

    internal NeraSpreadsheetSplitScrollBarController(
        NeraSpreadsheetControl owner,
        NeraSpreadsheetSplitController split,
        SpreadsheetSplitScrollBarStyle? style)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _style = style ?? new SpreadsheetSplitScrollBarStyle();
        _adorner = new NeraSpreadsheetSplitScrollBarAdorner(
            owner,
            split,
            _style);
        owner.Loaded += OnOwnerLoaded;
        owner.Unloaded += OnOwnerUnloaded;
        owner.SizeChanged += OnOwnerSizeChanged;
        AttachIfPossible();
    }

    public bool IsDisposed => _adorner is null;

    public bool IsAttached => _adornerLayer is not null;

    public bool IsVisible
    {
        get => GetAdorner().Visibility == Visibility.Visible;
        set
        {
            GetAdorner().Visibility = value
                ? Visibility.Visible
                : Visibility.Collapsed;
            GetAdorner().RefreshFromSplitFrame();
        }
    }

    public SpreadsheetSplitScrollBarStyle Style
    {
        get => _style;
        set
        {
            _style = value ?? throw new ArgumentNullException(nameof(value));
            GetAdorner().ScrollBarStyle = value;
        }
    }

    public SpreadsheetSplitScrollBarLayout? Layout =>
        GetAdorner().ScrollBarLayout;

    public int ScrollBarCount => Layout?.Count ?? 0;

    public SpreadsheetSplitScrollBarHit HitTest(double bodyX, double bodyY) =>
        Layout?.HitTest(new PointD(bodyX, bodyY)) ?? default;

    public void Refresh()
    {
        AttachOrThrow();
        _split.RenderNow();
        GetAdorner().RefreshFromSplitFrame();
        _owner.UpdateLayout();
    }

    public void Dispose()
    {
        var adorner = _adorner;
        if (adorner is null)
        {
            return;
        }

        _owner.Loaded -= OnOwnerLoaded;
        _owner.Unloaded -= OnOwnerUnloaded;
        _owner.SizeChanged -= OnOwnerSizeChanged;
        DetachAdorner();
        _adorner = null;
        adorner.Dispose();
        NeraSpreadsheetSplitScrollBarExtensions.Remove(_owner, this);
        GC.SuppressFinalize(this);
    }

    private NeraSpreadsheetSplitScrollBarAdorner GetAdorner()
    {
        ObjectDisposedException.ThrowIf(_adorner is null, this);
        return _adorner!;
    }

    private void AttachIfPossible()
    {
        if (_adornerLayer is not null ||
            _adorner is null ||
            !_owner.IsLoaded)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(_owner);
        if (layer is null)
        {
            return;
        }

        layer.Add(_adorner);
        _adornerLayer = layer;
        _adorner.RefreshFromSplitFrame();
        layer.UpdateLayout();
    }

    private void AttachOrThrow()
    {
        AttachIfPossible();
        if (_adornerLayer is null)
        {
            throw new InvalidOperationException(
                "The WPF spreadsheet must be loaded inside an AdornerDecorator before split scrollbars can render.");
        }
    }

    private void DetachAdorner()
    {
        var layer = _adornerLayer;
        var adorner = _adorner;
        _adornerLayer = null;
        if (layer is not null && adorner is not null)
        {
            layer.Remove(adorner);
        }
    }

    private void OnOwnerLoaded(object sender, RoutedEventArgs e) =>
        AttachIfPossible();

    private void OnOwnerUnloaded(object sender, RoutedEventArgs e) =>
        DetachAdorner();

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) =>
        _adorner?.RefreshFromSplitFrame();
}

public static class NeraSpreadsheetSplitScrollBarExtensions
{
    private static readonly ConditionalWeakTable<
        NeraSpreadsheetControl,
        NeraSpreadsheetSplitScrollBarController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetSplitScrollBarController EnableSplitPaneScrollBars(
        this NeraSpreadsheetControl control,
        SpreadsheetSplitScrollBarStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (!existing.IsDisposed)
                {
                    if (style is not null)
                    {
                        existing.Style = style;
                    }
                    if (control.IsLoaded)
                    {
                        existing.Refresh();
                    }
                    return existing;
                }
                Controllers.Remove(control);
            }

            var split = control.TryGetSplitPaneController(out var existingSplit)
                ? existingSplit
                : control.EnableSplitPanes();
            var controller = new NeraSpreadsheetSplitScrollBarController(
                control,
                split,
                style);
            Controllers.Add(control, controller);
            if (control.IsLoaded)
            {
                controller.Refresh();
            }
            return controller;
        }
    }

    public static bool TryGetSplitPaneScrollBarController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetSplitScrollBarController controller)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing) &&
                !existing.IsDisposed)
            {
                controller = existing;
                return true;
            }
        }

        controller = null!;
        return false;
    }

    public static bool DisableSplitPaneScrollBars(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetSplitScrollBarController controller;
        lock (SyncRoot)
        {
            if (!Controllers.TryGetValue(control, out var existing))
            {
                return false;
            }
            controller = existing;
            Controllers.Remove(control);
        }

        controller.Dispose();
        return true;
    }

    internal static void Remove(
        NeraSpreadsheetControl control,
        NeraSpreadsheetSplitScrollBarController controller)
    {
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var current) &&
                ReferenceEquals(current, controller))
            {
                Controllers.Remove(control);
            }
        }
    }
}

internal sealed class NeraSpreadsheetSplitScrollBarAdorner : Adorner, IDisposable
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly NeraSpreadsheetSplitController _split;
    private readonly SpreadsheetSplitScrollBarInteractionController _interaction = new();
    private SpreadsheetSplitScrollBarStyle _style;
    private SpreadsheetSplitScrollBarLayout? _layout;
    private bool _disposed;

    internal NeraSpreadsheetSplitScrollBarAdorner(
        NeraSpreadsheetControl owner,
        NeraSpreadsheetSplitController split,
        SpreadsheetSplitScrollBarStyle style)
        : base(owner)
    {
        _owner = owner;
        _split = split;
        _style = style;
        IsHitTestVisible = true;
        Focusable = false;
        ClipToBounds = true;
        _split.SplitChanged += OnSplitChanged;
        _split.PaneScrollChanged += OnPaneScrollChanged;
    }

    internal SpreadsheetSplitScrollBarStyle ScrollBarStyle
    {
        get => _style;
        set
        {
            _style = value;
            RefreshFromSplitFrame();
        }
    }

    internal SpreadsheetSplitScrollBarLayout? ScrollBarLayout => _layout;

    internal void RefreshFromSplitFrame()
    {
        if (_disposed || Visibility != Visibility.Visible)
        {
            return;
        }

        var frame = _split.LastFrame;
        if (frame is null)
        {
            _layout = null;
        }
        else
        {
            _layout = frame.CreateScrollBarLayout(
                GetContentExtent(frame),
                _style);
        }
        InvalidateVisual();
    }

    protected override HitTestResult? HitTestCore(
        PointHitTestParameters hitTestParameters)
    {
        if (_layout is null)
        {
            return null;
        }

        var bodyPoint = ToBodyPoint(hitTestParameters.HitPoint);
        return _layout.HitTest(bodyPoint).IsHit
            ? new PointHitTestResult(this, hitTestParameters.HitPoint)
            : null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_disposed || _layout is null)
        {
            return;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme);
        drawingContext.PushTransform(new TranslateTransform(
            chrome.RowHeaderWidth,
            chrome.ColumnHeaderHeight));
        foreach (var scrollBar in _layout.ScrollBars)
        {
            drawingContext.DrawRectangle(
                ToBrush(_style.TrackColor),
                null,
                ToRect(scrollBar.TrackBounds));
            drawingContext.DrawRectangle(
                ToBrush(scrollBar.PaneId == _split.ActivePane
                    ? _style.ActiveThumbColor
                    : _style.ThumbColor),
                new Pen(ToBrush(_style.BorderColor), 1d),
                ToRect(scrollBar.ThumbBounds));
        }
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_layout is null)
        {
            return;
        }

        var result = _interaction.BeginPointer(
            _layout,
            ToBodyPoint(e.GetPosition(this)));
        if (!result.Handled)
        {
            return;
        }

        if (result.ScrollRequest is { } request)
        {
            ApplyRequest(request);
        }
        else if (_interaction.DragPaneId is { } paneId)
        {
            _split.SetActivePane(paneId);
        }
        if (result.IsDragging)
        {
            CaptureMouse();
        }
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_layout is null)
        {
            return;
        }

        var point = ToBodyPoint(e.GetPosition(this));
        if (_interaction.IsDragging)
        {
            var result = _interaction.MovePointer(point);
            if (result.ScrollRequest is { } request)
            {
                ApplyRequest(request);
            }
            Cursor = Cursors.Hand;
            e.Handled = true;
            return;
        }

        Cursor = _layout.HitTest(point).IsHit ? Cursors.Hand : null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_interaction.EndPointer())
        {
            return;
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        Cursor = _layout?.HitTest(ToBodyPoint(e.GetPosition(this))).IsHit == true
            ? Cursors.Hand
            : null;
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _interaction.Cancel();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_layout is null)
        {
            return;
        }

        var hit = _layout.HitTest(ToBodyPoint(e.GetPosition(this)));
        var paneId = hit.IsHit ? hit.PaneId : _split.ActivePane;
        _split.SetActivePane(paneId);
        var delta = -(e.Delta / 120d) * _owner.WheelPixelsPerNotch;
        _split.QueuePaneScroll(
            paneId,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
                : new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        e.Handled = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _split.SplitChanged -= OnSplitChanged;
        _split.PaneScrollChanged -= OnPaneScrollChanged;
        _interaction.Cancel();
        _disposed = true;
    }

    private void ApplyRequest(SpreadsheetSplitScrollRequest request)
    {
        var current = _split.GetPaneScroll(request.PaneId);
        _split.SetActivePane(request.PaneId);
        _split.ScrollPaneTo(
            request.PaneId,
            request.Axis == SpreadsheetScrollBarAxis.Horizontal
                ? request.Offset
                : current.X,
            request.Axis == SpreadsheetScrollBarAxis.Vertical
                ? request.Offset
                : current.Y,
            animated: false);
        RefreshFromSplitFrame();
    }

    private PointD ToBodyPoint(Point point)
    {
        var chrome = SpreadsheetChromeGeometry.Calculate(
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme);
        return new PointD(
            point.X - chrome.RowHeaderWidth,
            point.Y - chrome.ColumnHeaderHeight);
    }

    private void OnSplitChanged(
        object? sender,
        SpreadsheetSplitChangedEventArgs e) =>
        RefreshFromSplitFrame();

    private void OnPaneScrollChanged(
        object? sender,
        SpreadsheetPaneScrollChangedEventArgs e) =>
        RefreshFromSplitFrame();

    private static SizeD GetContentExtent(
        SpreadsheetSplitViewportFrame frame)
    {
        if (frame.Panes.Count == 0)
        {
            return default;
        }

        var layout = frame.Panes[0].ViewportFrame.Layout;
        return new SizeD(layout.ContentWidth, layout.ContentHeight);
    }

    private static Rect ToRect(RectD bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static SolidColorBrush ToBrush(ColorRgba color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue));
        brush.Freeze();
        return brush;
    }
}
