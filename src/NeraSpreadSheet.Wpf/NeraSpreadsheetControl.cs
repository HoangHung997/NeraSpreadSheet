using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Foundation.Performance;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
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

public sealed partial class NeraSpreadsheetControl : FrameworkElement, IDisposable
{
    private readonly ContinuousScrollController _scrollController = new();
    private readonly WpfDisplayListRenderer _displayListRenderer = new();
    private readonly FramePacingMonitor _framePacing = new();
    private readonly VisualCollection _visuals;
    private readonly WpfDirect2DGpuSurface _gpuSurface;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetAnalyticsViewportInteractionController? _analyticsInput;
    private SpreadsheetCellEditorController? _cellEditor;
    private Worksheet? _subscribedWorksheet;
    private ViewportLayout? _lastLayout;
    private SpreadsheetHeaderResizeHandle? _headerResize;
    private TimeSpan? _lastRenderingTime;
    private bool _isFrameLoopAttached;
    private bool _sessionEventsAttached;
    private bool _disposed;
    private Rect _editorBounds = Rect.Empty;
    private Rect _editorClipBounds = Rect.Empty;
    private WpfRenderingBackend _renderingBackend;
    private bool _useAdaptiveNavigationExtent;
    private int _adaptiveNavigationTrailingRowCount =
        SpreadsheetViewportEngine.DefaultAdaptiveTrailingRowCount;
    private int _adaptiveNavigationTrailingColumnCount =
        SpreadsheetViewportEngine.DefaultAdaptiveTrailingColumnCount;
    private readonly ScaleTransform _zoomTransform = new(1d, 1d);
    private double _zoom = 1d;

    public const double MinimumZoom = 0.25d;

    public const double MaximumZoom = 4d;

    public NeraSpreadsheetControl()
    {
        Focusable = true;
        LayoutTransform = _zoomTransform;
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        _editor.PreviewKeyDown += OnEditorKeyDown;
        InitializeFormulaEditingUi();
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
    public SpreadsheetRenderTheme RenderTheme { get; set; } = new() { ShowHeaders = true };
    public double ContentWidth { get; private set; }
    public double ContentHeight { get; private set; }
    public double WheelPixelsPerNotch { get; set; } = 96d;
    public double OverscanPixels { get; set; } = 128d;
    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;
    public bool IsEditing => _cellEditor?.IsEditing == true;
    public FramePacingSnapshot FramePacing => _framePacing.Capture();

    public event EventHandler? ZoomChanged;

    /// <summary>
    /// Gets or sets the visual spreadsheet zoom where 1.0 represents 100%.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!double.IsFinite(value) ||
                value < MinimumZoom ||
                value > MaximumZoom)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Zoom must be between {MinimumZoom} and {MaximumZoom}.");
            }
            if (Math.Abs(_zoom - value) <= 1e-9)
            {
                return;
            }

            _zoom = value;
            _zoomTransform.ScaleX = value;
            _zoomTransform.ScaleY = value;
            if (!ReferenceEquals(LayoutTransform, _zoomTransform))
            {
                LayoutTransform = _zoomTransform;
            }
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Changes zoom in Excel-like ten percentage-point wheel steps.
    /// </summary>
    public void ZoomByWheel(int wheelDelta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (wheelDelta == 0)
        {
            return;
        }

        var percent = (int)Math.Round(_zoom * 100d / 10d) * 10;
        percent += Math.Sign(wheelDelta) * 10;
        Zoom = Math.Clamp(percent / 100d, MinimumZoom, MaximumZoom);
    }

    /// <summary>
    /// Gets or sets whether the scroll range follows the sparse used range and
    /// active cell instead of exposing the full physical worksheet extent.
    /// </summary>
    public bool UseAdaptiveNavigationExtent
    {
        get => _useAdaptiveNavigationExtent;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_useAdaptiveNavigationExtent == value)
            {
                return;
            }
            _useAdaptiveNavigationExtent = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets the minimum blank rows kept after used or navigated content
    /// when <see cref="UseAdaptiveNavigationExtent"/> is enabled.
    /// </summary>
    public int AdaptiveNavigationTrailingRowCount
    {
        get => _adaptiveNavigationTrailingRowCount;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_adaptiveNavigationTrailingRowCount == value)
            {
                return;
            }
            _adaptiveNavigationTrailingRowCount = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets the minimum blank columns kept after used or navigated
    /// content when <see cref="UseAdaptiveNavigationExtent"/> is enabled.
    /// </summary>
    public int AdaptiveNavigationTrailingColumnCount
    {
        get => _adaptiveNavigationTrailingColumnCount;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_adaptiveNavigationTrailingColumnCount == value)
            {
                return;
            }
            _adaptiveNavigationTrailingColumnCount = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            InvalidateVisual();
        }
    }

    public WpfRenderingBackend RenderingBackend
    {
        get => _renderingBackend;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
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
        _editor.Measure(
            _editor.Visibility == Visibility.Visible && !_editorBounds.IsEmpty
                ? _editorBounds.Size
                : new Size(0d, 0d));
        return new Size(0d, 0d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _gpuSurface.Arrange(new Rect(0d, 0d, finalSize.Width, finalSize.Height));
        if (_editor.Visibility == Visibility.Visible && !_editorBounds.IsEmpty)
        {
            _editor.Arrange(_editorBounds);
            _editor.Clip = _editorClipBounds.IsEmpty
                ? null
                : new RectangleGeometry(_editorClipBounds);
        }
        else
        {
            _editor.Arrange(new Rect(0d, 0d, 0d, 0d));
            _editor.Clip = null;
        }
        return finalSize;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        _lastLayout = null;
        UpdateContentExtent();
        ClampScrollToContentBounds();
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_disposed)
        {
            return;
        }
        _framePacing.RecordFrame();

        if (_session is null || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            _lastLayout = null;
            _gpuSurface.SetDisplayList(null);
            UpdateGpuSurfaceVisibility();
            drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
            return;
        }

        var chrome = GetChromeMetrics();
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            _lastLayout = null;
            _gpuSurface.SetDisplayList(null);
            drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
            return;
        }

        EnsureWorksheetSubscription();
        var viewport = EnsureViewport();
        var snapshot = _scrollController.Snapshot;
        var frame = viewport.Compose(
            snapshot.OffsetX,
            snapshot.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            OverscanPixels,
            RenderTheme);
        _lastLayout = frame.Layout;
        UpdateContentExtent(chrome);
        var bodyDisplayList = SpreadsheetFormulaReferenceDisplayListComposer.Compose(
            frame.DisplayList,
            frame.Layout,
            GetFormulaReferenceHighlights(),
            RenderTheme.FormulaReferenceStrokeWidth);
        var displayList = SpreadsheetChromeDisplayListComposer.Compose(
            bodyDisplayList,
            frame.Layout,
            _session.Selection.Capture(),
            RenderTheme);

        if (_renderingBackend == WpfRenderingBackend.Direct2DD3DImage)
        {
            UpdateGpuSurfaceVisibility();
            _gpuSurface.SetDisplayList(displayList);
            return;
        }

        _gpuSurface.SetDisplayList(null);
        UpdateGpuSurfaceVisibility();
        drawingContext.DrawRectangle(Background, null, new Rect(0d, 0d, ActualWidth, ActualHeight));
        _displayListRenderer.Render(drawingContext, displayList, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_disposed)
        {
            return;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ZoomByWheel(e.Delta);
            e.Handled = true;
            return;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            e.Handled = true;
            return;
        }
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
        if (_disposed || _session is null)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (TryBeginFormulaReferencePointer(point))
        {
            e.Handled = true;
            return;
        }
        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        if (TryBeginHeaderResize(point.X, point.Y))
        {
            e.Handled = true;
            return;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            point.X,
            point.Y,
            ActualWidth,
            ActualHeight,
            RenderTheme);
        var scroll = _scrollController.Snapshot;
        switch (hit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.RowHeader:
                if (EnsureViewport().TryHitTestRow(hit.BodyY, scroll.OffsetY, out var rowIndex))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        _session.Selection.ExtendRowsTo(rowIndex);
                    }
                    else
                    {
                        _session.Selection.SelectRow(rowIndex, additive: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                    }
                }
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (EnsureViewport().TryHitTestColumn(hit.BodyX, scroll.OffsetX, out var columnIndex))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        _session.Selection.ExtendColumnsTo(columnIndex);
                    }
                    else
                    {
                        _session.Selection.SelectColumn(columnIndex, additive: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                    }
                }
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        var viewport = EnsureViewport();
        if (_lastLayout is not null &&
            EnsureAnalyticsInput().PointerPressed(
                new PointD(hit.BodyX, hit.BodyY),
                _lastLayout))
        {
            CaptureMouse();
            Cursor = GetAnalyticsCursor(
                _session.AnalyticsInteraction.Snapshot.ActiveHandle);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!viewport.TryHitTest(hit.BodyX, hit.BodyY, scroll.OffsetX, scroll.OffsetY, out var address))
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (UpdateFormulaReferencePointer(point))
        {
            e.Handled = true;
            return;
        }
        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, point.X, point.Y);
            Cursor = GetResizeCursor(resize.Axis);
            e.Handled = true;
            return;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.PointerMoved(ToBodyPoint(point));
            Cursor = _session is null
                ? null
                : GetAnalyticsCursor(
                    _session.AnalyticsInteraction.Snapshot.ActiveHandle);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        UpdatePointerCursor(point.X, point.Y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (UpdateFormulaReferencePointer(point) &&
            EndFormulaReferencePointer())
        {
            e.Handled = true;
            return;
        }
        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, point.X, point.Y);
            _headerResize = null;
            ReleaseMouseCapture();
            UpdatePointerCursor(point.X, point.Y);
            e.Handled = true;
            return;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.PointerReleased(ToBodyPoint(point));
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            UpdatePointerCursor(point.X, point.Y);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        var changed = false;
        if (_formulaReferenceAnchor is not null)
        {
            _formulaReferenceAnchor = null;
            changed = true;
        }
        if (_headerResize is not null)
        {
            _headerResize = null;
            changed = true;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.Cancel();
            changed = true;
        }
        if (changed)
        {
            Cursor = null;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_disposed || _session is null || IsEditing)
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

        if (_session.AnalyticsInteraction.SelectedItem.HasValue)
        {
            var analyticsKey = e.Key switch
            {
                Key.Left => SpreadsheetAnalyticsKeyboardKey.Left,
                Key.Right => SpreadsheetAnalyticsKeyboardKey.Right,
                Key.Up => SpreadsheetAnalyticsKeyboardKey.Up,
                Key.Down => SpreadsheetAnalyticsKeyboardKey.Down,
                Key.Delete => SpreadsheetAnalyticsKeyboardKey.Delete,
                Key.Escape => SpreadsheetAnalyticsKeyboardKey.Escape,
                _ => (SpreadsheetAnalyticsKeyboardKey?)null,
            };
            if (analyticsKey.HasValue)
            {
                var modifiers = SpreadsheetAnalyticsKeyboardModifiers.None;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    modifiers |= SpreadsheetAnalyticsKeyboardModifiers.Shift;
                }
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    modifiers |= SpreadsheetAnalyticsKeyboardModifiers.Control;
                }

                e.Handled = EnsureAnalyticsInput().Keyboard(
                    analyticsKey.Value,
                    modifiers);
                if (e.Handled)
                {
                    return;
                }
            }
            if (e.Key == Key.F2)
            {
                e.Handled = true;
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
        if (_disposed ||
            _session is null ||
            IsEditing ||
            _session.AnalyticsInteraction.SelectedItem.HasValue ||
            string.IsNullOrEmpty(e.Text) ||
            e.Text.Any(char.IsControl))
        {
            return;
        }
        BeginEdit(e.Text);
        e.Handled = true;
    }

    public void BeginEdit(string? replacementText = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cellEditor is null)
        {
            return;
        }
        var state = _cellEditor.BeginEdit();
        WpfCellEditorStyle.Apply(
            _editor,
            _session!.ActiveWorksheet.GetEffectiveStyle(
                state.Address,
                _session.Workbook.Styles));
        _editor.Text = replacementText ?? state.InitialText;
        _formulaReferenceSpan = null;
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
        UpdateFormulaSuggestions(
            replacementText is null ? _editor.CaretIndex : _editor.Text.Length);
    }

    public bool CommitEditor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cellEditor?.State is { } target) _session!.Selection.SetActiveCell(target.Address);
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }
        HideEditor();
        ResetFormulaEditingUi();
        Focus();
        return true;
    }

    public bool CancelEditor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cellEditor?.State is { } target) _session!.Selection.SetActiveCell(target.Address);
        if (_cellEditor is null || !_cellEditor.Cancel())
        {
            return false;
        }
        HideEditor();
        ResetFormulaEditingUi();
        Focus();
        return true;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        EnsureFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var before = _scrollController.Snapshot;
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        UpdateContentExtent();
        UpdateEditorBounds();
        InvalidateVisual();
        if (!animated && before != _scrollController.Snapshot)
        {
            ScrollChanged?.Invoke(
                this,
                new ScrollChangedEventArgs(_scrollController.Snapshot));
        }
        if (animated)
        {
            EnsureFrameLoop();
        }
    }

    /// <summary>
    /// Scrolls only as far as needed to make a worksheet cell visible while
    /// preserving continuous pixel offsets and frozen panes.
    /// </summary>
    public bool ScrollCellIntoView(CellAddress address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is null || _viewport is null)
        {
            return false;
        }

        UpdateContentExtent();
        var chrome = GetChromeMetrics();
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(
                address,
                scroll.OffsetX,
                scroll.OffsetY,
                out var bounds))
        {
            return false;
        }

        var frozen = _viewport.GetFrozenPaneExtent();
        var nextX = scroll.OffsetX;
        var nextY = scroll.OffsetY;
        if (address.ColumnIndex >= _session.View.FrozenColumns)
        {
            var visibleLeft = Math.Clamp(frozen.Width, 0d, chrome.BodyWidth);
            if (bounds.Left < visibleLeft)
            {
                nextX -= visibleLeft - bounds.Left;
            }
            else if (bounds.Right > chrome.BodyWidth)
            {
                nextX += bounds.Right - chrome.BodyWidth;
            }
        }
        if (address.RowIndex >= _session.View.FrozenRows)
        {
            var visibleTop = Math.Clamp(frozen.Height, 0d, chrome.BodyHeight);
            if (bounds.Top < visibleTop)
            {
                nextY -= visibleTop - bounds.Top;
            }
            else if (bounds.Bottom > chrome.BodyHeight)
            {
                nextY += bounds.Bottom - chrome.BodyHeight;
            }
        }

        nextX = Math.Clamp(
            nextX,
            0d,
            Math.Max(0d, ContentWidth - chrome.BodyWidth));
        nextY = Math.Clamp(
            nextY,
            0d,
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        if (Math.Abs(nextX - scroll.OffsetX) <= 1e-9 &&
            Math.Abs(nextY - scroll.OffsetY) <= 1e-9)
        {
            return false;
        }

        ScrollTo(nextX, nextY, animated: false);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        DetachFrameLoop();
        DetachSessionEvents();
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _editor.PreviewKeyDown -= OnEditorKeyDown;
        DisposeFormulaEditingUi();
        _gpuSurface.Dispose();
        _disposed = true;
    }

    private SpreadsheetChromeMetrics GetChromeMetrics() =>
        SpreadsheetChromeGeometry.Calculate(ActualWidth, ActualHeight, RenderTheme);

    private PointD ToBodyPoint(Point point)
    {
        var chrome = GetChromeMetrics();
        return new PointD(
            point.X - chrome.RowHeaderWidth,
            point.Y - chrome.ColumnHeaderHeight);
    }

    private bool TryBeginHeaderResize(double x, double y)
    {
        if (_lastLayout is null ||
            !SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                x,
                y,
                ActualWidth,
                ActualHeight,
                RenderTheme,
                _lastLayout,
                out var resize))
        {
            return false;
        }

        _headerResize = resize;
        CaptureMouse();
        Cursor = GetResizeCursor(resize.Axis);
        return true;
    }

    private void ApplyHeaderResize(SpreadsheetHeaderResizeHandle resize, double x, double y)
    {
        if (_session is null)
        {
            return;
        }

        var size = SpreadsheetHeaderResizeGeometry.CalculateSize(resize, x, y);
        if (resize.Axis == WorksheetAxis.Row)
        {
            _session.ActiveWorksheet.Dimensions.SetRowHeight(resize.Index, size);
        }
        else
        {
            _session.ActiveWorksheet.Dimensions.SetColumnWidth(resize.Index, size);
        }
    }

    private void UpdatePointerCursor(double x, double y)
    {
        if (_lastLayout is not null &&
            SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                x,
                y,
                ActualWidth,
                ActualHeight,
                RenderTheme,
                _lastLayout,
                out var resize))
        {
            Cursor = GetResizeCursor(resize.Axis);
            return;
        }
        if (_session is null || _lastLayout is null || _viewport is null)
        {
            Cursor = null;
            return;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            x,
            y,
            ActualWidth,
            ActualHeight,
            RenderTheme);
        if (hit.Region != SpreadsheetChromeRegion.Body)
        {
            Cursor = null;
            return;
        }

        var analyticsHit = SpreadsheetAnalyticsHitTester.HitTest(
            _viewport.GetAnalyticsInteractionTargets(_lastLayout),
            new PointD(hit.BodyX, hit.BodyY),
            _session.AnalyticsInteraction.SelectedItem);
        Cursor = analyticsHit.HasValue
            ? GetAnalyticsCursor(analyticsHit.Value.Handle)
            : null;
    }

    private static Cursor GetResizeCursor(WorksheetAxis axis) =>
        axis == WorksheetAxis.Row ? Cursors.SizeNS : Cursors.SizeWE;

    private static Cursor? GetAnalyticsCursor(
        SpreadsheetAnalyticsResizeHandle handle) =>
        handle switch
        {
            SpreadsheetAnalyticsResizeHandle.Move => Cursors.SizeAll,
            SpreadsheetAnalyticsResizeHandle.North or
                SpreadsheetAnalyticsResizeHandle.South => Cursors.SizeNS,
            SpreadsheetAnalyticsResizeHandle.East or
                SpreadsheetAnalyticsResizeHandle.West => Cursors.SizeWE,
            SpreadsheetAnalyticsResizeHandle.NorthWest or
                SpreadsheetAnalyticsResizeHandle.SouthEast => Cursors.SizeNWSE,
            SpreadsheetAnalyticsResizeHandle.NorthEast or
                SpreadsheetAnalyticsResizeHandle.SouthWest => Cursors.SizeNESW,
            _ => null,
        };

    private SpreadsheetViewportEngine EnsureViewport() => _viewport ??= new SpreadsheetViewportEngine(
        _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private SpreadsheetAnalyticsViewportInteractionController EnsureAnalyticsInput() =>
        _analyticsInput ??= new SpreadsheetAnalyticsViewportInteractionController(
            EnsureViewport());

    private void SetSession(SpreadsheetSession? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_session, value))
        {
            return;
        }

        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetViewportEngine(value);
        _analyticsInput = _viewport is null
            ? null
            : new SpreadsheetAnalyticsViewportInteractionController(_viewport);
        _cellEditor = value?.Editor;
        _lastLayout = null;
        _headerResize = null;
        _scrollController.Reset();
        _framePacing.Reset();
        HideEditor();
        ResetFormulaEditingUi();
        AttachSessionEvents();
        UpdateContentExtent();
        UpdateGpuSurfaceVisibility();
        InvalidateVisual();
    }

    private void AttachSessionEvents()
    {
        if (_session is null || _sessionEventsAttached || _disposed)
        {
            return;
        }
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged;
        _session.Analytics.Changed += OnAnalyticsChanged;
        _session.AnalyticsPlacements.Changed += OnAnalyticsPlacementChanged;
        _session.AnalyticsInteraction.Changed += OnAnalyticsInteractionChanged;
        _sessionEventsAttached = true;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null && _sessionEventsAttached)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged;
            _session.Analytics.Changed -= OnAnalyticsChanged;
            _session.AnalyticsPlacements.Changed -= OnAnalyticsPlacementChanged;
            _session.AnalyticsInteraction.Changed -= OnAnalyticsInteractionChanged;
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
        if (_disposed)
        {
            return;
        }
        CancelEditor();
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _lastLayout = null;
        _headerResize = null;
        _scrollController.Reset();
        UpdateContentExtent();
        InvalidateVisual();
    }

    private void OnViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (_disposed || _session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _viewport?.ClearDisplayListCache();
        _lastLayout = null;
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnSelectionChanged(object? sender, NeraSelectionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (_useAdaptiveNavigationExtent)
        {
            UpdateContentExtent();
            ClampScrollToContentBounds();
        }
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnAnalyticsChanged(
        object? sender,
        SpreadsheetAnalyticsChangedEventArgs e)
    {
        if (!_disposed &&
            _session is not null &&
            ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            InvalidateVisual();
        }
    }

    private void OnAnalyticsPlacementChanged(
        object? sender,
        SpreadsheetAnalyticsPlacementChangedEventArgs e)
    {
        if (!_disposed &&
            _session is not null &&
            ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            InvalidateVisual();
        }
    }

    private void OnAnalyticsInteractionChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            InvalidateVisual();
        }
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (_useAdaptiveNavigationExtent)
        {
            UpdateContentExtent();
            ClampScrollToContentBounds();
        }
        InvalidateVisual();
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        _viewport?.InvalidateMetrics();
        _lastLayout = null;
        UpdateContentExtent();
        ClampScrollToContentBounds();
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
        UpdateContentExtent(GetChromeMetrics());
    }

    private void UpdateContentExtent(SpreadsheetChromeMetrics chrome)
    {
        if (_viewport is null)
        {
            ContentWidth = 0d;
            ContentHeight = 0d;
            return;
        }
        var extent = _useAdaptiveNavigationExtent && _session is not null
            ? _viewport.GetAdaptiveNavigationExtent(
                _session.Selection.ActiveCell,
                new SizeD(chrome.BodyWidth, chrome.BodyHeight),
                new PointD(
                    _scrollController.Snapshot.OffsetX,
                    _scrollController.Snapshot.OffsetY),
                _adaptiveNavigationTrailingRowCount,
                _adaptiveNavigationTrailingColumnCount)
            : _viewport.GetContentExtent();
        ContentWidth = extent.Width;
        ContentHeight = extent.Height;
    }

    private void ClampScrollToContentBounds()
    {
        if (_viewport is null)
        {
            return;
        }
        var chrome = GetChromeMetrics();
        var snapshot = _scrollController.Snapshot;
        var nextX = Math.Clamp(
            snapshot.OffsetX,
            0d,
            Math.Max(0d, ContentWidth - chrome.BodyWidth));
        var nextY = Math.Clamp(
            snapshot.OffsetY,
            0d,
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        if (Math.Abs(nextX - snapshot.OffsetX) > 1e-9 ||
            Math.Abs(nextY - snapshot.OffsetY) > 1e-9)
        {
            ScrollTo(nextX, nextY, animated: false);
        }
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null)
        {
            return;
        }
        var active = _session.Selection.ActiveCell;
        var next = SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
            _session.ActiveWorksheet,
            active,
            rowDelta,
            columnDelta);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
        ScrollCellIntoView(next);
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (TryHandleFormulaSuggestionKey(e))
        {
            e.Handled = true;
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Enter or Key.Return)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                var insertionStart = _editor.SelectionStart;
                _editor.SelectedText = Environment.NewLine;
                _editor.CaretIndex = insertionStart + Environment.NewLine.Length;
                e.Handled = true;
                return;
            }
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
        if (_cellEditor?.State is not { } state || _viewport is null || _session is null)
        {
            return;
        }
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(state.Address, scroll.OffsetX, scroll.OffsetY, out var bounds))
        {
            _editor.Visibility = Visibility.Collapsed;
            _editorBounds = Rect.Empty;
            _editorClipBounds = Rect.Empty;
            InvalidateMeasure();
            InvalidateArrange();
            return;
        }

        var chrome = GetChromeMetrics();
        var viewportRect = new Rect(0d, 0d, Math.Max(0d, ActualWidth), Math.Max(0d, ActualHeight));
        var candidate = new Rect(
            chrome.RowHeaderWidth + bounds.X,
            chrome.ColumnHeaderHeight + bounds.Y,
            Math.Max(20d, bounds.Width),
            Math.Max(18d, bounds.Height));
        var frozen = _viewport.GetFrozenPaneExtent();
        var frozenWidth = Math.Clamp(frozen.Width, 0d, chrome.BodyWidth);
        var frozenHeight = Math.Clamp(frozen.Height, 0d, chrome.BodyHeight);
        var frozenColumn = state.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = state.Address.RowIndex < _session.View.FrozenRows;
        var paneLeft = chrome.RowHeaderWidth + (frozenColumn ? 0d : frozenWidth);
        var paneTop = chrome.ColumnHeaderHeight + (frozenRow ? 0d : frozenHeight);
        var paneRight = chrome.RowHeaderWidth + (frozenColumn ? frozenWidth : chrome.BodyWidth);
        var paneBottom = chrome.ColumnHeaderHeight + (frozenRow ? frozenHeight : chrome.BodyHeight);
        var pane = new Rect(
            paneLeft,
            paneTop,
            Math.Max(0d, paneRight - paneLeft),
            Math.Max(0d, paneBottom - paneTop));
        var visible = Rect.Intersect(Rect.Intersect(candidate, pane), viewportRect);
        if (visible.IsEmpty || visible.Width <= 0d || visible.Height <= 0d)
        {
            _editor.Visibility = Visibility.Collapsed;
            _editorBounds = Rect.Empty;
            InvalidateArrange();
            return;
        }

        _editor.Visibility = Visibility.Visible;
        _editorBounds = candidate;
        _editorClipBounds = new Rect(
            visible.X - candidate.X,
            visible.Y - candidate.Y,
            visible.Width,
            visible.Height);
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void HideEditor()
    {
        _editor.Visibility = Visibility.Collapsed;
        _editorBounds = Rect.Empty;
        _editorClipBounds = Rect.Empty;
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void EnsureFrameLoop()
    {
        if (_isFrameLoopAttached || _disposed)
        {
            return;
        }
        CompositionTarget.Rendering += OnRendering;
        _isFrameLoopAttached = true;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_disposed || e is not RenderingEventArgs renderingEventArgs)
        {
            return;
        }
        var currentTime = renderingEventArgs.RenderingTime;
        var elapsed = _lastRenderingTime is null
            ? TimeSpan.FromSeconds(1d / 60d)
            : currentTime - _lastRenderingTime.Value;
        _lastRenderingTime = currentTime;
        var chrome = GetChromeMetrics();
        var bounds = new ScrollBounds(
            Math.Max(0d, ContentWidth - chrome.BodyWidth),
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);
        if (result.Changed)
        {
            UpdateContentExtent(chrome);
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
        if (_disposed)
        {
            return;
        }
        AttachSessionEvents();
        UpdateGpuSurfaceVisibility();
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
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
            !_disposed && _renderingBackend == WpfRenderingBackend.Direct2DD3DImage && _session is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
