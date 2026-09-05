using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private bool _applyingSplitViewState;

    private bool IsEditing => _cellEditor?.IsEditing == true;

    private SpreadsheetSplitViewportEngine GetEngine()
    {
        SynchronizeSession();
        return _engine ?? throw new InvalidOperationException(
            "A spreadsheet session is required before split-pane operations can run.");
    }

    private SpreadsheetSplitViewportFrame? EnsureFrame()
    {
        SynchronizeSession();
        if (_engine is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
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
        SpreadsheetChromeGeometry.Calculate(
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);

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
        PersistCurrentSplitState(SpreadsheetSplitViewChangeKind.Topology);
        InvalidateSplitLayout();
        SplitChanged?.Invoke(
            this,
            new SpreadsheetSplitChangedEventArgs(
                mode,
                splitX,
                splitY,
                _lastFrame?.Layout));
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
        Invalidate();
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

        var nextMode = ToSurfaceMode(state.Mode);
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

    private static SpreadsheetSplitPaneMode ToSurfaceMode(
        SpreadsheetSplitViewMode mode) => mode switch
    {
        SpreadsheetSplitViewMode.None => SpreadsheetSplitPaneMode.None,
        SpreadsheetSplitViewMode.Vertical => SpreadsheetSplitPaneMode.Vertical,
        SpreadsheetSplitViewMode.Horizontal => SpreadsheetSplitPaneMode.Horizontal,
        SpreadsheetSplitViewMode.Both => SpreadsheetSplitPaneMode.Both,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private void InvalidateSplitLayout()
    {
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

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
    }

    private void AttachSessionEvents()
    {
        if (_session is null)
        {
            return;
        }

        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged;
        _session.View.SplitChanged += OnSplitViewChanged;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged;
            _session.View.SplitChanged -= OnSplitViewChanged;
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
        CancelSplitViewHistory(restoreBeforeState: true);
        CancelEditor();
        EnsureWorksheetSubscription();
        _engine?.InvalidateMetrics();
        ApplyStoredSplitState();
        _lastFrame = null;
        Invalidate();
    }

    private void OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnViewChanged(
        object? sender,
        SpreadsheetViewChangedEventArgs e)
    {
        if (_session is null ||
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _engine?.ClearDisplayListCache();
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnSplitViewChanged(
        object? sender,
        SpreadsheetSplitViewChangedEventArgs e)
    {
        if (_session is null ||
            ReferenceEquals(e.Source, this) ||
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        ApplySplitViewState(e.State);
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        InvalidateDirtyRange(e.Range);
    }

    private void OnDimensionsChanged(
        object? sender,
        DimensionChangedEventArgs e)
    {
        _engine?.InvalidateMetrics();
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private void StartFrameLoop()
    {
        _lastFrameUtc = DateTime.UtcNow;
        _frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        if (_engine is null)
        {
            _frameTimer.Stop();
            CancelSplitViewHistory(restoreBeforeState: true);
            return;
        }

        var before = CaptureVisiblePaneScrolls();
        var now = DateTime.UtcNow;
        var changed = _engine.AdvanceScrollFrame(now - _lastFrameUtc);
        _lastFrameUtc = now;
        if (changed)
        {
            PublishChangedPaneScrolls(before);
            PersistCurrentSplitState(
                SpreadsheetSplitViewChangeKind.PaneScroll);
            _lastFrame = null;
            UpdateEditorBounds();
            Invalidate();
        }

        if (!_engine.HasPendingScroll)
        {
            _frameTimer.Stop();
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

    private static void ValidateSplitCoordinate(
        double? value,
        string parameterName)
    {
        if (value is { } coordinate && !double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Split coordinates must be finite.");
        }
    }
}
