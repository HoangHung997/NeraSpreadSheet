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
        InvalidateSplitLayout();
        SplitChanged?.Invoke(
            this,
            new SpreadsheetSplitChangedEventArgs(mode, splitX, splitY, _lastFrame?.Layout));
    }

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

        DetachSessionEvents();
        _cellEditor?.Cancel();
        _session = next;
        _engine = next is null ? null : new SpreadsheetSplitViewportEngine(next);
        _cellEditor = next?.Editor;
        _lastFrame = null;
        HideEditor();
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
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged;
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
        CancelEditor();
        EnsureWorksheetSubscription();
        _engine?.InvalidateMetrics();
        _engine?.ResetPaneScrolls();
        _lastFrame = null;
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (_session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _engine?.ClearDisplayListCache();
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        _lastFrame = null;
        Invalidate();
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
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
            return;
        }

        var before = CaptureVisiblePaneScrolls();
        var now = DateTime.UtcNow;
        var changed = _engine.AdvanceScrollFrame(now - _lastFrameUtc);
        _lastFrameUtc = now;
        if (changed)
        {
            PublishChangedPaneScrolls(before);
            _lastFrame = null;
            UpdateEditorBounds();
            Invalidate();
        }

        if (!_engine.HasPendingScroll)
        {
            _frameTimer.Stop();
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
            if (!before.TryGetValue(paneId, out var previous) || previous != current)
            {
                PaneScrollChanged?.Invoke(
                    this,
                    new SpreadsheetPaneScrollChangedEventArgs(paneId, current));
            }
        }
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

}
