using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private HeaderReorderState? _headerReorder;
    private SpreadsheetSplitHeaderReorderDropTarget? _headerReorderDropTarget;
    private DrawingVisual? _headerReorderPreviewVisual;
    private TimeSpan? _headerReorderLastAutoScrollRenderingTime;
    private bool _headerReorderOwnsMouseCapture;
    private bool _headerReorderAutoScrollAttached;
    private double _headerReorderPointerX;
    private double _headerReorderPointerY;

    private bool TryBeginHeaderReorderCandidate(
        double controlX,
        double controlY)
    {
        if (_session is null ||
            !TryGetHeaderReorderSource(
                controlX,
                controlY,
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
            new PointD(controlX, controlY),
            IsActive: false);
        _headerReorderPointerX = controlX;
        _headerReorderPointerY = controlY;
        _headerReorderDropTarget = null;
        return true;
    }

    private bool UpdateHeaderReorder(
        double controlX,
        double controlY,
        bool leftButtonPressed)
    {
        _headerReorderPointerX = controlX;
        _headerReorderPointerY = controlY;
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
                    new PointD(controlX, controlY)))
            {
                return false;
            }

            state = state with { IsActive = true };
            _headerReorder = state;
            TryCaptureHeaderReorderMouse();
        }

        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            _headerReorderDropTarget = null;
            UpdateHeaderReorderPreviewVisual();
            UpdateHeaderReorderAutoScrollSubscription();
            return true;
        }

        if (SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
                state.Axis,
                state.SourceIndex,
                state.Count,
                controlX,
                controlY,
                ActualWidth,
                ActualHeight,
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
        UpdateHeaderReorderPreviewVisual();
        UpdateHeaderReorderAutoScrollSubscription();
        return true;
    }

    private bool CompleteHeaderReorder(
        double controlX,
        double controlY)
    {
        if (_headerReorder is not { } state)
        {
            return false;
        }

        var wasActive = state.IsActive;
        if (wasActive)
        {
            UpdateHeaderReorder(
                controlX,
                controlY,
                leftButtonPressed: true);
        }
        var target = _headerReorderDropTarget;
        ClearHeaderReorderState(releaseCapture: true);

        if (wasActive &&
            target is { IsNoOp: false } &&
            _session is not null)
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

    private void CancelHeaderReorder() =>
        ClearHeaderReorderState(releaseCapture: true);

    private void TryCaptureHeaderReorderMouse()
    {
        if (_headerReorderOwnsMouseCapture ||
            Mouse.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!IsMouseCaptured && !CaptureMouse())
        {
            return;
        }
        if (!IsMouseCaptured)
        {
            return;
        }

        _headerReorderOwnsMouseCapture = true;
        LostMouseCapture -= OnHeaderReorderLostMouseCapture;
        LostMouseCapture += OnHeaderReorderLostMouseCapture;
    }

    private void ClearHeaderReorderState(bool releaseCapture)
    {
        var ownsCapture = _headerReorderOwnsMouseCapture;
        _headerReorder = null;
        _headerReorderDropTarget = null;
        RemoveHeaderReorderPreviewVisual();
        DetachHeaderReorderAutoScroll();
        LostMouseCapture -= OnHeaderReorderLostMouseCapture;
        _headerReorderOwnsMouseCapture = false;
        if (releaseCapture && ownsCapture && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void OnHeaderReorderLostMouseCapture(
        object sender,
        MouseEventArgs e)
    {
        if (!_headerReorderOwnsMouseCapture ||
            _headerReorder is not { IsActive: true })
        {
            return;
        }

        LostMouseCapture -= OnHeaderReorderLostMouseCapture;
        _headerReorderOwnsMouseCapture = false;
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            ClearHeaderReorderState(releaseCapture: false);
            Cursor = null;
        }
    }

    private bool TryGetHeaderReorderSource(
        double controlX,
        double controlY,
        out SpreadsheetSplitHeaderReorderSource source)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null ||
            TryGetHeaderResizeHandle(controlX, controlY, out _))
        {
            source = default;
            return false;
        }

        return SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            controlX,
            controlY,
            ActualWidth,
            ActualHeight,
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

    private void UpdateHeaderReorderPreviewVisual()
    {
        if (_headerReorderDropTarget is not { } target)
        {
            RemoveHeaderReorderPreviewVisual();
            return;
        }

        if (_headerReorderPreviewVisual is null)
        {
            _headerReorderPreviewVisual = new DrawingVisual();
            _visuals.Add(_headerReorderPreviewVisual);
        }

        var sourceColor = target.IsNoOp
            ? _owner.RenderTheme.HeaderBorder
            : _owner.RenderTheme.ActivePaneBorder;
        var brush = new SolidColorBrush(Color.FromArgb(
            sourceColor.Alpha,
            sourceColor.Red,
            sourceColor.Green,
            sourceColor.Blue));
        brush.Freeze();
        using var drawing = _headerReorderPreviewVisual.RenderOpen();
        drawing.DrawRectangle(
            brush,
            null,
            new System.Windows.Rect(
                target.PreviewBounds.X,
                target.PreviewBounds.Y,
                target.PreviewBounds.Width,
                target.PreviewBounds.Height));
    }

    private void RemoveHeaderReorderPreviewVisual()
    {
        if (_headerReorderPreviewVisual is null)
        {
            return;
        }

        _visuals.Remove(_headerReorderPreviewVisual);
        _headerReorderPreviewVisual = null;
    }

    private void UpdateHeaderReorderAutoScrollSubscription()
    {
        var velocity = GetHeaderReorderAutoScrollVelocity();
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            DetachHeaderReorderAutoScroll();
            return;
        }
        if (_headerReorderAutoScrollAttached)
        {
            return;
        }

        _headerReorderLastAutoScrollRenderingTime = null;
        CompositionTarget.Rendering += OnHeaderReorderAutoScrollRendering;
        _headerReorderAutoScrollAttached = true;
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

    private void OnHeaderReorderAutoScrollRendering(
        object? sender,
        EventArgs e)
    {
        if (_headerReorder is not { IsActive: true } state ||
            e is not RenderingEventArgs rendering)
        {
            DetachHeaderReorderAutoScroll();
            return;
        }

        var velocity = GetHeaderReorderAutoScrollVelocity();
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            DetachHeaderReorderAutoScroll();
            return;
        }

        var elapsed = _headerReorderLastAutoScrollRenderingTime is null
            ? TimeSpan.FromSeconds(1d / 60d)
            : rendering.RenderingTime -
              _headerReorderLastAutoScrollRenderingTime.Value;
        _headerReorderLastAutoScrollRenderingTime = rendering.RenderingTime;
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

    private void DetachHeaderReorderAutoScroll()
    {
        if (!_headerReorderAutoScrollAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnHeaderReorderAutoScrollRendering;
        _headerReorderAutoScrollAttached = false;
        _headerReorderLastAutoScrollRenderingTime = null;
    }

    private readonly record struct HeaderReorderState(
        SpreadsheetPaneId SourcePaneId,
        WorksheetAxis Axis,
        int SourceIndex,
        int Count,
        PointD StartPoint,
        bool IsActive);
}
