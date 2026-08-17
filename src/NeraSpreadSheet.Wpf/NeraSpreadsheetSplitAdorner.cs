using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;
using NeraSelectionChangedEventArgs = NeraSpreadSheet.Interaction.SelectionChangedEventArgs;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner, IDisposable
{
    private const double GeometryEpsilon = 1e-9;
    private readonly NeraSpreadsheetControl _owner;
    private readonly VisualCollection _visuals;
    private readonly WpfDisplayListRenderer _displayListRenderer = new();
    private readonly WpfDirect2DGpuSurface _gpuSurface;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private Worksheet? _subscribedWorksheet;
    private SpreadsheetSplitViewportEngine? _engine;
    private SpreadsheetCellEditorController? _cellEditor;
    private SpreadsheetSplitViewportFrame? _lastFrame;
    private TimeSpan? _lastRenderingTime;
    private SplitDragState? _splitDrag;
    private Rect _editorBounds = Rect.Empty;
    private SpreadsheetSplitPaneMode _mode;
    private double? _splitX;
    private double? _splitY;
    private double _separatorThickness = 6d;
    private double _minimumPaneExtent = 64d;
    private WpfRenderingBackend _activeBackend;
    private bool _isFrameLoopAttached;
    private bool _sessionEventsAttached;
    private bool _disposed;

    internal NeraSpreadsheetSplitAdorner(NeraSpreadsheetControl owner)
        : base(owner ?? throw new ArgumentNullException(nameof(owner)))
    {
        _owner = owner;
        _activeBackend = owner.RenderingBackend;
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;
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

    internal SpreadsheetSplitPaneMode Mode => _mode;

    internal double? SplitX => _splitX;

    internal double? SplitY => _splitY;

    internal double SeparatorThickness
    {
        get => _separatorThickness;
        set
        {
            var validated = Guard.PositiveFinite(value, nameof(value));
            if (Math.Abs(_separatorThickness - validated) <= GeometryEpsilon)
            {
                return;
            }
            _separatorThickness = validated;
            InvalidateSplitLayout();
        }
    }

    internal double MinimumPaneExtent
    {
        get => _minimumPaneExtent;
        set
        {
            var validated = Guard.PositiveFinite(value, nameof(value));
            if (Math.Abs(_minimumPaneExtent - validated) <= GeometryEpsilon)
            {
                return;
            }
            _minimumPaneExtent = validated;
            InvalidateSplitLayout();
        }
    }

    internal SpreadsheetPaneId ActivePane => _engine?.ActivePane ?? SpreadsheetPaneId.TopLeft;

    internal SpreadsheetSplitViewportFrame? LastFrame => _lastFrame;

    internal WpfGpuRendererDiagnostics? GpuDiagnostics =>
        _activeBackend == WpfRenderingBackend.Direct2DD3DImage
            ? new WpfGpuRendererDiagnostics(
                _gpuSurface.TextureWidth,
                _gpuSurface.TextureHeight,
                _gpuSurface.CachedTextLayoutCount,
                _gpuSurface.TextLayoutCacheHits,
                _gpuSurface.TextLayoutCacheMisses,
                _gpuSurface.TextLayoutCacheEvictions)
            : null;

    internal event EventHandler<SpreadsheetSplitChangedEventArgs>? SplitChanged;

    internal event EventHandler<SpreadsheetPaneScrollChangedEventArgs>? PaneScrollChanged;

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size MeasureOverride(Size constraint)
    {
        var size = _owner.RenderSize;
        _gpuSurface.Measure(size);
        _editor.Measure(size);
        return size;
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
            _editor.Arrange(Rect.Empty);
        }
        return finalSize;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        _lastFrame = null;
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_disposed)
        {
            return;
        }

        var displayList = ComposeDisplayList();
        if (displayList is null)
        {
            _gpuSurface.SetDisplayList(null);
            UpdateGpuSurfaceVisibility();
            drawingContext.DrawRectangle(
                _owner.Background,
                null,
                new Rect(0d, 0d, ActualWidth, ActualHeight));
            return;
        }

        if (_activeBackend == WpfRenderingBackend.Direct2DD3DImage)
        {
            UpdateGpuSurfaceVisibility();
            _gpuSurface.SetDisplayList(displayList);
            return;
        }

        _gpuSurface.SetDisplayList(null);
        UpdateGpuSurfaceVisibility();
        drawingContext.DrawRectangle(
            _owner.Background,
            null,
            new Rect(0d, 0d, ActualWidth, ActualHeight));
        _displayListRenderer.Render(
            drawingContext,
            displayList,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    internal void NotifyOwnerStateChanged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SynchronizeSession();
        SynchronizeBackend();
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    internal void SetMode(SpreadsheetSplitPaneMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var chrome = GetChromeMetrics();
        var centeredX = Math.Max(0d, (chrome.BodyWidth - _separatorThickness) / 2d);
        var centeredY = Math.Max(0d, (chrome.BodyHeight - _separatorThickness) / 2d);
        SetSplitCore(
            mode,
            mode is SpreadsheetSplitPaneMode.Vertical or SpreadsheetSplitPaneMode.Both
                ? _splitX ?? centeredX
                : null,
            mode is SpreadsheetSplitPaneMode.Horizontal or SpreadsheetSplitPaneMode.Both
                ? _splitY ?? centeredY
                : null);
    }

    internal void SetSplit(double? splitX, double? splitY)
    {
        ValidateSplitCoordinate(splitX, nameof(splitX));
        ValidateSplitCoordinate(splitY, nameof(splitY));
        var mode = (splitX, splitY) switch
        {
            (not null, not null) => SpreadsheetSplitPaneMode.Both,
            (not null, null) => SpreadsheetSplitPaneMode.Vertical,
            (null, not null) => SpreadsheetSplitPaneMode.Horizontal,
            _ => SpreadsheetSplitPaneMode.None,
        };
        SetSplitCore(mode, splitX, splitY);
    }

    internal void SetActivePane(SpreadsheetPaneId paneId)
    {
        GetEngine().SetActivePane(paneId);
        _lastFrame = null;
        InvalidateVisual();
    }

    internal PointD GetPaneScroll(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScroll(paneId) ?? default;

    internal ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScrollSnapshot(paneId) ?? default;

    internal void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated)
    {
        GetEngine().ScrollPaneTo(paneId, offsetX, offsetY, animated);
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
        if (animated)
        {
            EnsureFrameLoop();
        }
    }

    internal void QueuePaneScroll(SpreadsheetPaneId paneId, ScrollDelta delta)
    {
        GetEngine().QueuePaneScroll(paneId, delta);
        EnsureFrameLoop();
    }

    internal void QueueActivePaneScroll(ScrollDelta delta)
    {
        GetEngine().QueueActivePaneScroll(delta);
        EnsureFrameLoop();
    }

    internal bool TryHitTest(
        double controlX,
        double controlY,
        out SpreadsheetPaneId paneId,
        out CellAddress address)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            paneId = default;
            address = default;
            return false;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            controlX,
            controlY,
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme);
        if (hit.Region != SpreadsheetChromeRegion.Body)
        {
            paneId = default;
            address = default;
            return false;
        }

        return GetEngine().TryHitTest(hit.BodyX, hit.BodyY, out paneId, out address);
    }

    internal void RenderNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SynchronizeSession();
        SynchronizeBackend();
        var displayList = ComposeDisplayList();
        if (_activeBackend == WpfRenderingBackend.Direct2DD3DImage)
        {
            _gpuSurface.SetDisplayList(displayList);
            UpdateGpuSurfaceVisibility();
        }
        InvalidateVisual();
        UpdateLayout();
        Dispatcher.Invoke(DispatcherPriority.Render, static () => { });
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
        _editor.KeyDown -= OnEditorKeyDown;
        _gpuSurface.Dispose();
        _disposed = true;
    }

    private DisplayList? ComposeDisplayList()
    {
        SynchronizeSession();
        SynchronizeBackend();
        var frame = EnsureFrame();
        if (frame is null || _session is null)
        {
            return null;
        }

        var paneLayouts = new List<SpreadsheetSplitPaneChromeLayout>(frame.Panes.Count);
        foreach (var pane in frame.Panes)
        {
            paneLayouts.Add(new SpreadsheetSplitPaneChromeLayout(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ViewportFrame.Layout));
        }

        return SpreadsheetSplitChromeDisplayListComposer.Compose(
            frame.DisplayList,
            frame.Layout,
            paneLayouts,
            _session.Selection.Capture(),
            _owner.RenderTheme);
    }

    private SpreadsheetSplitViewportEngine GetEngine()
    {
        SynchronizeSession();
        return _engine ?? throw new InvalidOperationException(
            "A spreadsheet session is required before split-pane operations can run.");
    }

    private SpreadsheetSplitViewportFrame? EnsureFrame()
    {
        SynchronizeSession();
        if (_engine is null || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            _lastFrame = null;
            return null;
        }

        var chrome = GetChromeMetrics();
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            _lastFrame = null;
            return null;
        }

        _lastFrame = _engine.Compose(
            new SpreadsheetSplitRequest(
                new SizeD(chrome.BodyWidth, chrome.BodyHeight),
                _splitX,
                _splitY,
                _separatorThickness,
                _minimumPaneExtent),
            _owner.OverscanPixels,
            _owner.RenderTheme);
        return _lastFrame;
    }

    private SpreadsheetChromeMetrics GetChromeMetrics() =>
        SpreadsheetChromeGeometry.Calculate(ActualWidth, ActualHeight, _owner.RenderTheme);

    private void SetSplitCore(
        SpreadsheetSplitPaneMode mode,
        double? splitX,
        double? splitY)
    {
        if (_mode == mode && _splitX == splitX && _splitY == splitY)
        {
            return;
        }

        _mode = mode;
        _splitX = splitX;
        _splitY = splitY;
        InvalidateSplitLayout();
        SplitChanged?.Invoke(
            this,
            new SpreadsheetSplitChangedEventArgs(mode, splitX, splitY, _lastFrame?.Layout));
    }

    private void InvalidateSplitLayout()
    {
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void SynchronizeBackend()
    {
        var next = _owner.RenderingBackend;
        if (_activeBackend == next)
        {
            return;
        }
        _activeBackend = next;
        _lastFrame = null;
        UpdateGpuSurfaceVisibility();
    }

    private void UpdateGpuSurfaceVisibility()
    {
        _gpuSurface.Visibility =
            !_disposed && _activeBackend == WpfRenderingBackend.Direct2DD3DImage && _session is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static void ValidateSplitCoordinate(double? value, string parameterName)
    {
        if (value is { } coordinate && !double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Split coordinates must be finite.");
        }
    }

    private readonly record struct SeparatorHit(bool Vertical, bool Horizontal);

    private readonly record struct SplitDragState(
        bool Vertical,
        bool Horizontal,
        double GrabOffsetX,
        double GrabOffsetY);
}
