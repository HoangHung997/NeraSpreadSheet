using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

public enum SpreadsheetSplitPaneMode
{
    None,
    Vertical,
    Horizontal,
    Both,
}

public sealed class SpreadsheetSplitChangedEventArgs : EventArgs
{
    public SpreadsheetSplitChangedEventArgs(
        SpreadsheetSplitPaneMode mode,
        double? splitX,
        double? splitY,
        SpreadsheetSplitLayout? layout)
    {
        Mode = mode;
        SplitX = splitX;
        SplitY = splitY;
        Layout = layout;
    }

    public SpreadsheetSplitPaneMode Mode { get; }

    public double? SplitX { get; }

    public double? SplitY { get; }

    public SpreadsheetSplitLayout? Layout { get; }
}

public sealed class SpreadsheetPaneScrollChangedEventArgs : EventArgs
{
    public SpreadsheetPaneScrollChangedEventArgs(
        SpreadsheetPaneId paneId,
        ScrollSnapshot snapshot)
    {
        PaneId = paneId;
        Snapshot = snapshot;
    }

    public SpreadsheetPaneId PaneId { get; }

    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetSplitController : IDisposable
{
    private NeraSpreadsheetControl? _owner;
    private NeraSpreadsheetSplitAdorner? _adorner;
    private AdornerLayer? _adornerLayer;

    internal NeraSpreadsheetSplitController(NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (owner.IsEditing)
        {
            owner.CancelEditor();
        }

        _adorner = new NeraSpreadsheetSplitAdorner(owner);
        _adorner.SplitChanged += OnAdornerSplitChanged;
        _adorner.PaneScrollChanged += OnAdornerPaneScrollChanged;
        owner.Loaded += OnOwnerLoaded;
        owner.Unloaded += OnOwnerUnloaded;
        owner.SizeChanged += OnOwnerSizeChanged;
        AttachIfPossible();
    }

    public bool IsDisposed => _adorner is null;

    public bool IsAttached => _adornerLayer is not null;

    public SpreadsheetSession? Session
    {
        get => GetOwner().Session;
        set
        {
            GetOwner().Session = value;
            GetAdorner().NotifyOwnerStateChanged();
        }
    }

    public WpfRenderingBackend RenderingBackend
    {
        get => GetOwner().RenderingBackend;
        set
        {
            GetOwner().RenderingBackend = value;
            GetAdorner().NotifyOwnerStateChanged();
        }
    }

    public SpreadsheetRenderTheme RenderTheme
    {
        get => GetOwner().RenderTheme;
        set
        {
            GetOwner().RenderTheme = value ??
                throw new ArgumentNullException(nameof(value));
            GetAdorner().NotifyOwnerStateChanged();
        }
    }

    public SpreadsheetSplitPaneMode Mode => GetAdorner().Mode;

    public double? SplitX => GetAdorner().SplitX;

    public double? SplitY => GetAdorner().SplitY;

    public double SeparatorThickness
    {
        get => GetAdorner().SeparatorThickness;
        set => GetAdorner().SeparatorThickness = value;
    }

    public double MinimumPaneExtent
    {
        get => GetAdorner().MinimumPaneExtent;
        set => GetAdorner().MinimumPaneExtent = value;
    }

    public SpreadsheetPaneId ActivePane => GetAdorner().ActivePane;

    public SpreadsheetSplitViewportFrame? LastFrame => GetAdorner().LastFrame;

    public WpfGpuRendererDiagnostics? GpuDiagnostics =>
        GetAdorner().GpuDiagnostics;

    public bool CanUndoViewChange => GetAdorner().CanUndoSplitViewChange;

    public bool CanRedoViewChange => GetAdorner().CanRedoSplitViewChange;

    public string? NextViewUndoDescription =>
        GetAdorner().NextSplitViewUndoDescription;

    public string? NextViewRedoDescription =>
        GetAdorner().NextSplitViewRedoDescription;

    public event EventHandler<SpreadsheetSplitChangedEventArgs>? SplitChanged;

    public event EventHandler<SpreadsheetPaneScrollChangedEventArgs>?
        PaneScrollChanged;

    public void SetMode(SpreadsheetSplitPaneMode mode) =>
        GetAdorner().SetModeWithHistory(mode);

    public void SetSplit(double? splitX, double? splitY) =>
        GetAdorner().SetSplitWithHistory(splitX, splitY);

    public void ClearSplit() => SetMode(SpreadsheetSplitPaneMode.None);

    public void SetActivePane(SpreadsheetPaneId paneId) =>
        GetAdorner().SetActivePaneWithHistory(paneId);

    public PointD GetPaneScroll(SpreadsheetPaneId paneId) =>
        GetAdorner().GetPaneScroll(paneId);

    public ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        GetAdorner().GetPaneScrollSnapshot(paneId);

    public void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated = false) =>
        GetAdorner().ScrollPaneToWithHistory(
            paneId,
            offsetX,
            offsetY,
            animated);

    public void QueuePaneScroll(
        SpreadsheetPaneId paneId,
        ScrollDelta delta) =>
        GetAdorner().QueuePaneScrollWithHistory(paneId, delta);

    public void QueueActivePaneScroll(ScrollDelta delta) =>
        GetAdorner().QueueActivePaneScrollWithHistory(delta);

    public bool UndoViewChange() => GetAdorner().UndoSplitViewChange();

    public bool RedoViewChange() => GetAdorner().RedoSplitViewChange();

    public bool TryHitTest(
        double controlX,
        double controlY,
        out SpreadsheetPaneId paneId,
        out CellAddress address) =>
        GetAdorner().TryHitTest(
            controlX,
            controlY,
            out paneId,
            out address);

    public void RenderNow()
    {
        AttachOrThrow();
        GetAdorner().RenderNow();
    }

    public bool Focus()
    {
        AttachOrThrow();
        return GetAdorner().Focus();
    }

    /// <summary>Gets the actual split native editor draft, or null after the canonical edit ends.</summary>
    public SpreadsheetEditorDraft? CurrentEditorDraft => GetAdorner().CurrentEditorDraft;

    /// <summary>Starts the canonical edit in the active split pane. Requires a loaded adorner host.</summary>
    public void BeginEdit(string? replacementText = null)
    {
        AttachOrThrow();
        GetAdorner().BeginEdit(replacementText);
    }

    /// <summary>Commits through Session.Editor once; validation failure keeps the native draft and selection.</summary>
    public bool CommitEditor() => GetAdorner().CommitEditor();

    /// <summary>Always cleans up the native editor; returns true only when a canonical edit was canceled.</summary>
    public bool CancelEditor() => GetAdorner().CancelEditor();

    /// <summary>
    /// Updates native text and UTF-16 selection without focus, history or restarting
    /// the edit. Invalid bounds throw without mutation; no active draft returns false.
    /// The owning control raises EditorDraftChanged for draft and lifecycle changes.
    /// </summary>
    public bool UpdateEditorDraft(string text, int selectionStart, int selectionLength) =>
        GetAdorner().UpdateEditorDraft(text, selectionStart, selectionLength);

    /// <summary>Focuses the split native editor while retaining its draft and selection.</summary>
    public bool FocusEditor() => GetAdorner().FocusEditor();

    internal void NotifyOwnerStateChanged() => GetAdorner().NotifyOwnerStateChanged();

    internal bool BeginViewHistory(
        string description,
        SpreadsheetSplitViewChangeKind changeKind) =>
        GetAdorner().BeginSplitViewHistory(description, changeKind);

    internal bool CommitViewHistory() =>
        GetAdorner().CommitSplitViewHistory();

    internal bool CancelViewHistory(bool restoreBeforeState = true) =>
        GetAdorner().CancelSplitViewHistory(restoreBeforeState);

    public void Dispose()
    {
        var owner = _owner;
        var adorner = _adorner;
        if (owner is null || adorner is null)
        {
            return;
        }

        adorner.CancelSplitViewHistory(restoreBeforeState: true);
        owner.Loaded -= OnOwnerLoaded;
        owner.Unloaded -= OnOwnerUnloaded;
        owner.SizeChanged -= OnOwnerSizeChanged;
        adorner.SplitChanged -= OnAdornerSplitChanged;
        adorner.PaneScrollChanged -= OnAdornerPaneScrollChanged;
        DetachAdorner();
        _owner = null;
        _adorner = null;
        adorner.Dispose();
        NeraSpreadsheetSplitExtensions.Remove(owner, this);
        GC.SuppressFinalize(this);
    }

    private NeraSpreadsheetControl GetOwner()
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _owner!;
    }

    private NeraSpreadsheetSplitAdorner GetAdorner()
    {
        ObjectDisposedException.ThrowIf(_adorner is null, this);
        return _adorner!;
    }

    private void AttachIfPossible()
    {
        if (_adornerLayer is not null ||
            _owner is null ||
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
        _adorner.NotifyOwnerStateChanged();
        layer.UpdateLayout();
    }

    private void AttachOrThrow()
    {
        AttachIfPossible();
        if (_adornerLayer is null)
        {
            throw new InvalidOperationException(
                "The WPF spreadsheet must be loaded inside an AdornerDecorator before split panes can render.");
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
        _adorner?.NotifyOwnerStateChanged();

    private void OnAdornerSplitChanged(
        object? sender,
        SpreadsheetSplitChangedEventArgs e) =>
        SplitChanged?.Invoke(this, e);

    private void OnAdornerPaneScrollChanged(
        object? sender,
        SpreadsheetPaneScrollChangedEventArgs e) =>
        PaneScrollChanged?.Invoke(this, e);
}

public static class NeraSpreadsheetSplitExtensions
{
    private static readonly ConditionalWeakTable<
        NeraSpreadsheetControl,
        NeraSpreadsheetSplitController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetSplitController EnableSplitPanes(
        this NeraSpreadsheetControl control,
        SpreadsheetSplitPaneMode mode = SpreadsheetSplitPaneMode.Vertical)
    {
        ArgumentNullException.ThrowIfNull(control);

        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (existing.IsDisposed)
                {
                    Controllers.Remove(control);
                }
                else
                {
                    existing.SetMode(mode);
                    return existing;
                }
            }

            var controller = new NeraSpreadsheetSplitController(control);
            Controllers.Add(control, controller);
            controller.SetMode(mode);
            return controller;
        }
    }

    public static bool TryGetSplitPaneController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetSplitController controller)
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

    public static bool DisableSplitPanes(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetSplitController controller;
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
        NeraSpreadsheetSplitController controller)
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
