using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Avalonia;

/// <summary>A virtualized viewport over a caller-owned spreadsheet session.
/// Access the host and attached workbook only from its Avalonia UI thread.</summary>
public sealed partial class NeraSpreadsheetControl : Control, IDisposable
{
    public static readonly DirectProperty<NeraSpreadsheetControl, SpreadsheetSession?> SessionProperty =
        AvaloniaProperty.RegisterDirect<NeraSpreadsheetControl, SpreadsheetSession?>(nameof(Session), control => control.Session, (control, value) => control.Session = value);
    public static readonly DirectProperty<NeraSpreadsheetControl, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<NeraSpreadsheetControl, double>(nameof(Zoom), control => control.Zoom, (control, value) => control.Zoom = value);
    private readonly AvaloniaDisplayListRenderer _renderer = new();
    private readonly ContinuousScrollController _scroll = new();
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromSeconds(1d / 60d) };
    private readonly Stopwatch _frameClock = new();
    private readonly TextBox _editor = new() { IsVisible = false, AcceptsReturn = true, BorderThickness = new Thickness(1), Padding = new Thickness(2, 0) };
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private Worksheet? _worksheet;
    private ViewportLayout? _layout;
    private SpreadsheetRenderTheme _renderTheme = new() { ShowHeaders = true };
    private bool _attached;
    private bool _subscribed;
    private bool _disposed;
    private bool _adaptive;
    private double _zoom = 1d;

    public NeraSpreadsheetControl()
    {
        Focusable = true; ClipToBounds = true;
        LogicalChildren.Add(_editor); VisualChildren.Add(_editor);
        Children = Array.AsReadOnly<Control>([_editor]);
        _frameTimer.Tick += OnFrame;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextChanged += OnEditorTextChanged;
        InitializeFormulaAssistance();
    }
    /// <summary>Exactly one reusable native editor, not a control per cell.</summary>
    public IReadOnlyList<Control> Children { get; }
    public SpreadsheetSession? Session
    {
        get => _session;
        set
        {
            VerifyUsable(); if (ReferenceEquals(_session, value)) return;
            _clipboardEpoch++; EndOwnedEdit(false); DetachSession(); StopMotion();
            _viewport = value is null ? null : new SpreadsheetViewportEngine(value); _layout = null; _scroll.Reset();
            SetAndRaise(SessionProperty, ref _session, value);
            if (_attached) AttachSession();
            RefreshFormulaHighlights(); Refresh(); SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    /// <summary>Visual zoom 10%–400%, independent of worksheet dimensions.</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            VerifyUsable();
            if (!double.IsFinite(value) || value < 0.1 || value > 4) throw new ArgumentOutOfRangeException(nameof(value), "Zoom must be between 0.1 and 4.");
            if (_zoom == value) return;
            SetAndRaise(ZoomProperty, ref _zoom, value); Refresh(); ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public SpreadsheetRenderTheme RenderTheme
    {
        get => _renderTheme;
        set
        {
            VerifyUsable(); ArgumentNullException.ThrowIfNull(value); _renderTheme = value;
            _renderer.ClearCaches(); _viewport?.ClearDisplayListCache(); RefreshFormulaHighlights(); Refresh();
        }
    }
    public bool UseAdaptiveNavigationExtent
    {
        get => _adaptive;
        set { VerifyUsable(); if (_adaptive != value) { _adaptive = value; Refresh(); } }
    }
    public ScrollSnapshot ScrollSnapshot => _scroll.Snapshot;
    public double ContentWidth { get; private set; }
    public double ContentHeight { get; private set; }
    public double ViewportBodyWidth => Chrome.BodyWidth;
    public double ViewportBodyHeight => Chrome.BodyHeight;
    public long RenderedFrameCount { get; private set; }
    public event EventHandler? SessionChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler? ViewportChanged;
    public event EventHandler? ZoomChanged;
    public event EventHandler? EditorDraftChanged;
    public event EventHandler<SpreadsheetInteractionFailedEventArgs>? InteractionFailed;
    private double DocumentWidth => Math.Max(0, Bounds.Width / _zoom);
    private double DocumentHeight => Math.Max(0, Bounds.Height / _zoom);
    private SpreadsheetChromeMetrics Chrome => SpreadsheetChromeGeometry.Calculate(DocumentWidth, DocumentHeight, _renderTheme);
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (_disposed || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        context.DrawRectangle(Brushes.White, null, new Rect(Bounds.Size));
        if (_session is null || _viewport is null) return;
        var chrome = Chrome; if (chrome.BodyWidth <= 0 || chrome.BodyHeight <= 0) return;
        var snapshot = _scroll.Snapshot;
        var frame = _viewport.Compose(snapshot.OffsetX, snapshot.OffsetY, chrome.BodyWidth, chrome.BodyHeight, 128d, _renderTheme);
        _layout = frame.Layout;
        var body = SpreadsheetFormulaReferenceDisplayListComposer.Compose(frame.DisplayList, frame.Layout, CurrentFormulaReferenceHighlights, _renderTheme.FormulaReferenceStrokeWidth);
        var displayList = SpreadsheetChromeDisplayListComposer.Compose(body, frame.Layout, _session.Selection.Capture(), _renderTheme);
        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom))) _renderer.Render(context, displayList);
        RenderedFrameCount++;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        _editor.Measure(_editor.IsVisible ? _editorBounds.Size : default);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 640, double.IsFinite(availableSize.Height) ? availableSize.Height : 400);
    }
    protected override Size ArrangeOverride(Size finalSize) { _editor.Arrange(_editor.IsVisible ? _editorBounds : default); return finalSize; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change); if (change.Property == BoundsProperty && !_disposed) Refresh();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); if (_disposed) return;
        _attached = true; AttachSession(); _viewport?.InvalidateMetrics(); _viewport?.ClearDisplayListCache(); Refresh();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (!_disposed)
        {
            _clipboardEpoch++; EndOwnedEdit(false); ReleasePointer(); StopMotion();
            var snapshot = _scroll.Snapshot; _scroll.Reset(); _scroll.ScrollTo(snapshot.OffsetX, snapshot.OffsetY, false);
            DetachSession(); _renderer.ClearCaches();
        }
        _attached = false; base.OnDetachedFromVisualTree(e);
    }
    /// <summary>Coalesces finite document-pixel deltas into the shared frame controller.</summary>
    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        VerifyUsable();
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY)) throw new ArgumentOutOfRangeException(nameof(deltaX), "Scroll deltas must be finite.");
        if (_session is null) return;
        _scroll.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        if (_attached && !_frameTimer.IsEnabled) { _frameClock.Restart(); _frameTimer.Start(); }
    }
    public void ScrollTo(double offsetX, double offsetY)
    {
        VerifyUsable();
        if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY)) throw new ArgumentOutOfRangeException(nameof(offsetX), "Scroll offsets must be finite.");
        UpdateExtent();
        _scroll.ScrollTo(Math.Clamp(offsetX, 0, Math.Max(0, ContentWidth - ViewportBodyWidth)), Math.Clamp(offsetY, 0, Math.Max(0, ContentHeight - ViewportBodyHeight)), false);
        UpdateEditorBounds(); InvalidateVisual(); ViewportChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool ScrollCellIntoView(CellAddress address)
    {
        VerifyUsable(); if (_session is null || _viewport is null) return false;
        UpdateExtent(); var scroll = _scroll.Snapshot;
        if (!_viewport.TryGetCellBounds(address, scroll.OffsetX, scroll.OffsetY, out var cell)) return false;
        var frozen = _viewport.GetFrozenPaneExtent(); var x = scroll.OffsetX; var y = scroll.OffsetY;
        if (address.ColumnIndex >= _session.View.FrozenColumns)
        {
            var left = Math.Min(frozen.Width, ViewportBodyWidth);
            if (cell.Left < left) x -= left - cell.Left; else if (cell.Right > ViewportBodyWidth) x += cell.Right - ViewportBodyWidth;
        }
        if (address.RowIndex >= _session.View.FrozenRows)
        {
            var top = Math.Min(frozen.Height, ViewportBodyHeight);
            if (cell.Top < top) y -= top - cell.Top; else if (cell.Bottom > ViewportBodyHeight) y += cell.Bottom - ViewportBodyHeight;
        }
        if (x == scroll.OffsetX && y == scroll.OffsetY) return false; ScrollTo(x, y); return true;
    }
    internal void AdvanceFrame(TimeSpan elapsed)
    {
        VerifyUsable(); UpdateExtent();
        var result = _scroll.AdvanceFrame(elapsed, new ScrollBounds(Math.Max(0, ContentWidth - ViewportBodyWidth), Math.Max(0, ContentHeight - ViewportBodyHeight)));
        if (result.Changed) { UpdateEditorBounds(); InvalidateVisual(); ViewportChanged?.Invoke(this, EventArgs.Empty); }
        if (!_scroll.HasPendingMotion) StopMotion();
    }
    private void OnFrame(object? sender, EventArgs e) { var elapsed = _frameClock.Elapsed; _frameClock.Restart(); AdvanceFrame(elapsed); }
    private void StopMotion() { _frameTimer.Stop(); _frameClock.Reset(); }
    private void AttachSession()
    {
        if (_subscribed || _session is null) return;
        _session.ActiveWorksheetChanged += OnWorksheetChanged; _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged; _session.Editor.StateChanged += OnCanonicalEditorChanged;
        _session.Analytics.Changed += OnVisualChanged; _session.AnalyticsPlacements.Changed += OnVisualChanged;
        _session.AnalyticsInteraction.Changed += OnVisualChanged; _subscribed = true; AttachWorksheet();
    }
    private void DetachSession()
    {
        if (_subscribed && _session is not null)
        {
            _session.ActiveWorksheetChanged -= OnWorksheetChanged; _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged; _session.Editor.StateChanged -= OnCanonicalEditorChanged;
            _session.Analytics.Changed -= OnVisualChanged; _session.AnalyticsPlacements.Changed -= OnVisualChanged;
            _session.AnalyticsInteraction.Changed -= OnVisualChanged;
        }
        _subscribed = false; DetachWorksheet();
    }
    private void AttachWorksheet()
    {
        DetachWorksheet(); _worksheet = _session?.ActiveWorksheet; if (_worksheet is null) return;
        _worksheet.CellsChanged += OnVisualChanged; _worksheet.Dimensions.Changed += OnDimensionsChanged;
    }
    private void DetachWorksheet()
    {
        if (_worksheet is null) return;
        _worksheet.CellsChanged -= OnVisualChanged; _worksheet.Dimensions.Changed -= OnDimensionsChanged; _worksheet = null;
    }
    private void OnWorksheetChanged(object? sender, EventArgs e)
    {
        _clipboardEpoch++; HideLocalEditor(); ReleasePointer(); StopMotion(); AttachWorksheet();
        _scroll.Reset(); _layout = null; _viewport?.InvalidateMetrics(); RefreshFormulaHighlights(); Refresh(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnSelectionChanged(object? sender, EventArgs e) { RefreshFormulaHighlights(); Refresh(); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    private void OnDimensionsChanged(object? sender, EventArgs e) { _viewport?.InvalidateMetrics(); _layout = null; Refresh(); }
    private void OnViewChanged(object? sender, EventArgs e) { _viewport?.ClearDisplayListCache(); _layout = null; Refresh(); }
    private void OnVisualChanged(object? sender, EventArgs e) { RefreshFormulaHighlights(); Refresh(); }
    private void UpdateExtent()
    {
        if (_session is null || _viewport is null) { ContentWidth = ContentHeight = 0; return; }
        var chrome = Chrome;
        var extent = _adaptive ? _viewport.GetAdaptiveNavigationExtent(_session.Selection.ActiveCell, new SizeD(chrome.BodyWidth, chrome.BodyHeight),
            new PointD(_scroll.Snapshot.OffsetX, _scroll.Snapshot.OffsetY), SpreadsheetViewportEngine.DefaultAdaptiveTrailingRowCount,
            SpreadsheetViewportEngine.DefaultAdaptiveTrailingColumnCount) : _viewport.GetContentExtent();
        ContentWidth = extent.Width; ContentHeight = extent.Height;
    }
    private void Refresh()
    {
        if (_disposed) return; UpdateExtent(); var snapshot = _scroll.Snapshot;
        var x = Math.Clamp(snapshot.OffsetX, 0, Math.Max(0, ContentWidth - ViewportBodyWidth));
        var y = Math.Clamp(snapshot.OffsetY, 0, Math.Max(0, ContentHeight - ViewportBodyHeight));
        if (x != snapshot.OffsetX || y != snapshot.OffsetY) _scroll.ScrollTo(x, y, false);
        UpdateEditorBounds(); InvalidateVisual(); ViewportChanged?.Invoke(this, EventArgs.Empty);
    }
    private void VerifyUsable() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return;
        _clipboardEpoch++; EndOwnedEdit(false); ReleasePointer(); StopMotion(); DetachSession(); DisposeFormulaAssistance();
        _frameTimer.Tick -= OnFrame; _editor.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown); _editor.TextChanged -= OnEditorTextChanged;
        _renderer.ClearCaches(); _viewport = null; _session = null; _disposed = true;
    }
}
public sealed class SpreadsheetInteractionFailedEventArgs : EventArgs
{
    public SpreadsheetInteractionFailedEventArgs(Exception exception) => Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    public Exception Exception { get; }
}
