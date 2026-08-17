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
    private void SynchronizeSession()
    {
        var next = _owner.Session;
        if (ReferenceEquals(_session, next))
        {
            EnsureWorksheetSubscription();
            return;
        }

        DetachSessionEvents();
        _cellEditor?.Cancel();
        _session = next;
        _engine = next is null ? null : new SpreadsheetSplitViewportEngine(next);
        _cellEditor = next?.Editor;
        _lastFrame = null;
        HideEditor();
        AttachSessionEvents();
        UpdateGpuSurfaceVisibility();
    }

    private void AttachSessionEvents()
    {
        if (_session is null || _sessionEventsAttached || _disposed || !IsLoaded)
        {
            return;
        }

        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged;
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
        }
        _sessionEventsAttached = false;
        DetachWorksheetSubscription();
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (!_sessionEventsAttached || ReferenceEquals(_subscribedWorksheet, worksheet))
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
        _engine?.InvalidateMetrics();
        _engine?.ResetPaneScrolls();
        _lastFrame = null;
        InvalidateVisual();
    }

    private void OnSelectionChanged(object? sender, NeraSelectionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (_disposed || _session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _engine?.ClearDisplayListCache();
        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _lastFrame = null;
        InvalidateVisual();
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
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
        if (_disposed || _engine is null || e is not RenderingEventArgs renderingEventArgs)
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
            _lastFrame = null;
            UpdateEditorBounds();
            InvalidateVisual();
        }

        if (!_engine.HasPendingScroll)
        {
            DetachFrameLoop();
        }
    }

    private Dictionary<SpreadsheetPaneId, ScrollSnapshot> CaptureVisiblePaneScrolls()
    {
        var snapshots = new Dictionary<SpreadsheetPaneId, ScrollSnapshot>();
        if (_lastFrame is null || _engine is null)
        {
            return snapshots;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            snapshots[pane.Pane.PaneId] = _engine.GetPaneScrollSnapshot(pane.Pane.PaneId);
        }
        return snapshots;
    }

    private void PublishChangedPaneScrolls(
        IReadOnlyDictionary<SpreadsheetPaneId, ScrollSnapshot> before)
    {
        if (_engine is null || _lastFrame is null)
        {
            return;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            var paneId = pane.Pane.PaneId;
            var current = _engine.GetPaneScrollSnapshot(paneId);
            if (!before.TryGetValue(paneId, out var previous) || previous != current)
            {
                PaneScrollChanged?.Invoke(
                    this,
                    new SpreadsheetPaneScrollChangedEventArgs(paneId, current));
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

        DetachFrameLoop();
        DetachSessionEvents();
    }
}
