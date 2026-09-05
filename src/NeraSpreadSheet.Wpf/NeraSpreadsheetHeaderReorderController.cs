using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

public sealed class NeraSpreadsheetHeaderReorderController : IDisposable
{
    private const double GeometryEpsilon = 1e-9;
    private NeraSpreadsheetControl? _owner;
    private SpreadsheetSession? _observedSession;
    private SpreadsheetViewportEngine? _viewport;
    private HeaderReorderState? _state;
    private SpreadsheetSplitHeaderReorderDropTarget? _dropTarget;
    private HeaderReorderPreviewAdorner? _previewAdorner;
    private AdornerLayer? _adornerLayer;
    private TimeSpan? _lastAutoScrollRenderingTime;
    private double _pointerX;
    private double _pointerY;
    private bool _autoScrollAttached;
    private bool _ownsMouseCapture;
    private bool _completing;

    internal NeraSpreadsheetHeaderReorderController(
        NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        owner.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        owner.PreviewMouseMove += OnPreviewMouseMove;
        owner.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        owner.LostMouseCapture += OnLostMouseCapture;
        owner.Loaded += OnOwnerLoaded;
        owner.Unloaded += OnOwnerUnloaded;
        owner.SizeChanged += OnOwnerSizeChanged;
        AttachIfPossible();
    }

    public bool IsDisposed => _owner is null;

    public bool IsAttached => _adornerLayer is not null;

    public bool IsDragging => _state is { IsActive: true };

    public bool IsAutoScrolling =>
        IsDragging &&
        !SpreadsheetHeaderReorderAutoScroll.IsZero(AutoScrollVelocity);

    public SpreadsheetSplitHeaderReorderDropTarget? DropTarget => _dropTarget;

    public PointD AutoScrollVelocity
    {
        get
        {
            if (_state is not { IsActive: true } state || _owner is null)
            {
                return default;
            }

            return SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
                state.Axis,
                _pointerX,
                _pointerY,
                _owner.ActualWidth,
                _owner.ActualHeight,
                _owner.RenderTheme);
        }
    }

    public void Dispose()
    {
        var owner = _owner;
        if (owner is null)
        {
            return;
        }

        Cancel();
        owner.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        owner.PreviewMouseMove -= OnPreviewMouseMove;
        owner.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        owner.LostMouseCapture -= OnLostMouseCapture;
        owner.Loaded -= OnOwnerLoaded;
        owner.Unloaded -= OnOwnerUnloaded;
        owner.SizeChanged -= OnOwnerSizeChanged;
        DetachPreviewAdorner();
        _owner = null;
        NeraSpreadsheetHeaderReorderExtensions.Remove(owner, this);
        GC.SuppressFinalize(this);
    }

    private void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var owner = _owner;
        if (owner?.Session is null ||
            owner.TryGetSplitPaneController(out _))
        {
            return;
        }

        var point = e.GetPosition(owner);
        if (!TryCreatePaneLayout(out var paneLayout, out var layout) ||
            SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                point.X,
                point.Y,
                owner.ActualWidth,
                owner.ActualHeight,
                owner.RenderTheme,
                layout,
                out _) ||
            !SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
                point.X,
                point.Y,
                owner.ActualWidth,
                owner.ActualHeight,
                owner.RenderTheme,
                paneLayout,
                out var source))
        {
            return;
        }

        var (sourceIndex, count) = ResolveSourceRange(
            owner.Session,
            source.Axis,
            source.Index);
        _state = new HeaderReorderState(
            source.Axis,
            sourceIndex,
            count,
            new PointD(point.X, point.Y),
            IsActive: false);
        _pointerX = point.X;
        _pointerY = point.Y;
        _dropTarget = null;
        UpdatePreview();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var owner = _owner;
        if (owner is null || _state is null)
        {
            return;
        }

        var point = e.GetPosition(owner);
        _pointerX = point.X;
        _pointerY = point.Y;
        if (!UpdateDrag(e.LeftButton == MouseButtonState.Pressed))
        {
            return;
        }

        owner.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        var owner = _owner;
        if (owner is null || _state is null)
        {
            return;
        }

        var point = e.GetPosition(owner);
        _pointerX = point.X;
        _pointerY = point.Y;
        var wasActive = _state is { IsActive: true };
        Complete();
        e.Handled = wasActive;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_completing ||
            !_ownsMouseCapture ||
            _state is not { IsActive: true })
        {
            return;
        }

        _ownsMouseCapture = false;
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            Cancel();
        }
    }

    private void OnOwnerLoaded(object sender, RoutedEventArgs e) =>
        AttachIfPossible();

    private void OnOwnerUnloaded(object sender, RoutedEventArgs e) =>
        DetachPreviewAdorner();

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_state is { IsActive: true })
        {
            UpdateDropTarget();
        }
    }

    private bool UpdateDrag(bool leftButtonPressed)
    {
        if (_state is not { } state)
        {
            return false;
        }
        if (!leftButtonPressed)
        {
            Cancel();
            return false;
        }

        if (!state.IsActive)
        {
            if (!SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                    state.StartPoint,
                    new PointD(_pointerX, _pointerY)))
            {
                return false;
            }

            state = state with { IsActive = true };
            _state = state;
            TryCaptureMouse();
        }

        UpdateDropTarget();
        UpdateAutoScrollSubscription();
        return true;
    }

    private void UpdateDropTarget()
    {
        if (_state is not { IsActive: true } state ||
            _owner is null ||
            !TryCreatePaneLayout(out var paneLayout, out _))
        {
            _dropTarget = null;
            UpdatePreview();
            return;
        }

        if (SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
                state.Axis,
                state.SourceIndex,
                state.Count,
                _pointerX,
                _pointerY,
                _owner.ActualWidth,
                _owner.ActualHeight,
                _owner.RenderTheme,
                paneLayout,
                out var target))
        {
            _dropTarget = target;
        }
        else
        {
            _dropTarget = null;
        }
        UpdatePreview();
    }

    private void Complete()
    {
        if (_state is not { } state)
        {
            return;
        }

        var wasActive = state.IsActive;
        if (wasActive)
        {
            UpdateDropTarget();
        }
        var target = _dropTarget;
        var session = _owner?.Session;

        _completing = true;
        try
        {
            ClearState(releaseCapture: true);
        }
        finally
        {
            _completing = false;
        }

        if (!wasActive || target is not { IsNoOp: false } || session is null)
        {
            return;
        }

        try
        {
            session.Reorder.Move(target.Value.Move);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    private void Cancel() => ClearState(releaseCapture: true);

    private void ClearState(bool releaseCapture)
    {
        var owner = _owner;
        var ownsCapture = _ownsMouseCapture;
        _state = null;
        _dropTarget = null;
        _ownsMouseCapture = false;
        DetachAutoScroll();
        UpdatePreview();
        if (releaseCapture &&
            ownsCapture &&
            owner is { IsMouseCaptured: true })
        {
            owner.ReleaseMouseCapture();
        }
        if (owner is not null)
        {
            owner.Cursor = null;
        }
    }

    private void TryCaptureMouse()
    {
        var owner = _owner;
        if (owner is null ||
            _ownsMouseCapture ||
            Mouse.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!owner.IsMouseCaptured && !owner.CaptureMouse())
        {
            return;
        }
        _ownsMouseCapture = owner.IsMouseCaptured;
    }

    private void UpdateAutoScrollSubscription()
    {
        if (!IsAutoScrolling)
        {
            DetachAutoScroll();
            return;
        }
        if (_autoScrollAttached)
        {
            return;
        }

        _lastAutoScrollRenderingTime = null;
        CompositionTarget.Rendering += OnAutoScrollRendering;
        _autoScrollAttached = true;
    }

    private void DetachAutoScroll()
    {
        if (!_autoScrollAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnAutoScrollRendering;
        _autoScrollAttached = false;
        _lastAutoScrollRenderingTime = null;
    }

    private void OnAutoScrollRendering(object? sender, EventArgs e)
    {
        var owner = _owner;
        if (owner is null ||
            _state is not { IsActive: true } ||
            e is not RenderingEventArgs rendering)
        {
            DetachAutoScroll();
            return;
        }

        var velocity = AutoScrollVelocity;
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            DetachAutoScroll();
            return;
        }

        var elapsed = _lastAutoScrollRenderingTime is null
            ? TimeSpan.FromSeconds(1d / 60d)
            : rendering.RenderingTime - _lastAutoScrollRenderingTime.Value;
        _lastAutoScrollRenderingTime = rendering.RenderingTime;
        if (elapsed > TimeSpan.FromMilliseconds(100d))
        {
            elapsed = TimeSpan.FromMilliseconds(100d);
        }

        var delta = SpreadsheetHeaderReorderAutoScroll.CalculateDelta(
            velocity,
            elapsed);
        var chrome = SpreadsheetChromeGeometry.Calculate(
            owner.ActualWidth,
            owner.ActualHeight,
            owner.RenderTheme);
        var snapshot = owner.ScrollSnapshot;
        var maximumX = Math.Max(0d, owner.ContentWidth - chrome.BodyWidth);
        var maximumY = Math.Max(0d, owner.ContentHeight - chrome.BodyHeight);
        var nextX = Math.Clamp(snapshot.OffsetX + delta.X, 0d, maximumX);
        var nextY = Math.Clamp(snapshot.OffsetY + delta.Y, 0d, maximumY);
        if (Math.Abs(nextX - snapshot.OffsetX) <= GeometryEpsilon &&
            Math.Abs(nextY - snapshot.OffsetY) <= GeometryEpsilon)
        {
            return;
        }

        owner.ScrollTo(nextX, nextY, animated: false);
        UpdateDropTarget();
    }

    private bool TryCreatePaneLayout(
        out SpreadsheetSplitPaneChromeLayout[] paneLayout,
        out NeraSpreadSheet.Layout.ViewportLayout layout)
    {
        paneLayout = [];
        layout = null!;
        var owner = _owner;
        var session = owner?.Session;
        if (owner is null ||
            session is null ||
            owner.ActualWidth <= 0d ||
            owner.ActualHeight <= 0d)
        {
            return false;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            owner.ActualWidth,
            owner.ActualHeight,
            owner.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return false;
        }

        if (!ReferenceEquals(_observedSession, session))
        {
            _observedSession = session;
            _viewport = new SpreadsheetViewportEngine(session);
        }
        _viewport!.InvalidateMetrics();
        var scroll = owner.ScrollSnapshot;
        var frame = _viewport.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            owner.OverscanPixels,
            owner.RenderTheme);
        layout = frame.Layout;
        paneLayout =
        [
            new SpreadsheetSplitPaneChromeLayout(
                SpreadsheetPaneId.TopLeft,
                new RectD(0d, 0d, chrome.BodyWidth, chrome.BodyHeight),
                layout),
        ];
        return true;
    }

    private static (int SourceIndex, int Count) ResolveSourceRange(
        SpreadsheetSession session,
        WorksheetAxis axis,
        int hitIndex)
    {
        if (session.Selection.Ranges.Count == 1)
        {
            var range = session.Selection.Ranges[0];
            if (axis == WorksheetAxis.Row &&
                range.Left == 0 &&
                range.Right == SpreadsheetLimits.MaxColumns - 1 &&
                hitIndex >= range.Top &&
                hitIndex <= range.Bottom)
            {
                return (range.Top, range.RowCount);
            }
            if (axis == WorksheetAxis.Column &&
                range.Top == 0 &&
                range.Bottom == SpreadsheetLimits.MaxRows - 1 &&
                hitIndex >= range.Left &&
                hitIndex <= range.Right)
            {
                return (range.Left, range.ColumnCount);
            }
        }

        return (hitIndex, 1);
    }

    private void AttachIfPossible()
    {
        var owner = _owner;
        if (_adornerLayer is not null || owner is null || !owner.IsLoaded)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(owner);
        if (layer is null)
        {
            return;
        }

        _previewAdorner ??= new HeaderReorderPreviewAdorner(owner);
        layer.Add(_previewAdorner);
        _adornerLayer = layer;
        UpdatePreview();
    }

    private void DetachPreviewAdorner()
    {
        var layer = _adornerLayer;
        var adorner = _previewAdorner;
        _adornerLayer = null;
        if (layer is not null && adorner is not null)
        {
            layer.Remove(adorner);
        }
    }

    private void UpdatePreview()
    {
        AttachIfPossible();
        if (_previewAdorner is null)
        {
            return;
        }

        _previewAdorner.Target = _dropTarget;
        _previewAdorner.InvalidateVisual();
    }

    private readonly record struct HeaderReorderState(
        WorksheetAxis Axis,
        int SourceIndex,
        int Count,
        PointD StartPoint,
        bool IsActive);

    private sealed class HeaderReorderPreviewAdorner : Adorner
    {
        internal HeaderReorderPreviewAdorner(NeraSpreadsheetControl owner)
            : base(owner)
        {
            IsHitTestVisible = false;
        }

        internal SpreadsheetSplitHeaderReorderDropTarget? Target { get; set; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (Target is not { } target ||
                AdornedElement is not NeraSpreadsheetControl owner)
            {
                return;
            }

            var sourceColor = target.IsNoOp
                ? owner.RenderTheme.HeaderBorder
                : owner.RenderTheme.ActivePaneBorder;
            var brush = new SolidColorBrush(Color.FromArgb(
                sourceColor.Alpha,
                sourceColor.Red,
                sourceColor.Green,
                sourceColor.Blue));
            brush.Freeze();
            drawingContext.DrawRectangle(
                brush,
                null,
                new Rect(
                    target.PreviewBounds.X,
                    target.PreviewBounds.Y,
                    target.PreviewBounds.Width,
                    target.PreviewBounds.Height));
        }
    }
}

public static class NeraSpreadsheetHeaderReorderExtensions
{
    private static readonly ConditionalWeakTable<
        NeraSpreadsheetControl,
        NeraSpreadsheetHeaderReorderController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetHeaderReorderController EnableHeaderReordering(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (!existing.IsDisposed)
                {
                    return existing;
                }
                Controllers.Remove(control);
            }

            var controller = new NeraSpreadsheetHeaderReorderController(control);
            Controllers.Add(control, controller);
            return controller;
        }
    }

    public static bool TryGetHeaderReorderController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetHeaderReorderController controller)
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

    public static bool DisableHeaderReordering(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetHeaderReorderController controller;
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
        NeraSpreadsheetHeaderReorderController controller)
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
