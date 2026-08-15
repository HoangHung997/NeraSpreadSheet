using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

public sealed class ScrollChangedEventArgs : EventArgs
{
    public ScrollChangedEventArgs(ScrollSnapshot snapshot) { Snapshot = snapshot; }
    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetControl : FrameworkElement
{
    private readonly ContinuousScrollController _scrollController = new();
    private readonly WpfDisplayListRenderer _displayListRenderer = new();
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private Worksheet? _subscribedWorksheet;
    private TimeSpan? _lastRenderingTime;
    private bool _isFrameLoopAttached;

    public NeraSpreadsheetControl()
    {
        Focusable = true;
        Unloaded += OnUnloaded;
    }

    public SpreadsheetSession? Session
    {
        get => _session;
        set => SetSession(value);
    }

    public Workbook? Workbook
    {
        get => _session?.Workbook;
        set => SetSession(value is null ? null : new SpreadsheetSession(value));
    }

    public Brush Background { get; set; } = Brushes.White;
    public SpreadsheetRenderTheme RenderTheme { get; set; } = new();
    public double ContentWidth { get; private set; }
    public double ContentHeight { get; private set; }
    public double WheelPixelsPerNotch { get; set; } = 96d;
    public double OverscanPixels { get; set; } = 128d;
    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
        if (_session is null || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        EnsureWorksheetSubscription();
        var viewport = EnsureViewport();
        var snapshot = _scrollController.Snapshot;
        var frame = viewport.Compose(snapshot.X, snapshot.Y, ActualWidth, ActualHeight, OverscanPixels, RenderTheme);
        ContentWidth = frame.Layout.ContentWidth;
        ContentHeight = frame.Layout.ContentHeight;
        _displayListRenderer.Render(drawingContext, frame.DisplayList, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var notches = e.Delta / 120d;
        var delta = -notches * WheelPixelsPerNotch;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            _scrollController.QueueDelta(new ScrollDelta(delta, 0d, ScrollInputKind.Wheel));
        }
        else
        {
            _scrollController.QueueDelta(new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        }
        EnsureFrameLoop();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (_session is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        var scroll = _scrollController.Snapshot;
        if (!EnsureViewport().TryHitTest(point.X, point.Y, scroll.X, scroll.Y, out var address))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            _session.Selection.ExtendTo(address);
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _session.Selection.AddRange(new CellRange(address, address));
        }
        else
        {
            _session.Selection.SetActiveCell(address);
        }
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_session is null)
        {
            return;
        }

        var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (control && e.Key == Key.Z)
        {
            e.Handled = _session.Undo();
            return;
        }
        if (control && e.Key == Key.Y)
        {
            e.Handled = _session.Redo();
            return;
        }
        if (e.Key == Key.Delete)
        {
            e.Handled = _session.ClearSelection();
            return;
        }

        var delta = e.Key switch
        {
            Key.Left => (Row: 0, Column: -1),
            Key.Right => (Row: 0, Column: 1),
            Key.Up => (Row: -1, Column: 0),
            Key.Down => (Row: 1, Column: 0),
            _ => (Row: 0, Column: 0),
        };
        if (delta == default)
        {
            return;
        }

        MoveActiveCell(delta.Row, delta.Column, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        e.Handled = true;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        EnsureFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        InvalidateVisual();
        if (animated)
        {
            EnsureFrameLoop();
        }
    }

    private SpreadsheetViewportEngine EnsureViewport() => _viewport ??= new SpreadsheetViewportEngine(
        _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private void SetSession(SpreadsheetSession? value)
    {
        if (ReferenceEquals(_session, value))
        {
            return;
        }

        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetViewportEngine(value);
        _scrollController.Reset();
        AttachSessionEvents();
        UpdateContentExtent();
        InvalidateVisual();
    }

    private void AttachSessionEvents()
    {
        if (_session is null)
        {
            return;
        }
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
        }
        DetachWorksheetSubscription();
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (ReferenceEquals(_subscribedWorksheet, worksheet))
        {
            return;
        }
        DetachWorksheetSubscription();
        _subscribedWorksheet = worksheet;
        if (worksheet is not null)
        {
            worksheet.CellsChanged += OnCellsChanged;
            worksheet.Dimensions.Changed += OnDimensionsChanged;
        }
    }

    private void DetachWorksheetSubscription()
    {
        if (_subscribedWorksheet is null)
        {
            return;
        }
        _subscribedWorksheet.CellsChanged -= OnCellsChanged;
        _subscribedWorksheet.Dimensions.Changed -= OnDimensionsChanged;
        _subscribedWorksheet = null;
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _scrollController.Reset();
        UpdateContentExtent();
        InvalidateVisual();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => InvalidateVisual();
    private void OnCellsChanged(object? sender, CellsChangedEventArgs e) => InvalidateVisual();

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateContentExtent();
        InvalidateVisual();
    }

    private void UpdateContentExtent()
    {
        if (_viewport is null)
        {
            ContentWidth = 0d;
            ContentHeight = 0d;
            return;
        }
        var extent = _viewport.GetContentExtent();
        ContentWidth = extent.Width;
        ContentHeight = extent.Height;
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null)
        {
            return;
        }
        var active = _session.Selection.ActiveCell;
        var row = Math.Clamp(active.RowIndex + rowDelta, 0, SpreadsheetLimits.MaxRows - 1);
        var column = Math.Clamp(active.ColumnIndex + columnDelta, 0, SpreadsheetLimits.MaxColumns - 1);
        var next = new CellAddress(row, column);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
    }

    private void EnsureFrameLoop()
    {
        if (_isFrameLoopAttached) return;
        CompositionTarget.Rendering += OnRendering;
        _isFrameLoopAttached = true;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs renderingEventArgs) return;
        var currentTime = renderingEventArgs.RenderingTime;
        var elapsed = _lastRenderingTime is null ? TimeSpan.FromSeconds(1d / 60d) : currentTime - _lastRenderingTime.Value;
        _lastRenderingTime = currentTime;
        var bounds = new ScrollBounds(Math.Max(0d, ContentWidth - ActualWidth), Math.Max(0d, ContentHeight - ActualHeight));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);
        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.Snapshot));
            InvalidateVisual();
        }
        if (!_scrollController.HasPendingMotion) DetachFrameLoop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFrameLoop();
        DetachSessionEvents();
    }

    private void DetachFrameLoop()
    {
        if (!_isFrameLoopAttached) return;
        CompositionTarget.Rendering -= OnRendering;
        _isFrameLoopAttached = false;
        _lastRenderingTime = null;
    }
}
