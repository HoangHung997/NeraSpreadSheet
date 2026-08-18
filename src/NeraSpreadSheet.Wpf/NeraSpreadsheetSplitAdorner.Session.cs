using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;
using NeraSelectionChangedEventArgs = NeraSpreadSheet.Interaction.SelectionChangedEventArgs;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private bool _applyingSplitViewState;

    private void SynchronizeSession()
    {
        var next = _owner.Session;
        if (ReferenceEquals(_session, next))
        {
            EnsureWorksheetSubscription();
            return;
        }

        CancelSplitViewHistory(restoreBeforeState: true);
        DetachSessionEvents();
        _cellEditor?.Cancel();
        _session = next;
        _engine = next is null
            ? null
            : new SpreadsheetSplitViewportEngine(next);
        _cellEditor = next?.Editor;
        _lastFrame = null;
        HideEditor();
        ApplyStoredSplitState();
        AttachSessionEvents();
        UpdateGpuSurfaceVisibility();
    }

    private void AttachSessionEvents()
    {
        if (_session is null ||
            _sessionEventsAttached ||
            _disposed ||
            !IsLoaded)
        {
            return;
        }

        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged;
        _session.View.SplitChanged += OnSplitViewChanged;
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
            _session.View.SplitChanged -= OnSplitViewChanged;
        }
        _sessionEventsAttached = false;
        DetachWorksheetSubscription();
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (!_sessionEventsAttached ||
            ReferenceEquals(_subscribedWorksheet, worksheet))
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

    private bool SetActivePaneCore(SpreadsheetPaneId paneId)
    {
        var engine = GetEngine();
        var previous = engine.ActivePane;
        engine.SetActivePane(paneId);
        if (engine.ActivePane == previous)
        {
            return false;
        }

        PersistCurrentSplitState(SpreadsheetSplitViewChangeKind.ActivePane);
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
        return true;
    }

    private void PersistCurrentSplitState(
        SpreadsheetSplitViewChangeKind changeKind)
    {
        if (_applyingSplitViewState || _session is null || _engine is null)
        {
            return;
        }

        var state = SpreadsheetSplitViewStateAdapter.Capture(
            _engine,
            _splitX,
            _splitY);
        _session.View.SetSplitState(
            _session.ActiveWorksheet,
            state,
            changeKind,
            this);
    }

    private void ApplyStoredSplitState() =>
        ApplySplitViewState(_session?.View.SplitState ?? default);

    private void ApplySplitViewState(SpreadsheetSplitViewState state)
    {
        if (_engine is null)
        {
            return;
        }

        var nextMode = ToAdornerMode(state.Mode);
        var topologyChanged =
            _mode != nextMode ||
            _splitX != state.SplitX ||
            _splitY != state.SplitY;
        var engineStateChanged =
            SpreadsheetSplitViewStateAdapter.Capture(
                _engine,
                _splitX,
                _splitY) != state;
        if (!topologyChanged && !engineStateChanged)
        {
            return;
        }

        _applyingSplitViewState = true;
        try
        {
            _mode = nextMode;
            _splitX = state.SplitX;
            _splitY = state.SplitY;
            SpreadsheetSplitViewStateAdapter.Apply(_engine, state);
        }
        finally
        {
            _applyingSplitViewState = false;
        }

        InvalidateSplitLayout();
        if (topologyChanged)
        {
            SplitChanged?.Invoke(
                this,
                new SpreadsheetSplitChangedEventArgs(
                    _mode,
                    _splitX,
                    _splitY,
                    _lastFrame?.Layout));
        }
    }

    private static SpreadsheetSplitPaneMode ToAdornerMode(
        SpreadsheetSplitViewMode mode) => mode switch
    {
        SpreadsheetSplitViewMode.None => SpreadsheetSplitPaneMode.None,
        SpreadsheetSplitViewMode.Vertical => SpreadsheetSplitPaneMode.Vertical,
        SpreadsheetSplitViewMode.Horizontal => SpreadsheetSplitPaneMode.Horizontal,
        SpreadsheetSplitViewMode.Both => SpreadsheetSplitPaneMode.Both,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        CancelSplitViewHistory(restoreBeforeState: true);
        CancelEditor();
        EnsureWorksheetSubscription();
        _engine?.InvalidateMetrics();
        ApplyStoredSplitState();
        _lastFrame = null;
        InvalidateVisual();
    }

    private void OnSelectionChanged(
        object? sender,
        NeraSelectionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnViewChanged(
        object? sender,
        SpreadsheetViewChangedEventArgs e)
    {
        if (_disposed ||
            _session is null ||
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _engine?.ClearDisplayListCache();
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnSplitViewChanged(
        object? sender,
        SpreadsheetSplitViewChangedEventArgs e)
    {
        if (_disposed ||
            _session is null ||
            ReferenceEquals(e.Source, this) ||
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        ApplySplitViewState(e.State);
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e) =>
        HandleCellsChanged(e);

    private void OnDimensionsChanged(
        object? sender,
        DimensionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _engine?.InvalidateMetrics();
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
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

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_disposed ||
            _engine is null ||
            e is not RenderingEventArgs renderingEventArgs)
        {
            return;
        }

        var before = CaptureVisiblePaneScrolls();
        var currentTime = renderingEventArgs.RenderingTime;
        var elapsed = _lastRenderingTime is null
            ? TimeSpan.FromSeconds(1d / 60d)
            : currentTime - _lastRenderingTime.Value;
        _lastRenderingTime = currentTime;
        var changed = _engine.AdvanceScrollFrame(elapsed);
        if (changed)
        {
            PublishChangedPaneScrolls(before);
            PersistCurrentSplitState(
                SpreadsheetSplitViewChangeKind.PaneScroll);
            _lastFrame = null;
            UpdateEditorBounds();
            InvalidateVisual();
        }

        if (!_engine.HasPendingScroll)
        {
            DetachFrameLoop();
            CommitSplitViewHistoryWhenFrameSettles();
        }
    }

    private Dictionary<SpreadsheetPaneId, ScrollSnapshot>
        CaptureVisiblePaneScrolls()
    {
        var snapshots = new Dictionary<SpreadsheetPaneId, ScrollSnapshot>();
        if (_lastFrame is null || _engine is null)
        {
            return snapshots;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            snapshots[pane.Pane.PaneId] =
                _engine.GetPaneScrollSnapshot(pane.Pane.PaneId);
        }
        return snapshots;
    }

    private void PublishChangedPaneScrolls(
        Dictionary<SpreadsheetPaneId, ScrollSnapshot> before)
    {
        if (_engine is null || _lastFrame is null)
        {
            return;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            var paneId = pane.Pane.PaneId;
            var current = _engine.GetPaneScrollSnapshot(paneId);
            if (!before.TryGetValue(paneId, out var previous) ||
                previous != current)
            {
                PaneScrollChanged?.Invoke(
                    this,
                    new SpreadsheetPaneScrollChangedEventArgs(
                        paneId,
                        current));
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        SynchronizeSession();
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

        CommitSplitViewHistory();
        DetachFrameLoop();
        DetachSessionEvents();
    }
}
