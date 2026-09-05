using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private HeaderReorderState? _headerReorder;
    private SpreadsheetSplitHeaderReorderDropTarget? _headerReorderDropTarget;
    private System.Windows.Forms.Timer? _headerReorderAutoScrollTimer;
    private DateTime _headerReorderLastAutoScrollUtc;
    private double _headerReorderPointerX;
    private double _headerReorderPointerY;

    private bool TryBeginHeaderReorderCandidate(double clientX, double clientY)
    {
        if (_session is null ||
            !TryGetHeaderReorderSource(
                clientX,
                clientY,
                out var source))
        {
            return false;
        }

        var (sourceIndex, count) = ResolveReorderSourceRange(
            source.Axis,
            source.Index);
        _headerReorder = new HeaderReorderState(
            source.PaneId,
            source.Axis,
            sourceIndex,
            count,
            new PointD(clientX, clientY),
            IsActive: false);
        _headerReorderPointerX = clientX;
        _headerReorderPointerY = clientY;
        _headerReorderDropTarget = null;
        return true;
    }

    private bool UpdateHeaderReorder(
        double clientX,
        double clientY,
        bool leftButtonPressed)
    {
        _headerReorderPointerX = clientX;
        _headerReorderPointerY = clientY;
        if (_headerReorder is not { } state)
        {
            return false;
        }
        if (!leftButtonPressed)
        {
            CancelHeaderReorder();
            return false;
        }

        if (!state.IsActive)
        {
            if (!SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                    state.StartPoint,
                    new PointD(clientX, clientY)))
            {
                return false;
            }

            state = state with { IsActive = true };
            _headerReorder = state;
            Capture = true;
        }

        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            _headerReorderDropTarget = null;
            UpdateHeaderReorderAutoScrollTimer();
            return true;
        }

        if (SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
                state.Axis,
                state.SourceIndex,
                state.Count,
                clientX,
                clientY,
                ClientSize.Width,
                ClientSize.Height,
                _owner.RenderTheme,
                CreatePaneChromeLayouts(frame),
                out var target))
        {
            _headerReorderDropTarget = target;
            SetActivePaneCore(target.PaneId);
        }
        else
        {
            _headerReorderDropTarget = null;
        }

        Cursor = Cursors.SizeAll;
        UpdateHeaderReorderAutoScrollTimer();
        Invalidate();
        return true;
    }

    private bool CompleteHeaderReorder(double clientX, double clientY)
    {
        if (_headerReorder is not { } state)
        {
            return false;
        }

        var wasActive = state.IsActive;
        if (wasActive)
        {
            UpdateHeaderReorder(
                clientX,
                clientY,
                leftButtonPressed: true);
        }
        var target = _headerReorderDropTarget;
        ClearHeaderReorderState(releaseCapture: true);
        Invalidate();

        if (wasActive && target is { IsNoOp: false } && _session is not null)
        {
            try
            {
                _session.Reorder.Move(target.Value.Move);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or ArgumentException)
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }
        return wasActive;
    }

    private void CancelHeaderReorder()
    {
        var wasActive = _headerReorder is { IsActive: true };
        ClearHeaderReorderState(releaseCapture: true);
        if (wasActive)
        {
            Invalidate();
        }
    }

    private void ClearHeaderReorderState(bool releaseCapture)
    {
        var wasActive = _headerReorder is { IsActive: true };
        _headerReorder = null;
        _headerReorderDropTarget = null;
        DisposeHeaderReorderAutoScrollTimer();
        if (releaseCapture && wasActive && Capture)
        {
            Capture = false;
        }
    }

    private bool TryGetHeaderReorderSource(
        double clientX,
        double clientY,
        out SpreadsheetSplitHeaderReorderSource source)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null ||
            TryGetHeaderResizeHandle(clientX, clientY, out _))
        {
            source = default;
            return false;
        }

        return SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme,
            CreatePaneChromeLayouts(frame),
            out source);
    }

    private (int SourceIndex, int Count) ResolveReorderSourceRange(
        WorksheetAxis axis,
        int hitIndex)
    {
        if (_session?.Selection.Ranges.Count == 1)
        {
            var range = _session.Selection.Ranges[0];
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

    private void UpdateHeaderReorderAutoScrollTimer()
    {
        var velocity = GetHeaderReorderAutoScrollVelocity();
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            _headerReorderAutoScrollTimer?.Stop();
            return;
        }

        if (_headerReorderAutoScrollTimer is null)
        {
            _headerReorderAutoScrollTimer = new System.Windows.Forms.Timer
            {
                Interval = 16,
            };
            _headerReorderAutoScrollTimer.Tick +=
                OnHeaderReorderAutoScrollTick;
        }
        if (_headerReorderAutoScrollTimer.Enabled)
        {
            return;
        }

        _headerReorderLastAutoScrollUtc = DateTime.UtcNow;
        _headerReorderAutoScrollTimer.Start();
    }

    private PointD GetHeaderReorderAutoScrollVelocity()
    {
        if (_headerReorder is not { IsActive: true } state)
        {
            return default;
        }

        var frame = _lastFrame ?? EnsureFrame();
        var paneId = _headerReorderDropTarget?.PaneId ?? state.SourcePaneId;
        if (frame is null || !frame.TryGetPane(paneId, out var pane))
        {
            return default;
        }

        var chrome = GetChromeMetrics();
        return SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            state.Axis,
            new PointD(
                _headerReorderPointerX,
                _headerReorderPointerY),
            new RectD(
                chrome.RowHeaderWidth + pane.Pane.Bounds.Left,
                chrome.ColumnHeaderHeight + pane.Pane.Bounds.Top,
                pane.Pane.Bounds.Width,
                pane.Pane.Bounds.Height));
    }

    private void OnHeaderReorderAutoScrollTick(
        object? sender,
        EventArgs e)
    {
        if (_headerReorder is not { IsActive: true } state)
        {
            DisposeHeaderReorderAutoScrollTimer();
            return;
        }

        var velocity = GetHeaderReorderAutoScrollVelocity();
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            _headerReorderAutoScrollTimer?.Stop();
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = now - _headerReorderLastAutoScrollUtc;
        _headerReorderLastAutoScrollUtc = now;
        if (elapsed > TimeSpan.FromMilliseconds(100d))
        {
            elapsed = TimeSpan.FromMilliseconds(100d);
        }

        var delta = SpreadsheetHeaderReorderAutoScroll.CalculateDelta(
            velocity,
            elapsed);
        var paneId = _headerReorderDropTarget?.PaneId ?? state.SourcePaneId;
        var current = GetPaneScroll(paneId);
        ScrollPaneTo(
            paneId,
            current.X + delta.X,
            current.Y + delta.Y,
            animated: false);
        UpdateHeaderReorder(
            _headerReorderPointerX,
            _headerReorderPointerY,
            leftButtonPressed: true);
    }

    private void DisposeHeaderReorderAutoScrollTimer()
    {
        var timer = _headerReorderAutoScrollTimer;
        _headerReorderAutoScrollTimer = null;
        if (timer is null)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnHeaderReorderAutoScrollTick;
        timer.Dispose();
    }

    private readonly record struct HeaderReorderState(
        SpreadsheetPaneId SourcePaneId,
        WorksheetAxis Axis,
        int SourceIndex,
        int Count,
        PointD StartPoint,
        bool IsActive);
}
