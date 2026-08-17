using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation.Performance;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;
using NeraSelectionChangedEventArgs = NeraSpreadSheet.Interaction.SelectionChangedEventArgs;

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
    private readonly FramePacingMonitor _framePacing = new();
    private readonly VisualCollection _visuals;
    private readonly WpfDirect2DGpuSurface _gpuSurface;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetCellEditorController? _cellEditor;
    private Worksheet? _subscribedWorksheet;
    private TimeSpan? _lastRenderingTime;
    private bool _isFrameLoopAttached;
    private bool _sessionEventsAttached;
    private Rect _editorBounds = Rect.Empty;
    private WpfRenderingBackend _renderingBackend;

    public NeraSpreadsheetControl()
    {
        Focusable = true;
        _visuals = new VisualCollection(this);
        _gpuSurface = new WpfDirect2DGpuSurface
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _editor = new TextBox
        {
            Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(2d, 0d, 2d, 0d),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _editor.KeyDown += OnEditorKeyDown;
        _visuals.Add(_gpuSurface);
        _visuals.Add(_editor);
        Loaded += OnLoaded;
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
    public bool IsEditing => _cellEditor?.IsEditing == true;
    public FramePacingSnapshot FramePacing => _framePacing.Capture();

    public WpfRenderingBackend RenderingBackend
    {
        get => _renderingBackend;
        set
        {
            if (_renderingBackend == value)
            {
                return;
            }
            _renderingBackend = value;
            UpdateGpuSurfaceVisibility();
            InvalidateVisual();
        }
    }

    public WpfGpuRendererDiagnostics? GpuDiagnostics =>
        _renderingBackend == WpfRenderingBackend.Direct2DD3DImage
            ? new WpfGpuRendererDiagnostics(
                _gpuSurface.TextureWidth,
                _gpuSurface.TextureHeight,
                _gpuSurface.CachedTextLayoutCount,
                _gpuSurface.TextLayoutCacheHits,
                _gpuSurface.TextLayoutCacheMisses,
                _gpuSurface.TextLayoutCacheEvictions)
            : null;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        _gpuSurface.Measure(availableSize);
        _editor.Measure(availableSize);
        return new Size(0d, 0d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _gpuSurface.Arrange(new Rect(0d, 0d, finalSize.Width, finalSize.Height));
        if (_editor.Visibility == Visibility.Visible && !_editorBounds.IsEmpty)
        {
            _editor.Arrange(_editorBounds);
        }
        else
        {
            _editor.Arrange(new Rect(0d, 0d, 0d, 0d));
        }
        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        _framePacing.RecordFrame();

        if (_session is null || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            _gpuSurface.SetDisplayList(null);
            UpdateGpuSurfaceVisibility();
            drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
            return;
        }

        EnsureWorksheetSubscription();
        var viewport = EnsureViewport();
        var snapshot = _scrollController.Snapshot;
        var frame = viewport.Compose(snapshot.OffsetX, snapshot.OffsetY, ActualWidth, ActualHeight, OverscanPixels, RenderTheme);
        ContentWidth = frame.Layout.ContentWidth;
        ContentHeight = frame.Layout.ContentHeight;

        if (_renderingBackend == WpfRenderingBackend.Direct2DD3DImage)
        {
            UpdateGpuSurfaceVisibility();
            _gpuSurface.SetDisplayList(frame.DisplayList);
            return;
        }

        _gpuSurface.SetDisplayList(null);
        UpdateGpuSurfaceVisibility();
        drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
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
        if (_session is null)
        {
            return;
        }
        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        var point = e.GetPosition(this);
        var scroll = _scrollController.Snapshot;
        if (!EnsureViewport().TryHitTest(point.X, point.Y, scroll.OffsetX, scroll.OffsetY, out var address))
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

        if (e.ClickCount >= 2)
        {
            BeginEdit();
        }
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_session is null || IsEditing)
        {
            return;
        }

        var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    e.Handled = _session.Undo();
                    break;
                case Key.Y:
                    e.Handled = _session.Redo();
                    break;
                case Key.C:
                    _session.Clipboard.CopyPrimarySelection();
                    e.Handled = true;
                    break;
                case Key.X:
                    e.Handled = _session.Clipboard.CutPrimarySelection();
                    break;
                case Key.V:
                    e.Handled = _session.Clipboard.PasteAtActiveCell();
                    break;
                case Key.B:
                    _session.Styles.ToggleBold();
                    e.Handled = true;
                    break;
                case Key.I:
                    _session.Styles.ToggleItalic();
                    e.Handled = true;
                    break;
            }
            if (e.Handled)
            {
                return;
            }
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = _session.ClearSelection();
            return;
        }
        if (e.Key == Key.F2)
        {
            BeginEdit();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            MoveActiveCell(1, 0, false);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab)
        {
            MoveActiveCell(0, (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1, false);
            e.Handled = true;
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

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (_session is null || IsEditing || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl))
        {
            return;
        }
        BeginEdit(e.Text);
        e.Handled = true;
    }

    public void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null)
        {
            return;
        }
        var state = _cellEditor.BeginEdit();
        _editor.Text = replacementText ?? state.InitialText;
        _editor.Visibility = Visibility.Visible;
        UpdateEditorBounds();
        _editor.Focus();
        if (replacementText is null)
        {
            _editor.SelectAll();
        }
        else
        {
            _editor.CaretIndex = _editor.Text.Length;
        }
    }

    public bool CommitEditor()
    {
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }
        HideEditor();
        Focus();
        return true;
    }

    public bool CancelEditor()
    {
        if (_cellEditor is null || !_cellEditor.Cancel())
        {
            return false;
        }
        HideEditor();
        Focus();
        return true;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        EnsureFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        UpdateEditorBounds();
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
        _cellEditor = value?.Editor;
        _scrollController.Reset();
        _framePacing.Reset();
        HideEditor();
        AttachSessionEvents();
        UpdateContentExtent();
        UpdateGpuSurfaceVisibility();
        InvalidateVisual();
    }

    private void AttachSessionEvents()
    {
        if (_session is null || _sessionEventsAttached)
        {
            return;
        }
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _sessionEventsAttached = true;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null && _sessionEventsAttached)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
        }
        _sessionEventsAttached = false;
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
        CancelEditor();
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _scrollController.Reset();
        UpdateContentExtent();
        InvalidateVisual();
    }

    private void OnSelectionChanged(object? sender, NeraSelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e) => InvalidateVisual();

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateContentExtent();
        UpdateEditorBounds();
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

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            if (CommitEditor())
            {
                MoveActiveCell(1, 0, false);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            if (CommitEditor())
            {
                MoveActiveCell(0, (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1, false);
            }
            e.Handled = true;
        }
    }

    private void UpdateEditorBounds()
    {
        if (_cellEditor?.State is not { } state || _viewport is null)
        {
            return;
        }
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(state.Address, scroll.OffsetX, scroll.OffsetY, out var bounds))
        {
            _editor.Visibility = Visibility.Collapsed;
            return;
        }
        var viewportRect = new Rect(0d, 0d, Math.Max(0d, ActualWidth), Math.Max(0d, ActualHeight));
        var candidate = new Rect(bounds.X, bounds.Y, Math.Max(20d, bounds.Width), Math.Max(18d, bounds.Height));
        _editor.Visibility = candidate.IntersectsWith(viewportRect) ? Visibility.Visible : Visibility.Collapsed;
        _editorBounds = candidate;
        InvalidateArrange();
    }

    private void HideEditor()
    {
        _editor.Visibility = Visibility.Collapsed;
        _editorBounds = Rect.Empty;
        InvalidateArrange();
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
            UpdateEditorBounds();
            InvalidateVisual();
        }
        if (!_scrollController.HasPendingMotion)
        {
            DetachFrameLoop();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachSessionEvents();
        UpdateGpuSurfaceVisibility();
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFrameLoop();
        DetachSessionEvents();
    }

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

    private void UpdateGpuSurfaceVisibility()
    {
        _gpuSurface.Visibility =
            _renderingBackend == WpfRenderingBackend.Direct2DD3DImage && _session is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
