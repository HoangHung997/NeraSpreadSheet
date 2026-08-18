using System.Windows.Documents;
using System.Windows.Input;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private SpreadsheetSplitHeaderResizeHandle? _headerResize;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (_disposed || _session is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (HitTestSeparator(point.X, point.Y) is not null ||
            !TryGetHeaderResizeHandle(point.X, point.Y, out var handle))
        {
            return;
        }

        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();
        _headerResize = handle;
        SetActivePaneCore(handle.PaneId);
        CaptureMouse();
        Cursor = GetHeaderResizeCursor(handle.Axis);
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_headerResize is { } resize)
        {
            if (!IsMouseCaptured)
            {
                _headerResize = null;
                UpdateHeaderPointerCursor(point.X, point.Y);
                return;
            }

            ApplyHeaderResize(resize, point.X, point.Y);
            Cursor = GetHeaderResizeCursor(resize.Axis);
            e.Handled = true;
            return;
        }

        if (_splitDrag is null &&
            HitTestSeparator(point.X, point.Y) is null &&
            TryGetHeaderResizeHandle(point.X, point.Y, out var handle))
        {
            Cursor = GetHeaderResizeCursor(handle.Axis);
            e.Handled = true;
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_disposed || _headerResize is not { } resize)
        {
            return;
        }

        var point = e.GetPosition(this);
        ApplyHeaderResize(resize, point.X, point.Y);
        _headerResize = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        UpdateHeaderPointerCursor(point.X, point.Y);
        e.Handled = true;
    }

    private bool TryGetHeaderResizeHandle(
        double controlX,
        double controlY,
        out SpreadsheetSplitHeaderResizeHandle handle)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            handle = default;
            return false;
        }

        return SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            controlX,
            controlY,
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme,
            CreatePaneChromeLayouts(frame),
            out handle);
    }

    private void ApplyHeaderResize(
        SpreadsheetSplitHeaderResizeHandle handle,
        double controlX,
        double controlY)
    {
        if (_session is null)
        {
            return;
        }

        var size = SpreadsheetSplitHeaderResizeGeometry.CalculateSize(
            handle,
            controlX,
            controlY);
        if (handle.Axis == WorksheetAxis.Row)
        {
            _session.ActiveWorksheet.Dimensions.SetRowHeight(handle.Index, size);
        }
        else
        {
            _session.ActiveWorksheet.Dimensions.SetColumnWidth(handle.Index, size);
        }
    }

    private void UpdateHeaderPointerCursor(double controlX, double controlY)
    {
        var separator = HitTestSeparator(controlX, controlY);
        if (separator is { } split)
        {
            Cursor = GetSeparatorCursor(split.Vertical, split.Horizontal);
            return;
        }

        Cursor = TryGetHeaderResizeHandle(controlX, controlY, out var handle)
            ? GetHeaderResizeCursor(handle.Axis)
            : null;
    }

    private static Cursor GetHeaderResizeCursor(WorksheetAxis axis) =>
        axis == WorksheetAxis.Row ? Cursors.SizeNS : Cursors.SizeWE;

    private static SpreadsheetSplitPaneChromeLayout[] CreatePaneChromeLayouts(
        SpreadsheetSplitViewportFrame frame)
    {
        var paneLayouts = new SpreadsheetSplitPaneChromeLayout[frame.Panes.Count];
        for (var index = 0; index < frame.Panes.Count; index++)
        {
            var pane = frame.Panes[index];
            paneLayouts[index] = new SpreadsheetSplitPaneChromeLayout(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ViewportFrame.Layout);
        }
        return paneLayouts;
    }
}
