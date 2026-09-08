using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Four stable native panes over one caller-owned session. Topology,
/// active pane, scroll offsets and split history belong to Session.View.</summary>
public sealed class NeraSpreadsheetSplitControl : Panel, IDisposable
{
    private readonly Pane[] _panes;
    private readonly SplitFormulaRouting _formulaRouting;
    private SpreadsheetSession? _session;
    private int _syncDepth;
    private bool _attached;
    private bool _subscribed;
    private bool _disposed;
    private IPointer? _dragPointer;
    private SpreadsheetSplitHitRegionKind _dragKind;
    private SpreadsheetSplitViewHistoryTransaction? _dragHistory;

    public NeraSpreadsheetSplitControl()
    {
        Background = Brushes.Gray; ClipToBounds = true;
        _panes = Enum.GetValues<SpreadsheetSplitViewPane>().Select(id => new Pane(this, id)).ToArray();
        foreach (var pane in _panes) Children.Add(pane.Root);
        _formulaRouting = new SplitFormulaRouting(this);
        // One handler owns precedence: Avalonia reverses sibling registrations on
        // a tunneling route, so subscription order cannot establish draft priority.
        AddHandler(PointerPressedEvent, OnPointerDown, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMove, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerUp, RoutingStrategies.Tunnel);
        Synchronize();
    }
    public SpreadsheetSession? Session
    {
        get => _session;
        set
        {
            VerifyUsable(); if (ReferenceEquals(_session, value)) return;
            _formulaRouting.Cancel(); FinishDrag(false); DetachSession(); _syncDepth++;
            try
            {
                _session = value;
                foreach (var pane in _panes) pane.Sheet.Session = value;
                if (_attached) AttachSession();
                Synchronize();
            }
            finally { _syncDepth--; }
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public SpreadsheetSplitViewState State => _session?.View.SplitState ?? default;
    public SpreadsheetSplitLayout? LastLayout { get; private set; }
    public NeraSpreadsheetControl ActiveSpreadsheet => GetPane(State.ActivePane);
    public NeraSpreadsheetControl? EditingSpreadsheet => _panes.FirstOrDefault(static pane => pane.Sheet.IsEditing)?.Sheet;
    public event EventHandler? SessionChanged;
    public event EventHandler? ActivePaneChanged;
    public event EventHandler<SpreadsheetInteractionFailedEventArgs>? InteractionFailed;
    public NeraSpreadsheetControl GetPane(SpreadsheetSplitViewPane pane)
    {
        if (!Enum.IsDefined(pane)) throw new ArgumentOutOfRangeException(nameof(pane));
        return _panes[(int)pane].Sheet;
    }
    public bool SetMode(SpreadsheetSplitViewMode mode, double? splitX = null, double? splitY = null)
    {
        VerifyUsable(); _formulaRouting.Cancel(); var view = RequireSession().View;
        var x = mode is SpreadsheetSplitViewMode.Vertical or SpreadsheetSplitViewMode.Both ? splitX ?? Math.Max(48, Bounds.Width / 2) : (double?)null;
        var y = mode is SpreadsheetSplitViewMode.Horizontal or SpreadsheetSplitViewMode.Both ? splitY ?? Math.Max(48, Bounds.Height / 2) : (double?)null;
        var changed = view.ExecuteSplitTopologyChange(mode, x, y); Synchronize(); return changed;
    }
    public bool ActivatePane(SpreadsheetSplitViewPane pane, bool focus = true)
    {
        VerifyUsable(); var session = RequireSession();
        if (!session.View.SplitState.IsPaneVisible(pane)) return false;
        var target = GetPane(pane);
        if (EditingSpreadsheet is { } editor && !ReferenceEquals(editor, target) && !editor.CommitEditor()) return false;
        var changed = session.View.SetSplitActivePane(pane, this);
        if (focus) target.Focus();
        if (changed && !_subscribed) ActivePaneChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    public void SetZoom(double zoom) { VerifyUsable(); foreach (var pane in _panes) pane.Sheet.Zoom = zoom; }
    public bool UndoSplit() { VerifyUsable(); return RequireSession().View.UndoSplitViewChange(); }
    public bool RedoSplit() { VerifyUsable(); return RequireSession().View.RedoSplitViewChange(); }
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 900, double.IsFinite(availableSize.Height) ? availableSize.Height : 600);
        var layout = Compute(size);
        foreach (var pane in _panes) if (layout.TryGetPane((SpreadsheetPaneId)pane.Id, out var slot)) pane.Root.Measure(new Size(slot.Bounds.Width, slot.Bounds.Height));
        return size;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = Compute(finalSize); LastLayout = layout; _syncDepth++;
        try
        {
            foreach (var pane in _panes)
            {
                if (layout.TryGetPane((SpreadsheetPaneId)pane.Id, out var slot))
                {
                    pane.Root.IsVisible = true;
                    pane.Root.Arrange(new Rect(slot.Bounds.X, slot.Bounds.Y, slot.Bounds.Width, slot.Bounds.Height));
                    pane.UpdateScrollbars();
                }
                else pane.Root.IsVisible = false;
            }
        }
        finally { _syncDepth--; }
        return finalSize;
    }
    private SpreadsheetSplitLayout Compute(Size size)
    {
        var state = State;
        return SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(new SizeD(Math.Max(0, size.Width), Math.Max(0, size.Height)), state.SplitX, state.SplitY, 5, 64));
    }
    private void Synchronize()
    {
        if (_disposed) return; _syncDepth++;
        try
        {
            var state = State;
            foreach (var pane in _panes)
            {
                var visible = state.IsPaneVisible(pane.Id);
                if (!visible && pane.Sheet.IsEditing) pane.Sheet.CancelEditor();
                pane.Root.IsVisible = visible;
                var scroll = state.GetPaneScroll(pane.Id); pane.Sheet.ScrollTo(scroll.OffsetX, scroll.OffsetY); pane.UpdateScrollbars();
            }
            InvalidateMeasure(); InvalidateArrange();
        }
        finally { _syncDepth--; }
    }
    private void OnSplitChanged(object? sender, SpreadsheetSplitViewChangedEventArgs e)
    {
        if (_disposed || _session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet)) return;
        if (ReferenceEquals(e.Source, this) && e.ChangeKind == SpreadsheetSplitViewChangeKind.PaneScroll) return;
        Synchronize();
        if (e.ChangeKind is SpreadsheetSplitViewChangeKind.ActivePane or SpreadsheetSplitViewChangeKind.ActiveWorksheet or SpreadsheetSplitViewChangeKind.History)
            ActivePaneChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnWorksheetChanged(object? sender, EventArgs e) { _formulaRouting.Cancel(); FinishDrag(false); Synchronize(); }
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_syncDepth == 0 && _session is not null && !_session.Editor.IsEditing) ActiveSpreadsheet.ScrollCellIntoView(_session.Selection.ActiveCell);
    }
    private void OnPaneViewportChanged(Pane pane)
    {
        pane.UpdateScrollbars();
        if (_syncDepth != 0 || _session is null || !pane.Root.IsVisible || _disposed) return;
        var scroll = pane.Sheet.ScrollSnapshot; _session.View.SetSplitPaneScroll(pane.Id, scroll.OffsetX, scroll.OffsetY, this);
    }
    private void OnPointerDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || _disposed || _session is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        LastLayout = Compute(Bounds.Size);
        if (_formulaRouting.TryPressed(e)) return;
        var point = e.GetPosition(this); var hit = LastLayout.HitTest(new PointD(point.X, point.Y));
        if (hit.RegionKind == SpreadsheetSplitHitRegionKind.Pane && hit.PaneId is { } id)
        {
            try { if (!ActivatePane((SpreadsheetSplitViewPane)id, false)) e.Handled = true; }
            catch (Exception exception)
            {
                e.Handled = true;
                if (InteractionFailed is not { } handler) throw;
                handler(this, new SpreadsheetInteractionFailedEventArgs(exception));
            }
            return;
        }
        if (hit.RegionKind is not (SpreadsheetSplitHitRegionKind.VerticalSeparator or SpreadsheetSplitHitRegionKind.HorizontalSeparator or SpreadsheetSplitHitRegionKind.SeparatorIntersection)) return;
        e.Handled = true; FinishDrag(false); _dragKind = hit.RegionKind; _dragPointer = e.Pointer;
        _dragHistory = _session.View.BeginSplitViewHistoryTransaction("Resize split panes", SpreadsheetSplitViewChangeKind.Topology); e.Pointer.Capture(this);
    }
    private void OnPointerMove(object? sender, PointerEventArgs e)
    {
        if (e.Handled || _disposed || _session is null) return;
        if (_formulaRouting.TryMoved(e)) return;
        if (_dragPointer != e.Pointer) return;
        e.Handled = true; var point = e.GetPosition(this); var state = State;
        var x = _dragKind is SpreadsheetSplitHitRegionKind.VerticalSeparator or SpreadsheetSplitHitRegionKind.SeparatorIntersection ? point.X : state.SplitX;
        var y = _dragKind is SpreadsheetSplitHitRegionKind.HorizontalSeparator or SpreadsheetSplitHitRegionKind.SeparatorIntersection ? point.Y : state.SplitY;
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(new SizeD(Bounds.Width, Bounds.Height), x, y, 5, 64));
        _session.View.SetSplitTopology(state.Mode, layout.SplitX ?? state.SplitX, layout.SplitY ?? state.SplitY, this);
    }
    private void OnPointerUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Handled || _disposed) return;
        if (_formulaRouting.TryReleased(e)) return;
        if (_dragPointer != e.Pointer) return;
        e.Handled = true; FinishDrag(true);
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { _formulaRouting.Cancel(); FinishDrag(false); base.OnPointerCaptureLost(e); }
    private void FinishDrag(bool commit)
    {
        var pointer = _dragPointer; _dragPointer = null; var history = _dragHistory; _dragHistory = null;
        if (history is not null) { using (history) { if (commit) history.Commit(); else history.Cancel(); } }
        pointer?.Capture(null);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); if (_disposed) return; _attached = true; AttachSession(); Synchronize();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _formulaRouting.Cancel(); FinishDrag(false); DetachSession(); _attached = false; base.OnDetachedFromVisualTree(e);
    }
    private void AttachSession()
    {
        if (_subscribed || _session is null) return;
        _session.View.SplitChanged += OnSplitChanged; _session.ActiveWorksheetChanged += OnWorksheetChanged; _session.Selection.Changed += OnSelectionChanged; _subscribed = true;
    }
    private void DetachSession()
    {
        if (_subscribed && _session is not null)
        {
            _session.View.SplitChanged -= OnSplitChanged; _session.ActiveWorksheetChanged -= OnWorksheetChanged; _session.Selection.Changed -= OnSelectionChanged;
        }
        _subscribed = false;
    }
    private SpreadsheetSession RequireSession() => _session ?? throw new InvalidOperationException("Assign a spreadsheet session first.");
    private void VerifyUsable() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return; _formulaRouting.Dispose(); FinishDrag(false); DetachSession(); _disposed = true;
        RemoveHandler(PointerPressedEvent, OnPointerDown); RemoveHandler(PointerMovedEvent, OnPointerMove); RemoveHandler(PointerReleasedEvent, OnPointerUp);
        foreach (var pane in _panes) pane.Dispose(); Children.Clear(); _session = null;
    }
    private sealed class Pane : IDisposable
    {
        private readonly NeraSpreadsheetSplitControl _owner;
        private bool _updating;
        public Pane(NeraSpreadsheetSplitControl owner, SpreadsheetSplitViewPane id)
        {
            _owner = owner; Id = id;
            Root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Sheet = new NeraSpreadsheetControl { UseAdaptiveNavigationExtent = true };
            Horizontal = new ScrollBar { Orientation = Orientation.Horizontal, SmallChange = 24 }; Vertical = new ScrollBar { Orientation = Orientation.Vertical, SmallChange = 24 };
            Root.Children.Add(Sheet); Grid.SetRow(Horizontal, 1); Root.Children.Add(Horizontal); Grid.SetColumn(Vertical, 1); Root.Children.Add(Vertical);
            Sheet.ViewportChanged += OnViewportChanged; Horizontal.ValueChanged += OnScrollChanged; Vertical.ValueChanged += OnScrollChanged; Sheet.InteractionFailed += OnInteractionFailed;
        }
        public SpreadsheetSplitViewPane Id { get; }
        public Grid Root { get; }
        public NeraSpreadsheetControl Sheet { get; }
        private ScrollBar Horizontal { get; }
        private ScrollBar Vertical { get; }
        private void OnViewportChanged(object? sender, EventArgs e) => _owner.OnPaneViewportChanged(this);
        private void OnScrollChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_updating && _owner._syncDepth == 0 && !_owner._disposed) Sheet.ScrollTo(Horizontal.Value, Vertical.Value);
        }
        private void OnInteractionFailed(object? sender, SpreadsheetInteractionFailedEventArgs e)
        {
            if (_owner.InteractionFailed is not { } handler) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.Exception).Throw();
            else handler(_owner, e);
        }
        public void UpdateScrollbars()
        {
            if (_updating) return; _updating = true;
            try
            {
                Horizontal.Maximum = Math.Max(0, Sheet.ContentWidth - Sheet.ViewportBodyWidth); Vertical.Maximum = Math.Max(0, Sheet.ContentHeight - Sheet.ViewportBodyHeight);
                Horizontal.ViewportSize = Sheet.ViewportBodyWidth; Vertical.ViewportSize = Sheet.ViewportBodyHeight;
                Horizontal.LargeChange = Sheet.ViewportBodyWidth; Vertical.LargeChange = Sheet.ViewportBodyHeight;
                Horizontal.Value = Sheet.ScrollSnapshot.OffsetX; Vertical.Value = Sheet.ScrollSnapshot.OffsetY;
            }
            finally { _updating = false; }
        }
        public void Dispose()
        {
            Sheet.ViewportChanged -= OnViewportChanged; Horizontal.ValueChanged -= OnScrollChanged; Vertical.ValueChanged -= OnScrollChanged;
            Sheet.InteractionFailed -= OnInteractionFailed; Sheet.Dispose();
        }
    }
}
