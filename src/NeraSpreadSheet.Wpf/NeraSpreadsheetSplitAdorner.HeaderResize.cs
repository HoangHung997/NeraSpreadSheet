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
        if (TryBeginScrollBarInteraction(point.X, point.Y))
        {
            e.Handled = true;
            return;
        }
        if (HitTestSeparator(point.X, point.Y) is not null)
        {
            return;
        }
        if (TryGetHeaderResizeHandle(point.X, point.Y, out var resize))
        {
            if (IsEditing)
            {
                CommitEditor();
            }
            Focus();
            _headerResize = resize;
            SetActivePaneCore(resize.PaneId);
            CaptureMouse();
            Cursor = GetHeaderResizeCursor(resize.Axis);
            e.Handled = true;
            return;
        }

        _ = TryBeginHeaderReorderCandidate(point.X, point.Y);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_scrollBarDrag is not null)
        {
            if (!IsMouseCaptured)
            {
                _scrollBarDrag = null;
                UpdateHeaderPointerCursor(point.X, point.Y);
                return;
            }

            UpdateScrollBarDrag(point.X, point.Y);
            Cursor = Cursors.Hand;
            e.Handled = true;
            return;
        }
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
        if (_headerReorder is not null &&
            UpdateHeaderReorder(
                point.X,
                point.Y,
                e.LeftButton == MouseButtonState.Pressed))
        {
            e.Handled = true;
            return;
        }

        UpdateHeaderPointerCursor(point.X, point.Y);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_scrollBarDrag is not null)
        {
            UpdateScrollBarDrag(point.X, point.Y);
            EndScrollBarDrag(persist: true);
            UpdateHeaderPointerCursor(point.X, point.Y);
            e.Handled = true;
            return;
        }
        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, point.X, point.Y);
            _headerResize = null;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            UpdateHeaderPointerCursor(point.X, point.Y);
            e.Handled = true;
            return;
        }
        if (_headerReorder is not null)
        {
            var reordered = CompleteHeaderReorder(point.X, point.Y);
            UpdateHeaderPointerCursor(point.X, point.Y);
            e.Handled = reordered;
        }
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
            _session.ActiveWorksheet.Dimensions.SetRowHeight(
                handle.Index,
                size);
        }
        else
        {
            _session.ActiveWorksheet.Dimensions.SetColumnWidth(
                handle.Index,
                size);
        }
    }

    private void UpdateHeaderPointerCursor(
        double controlX,
        double controlY)
    {
        if (_headerReorder is { IsActive: true })
        {
            Cursor = Cursors.SizeAll;
            return;
        }
        if (TryGetScrollBarHit(
            controlX,
            controlY,
            out _,
            out _,
            out _))
        {
            Cursor = Cursors.Hand;
            return;
        }

        var separator = HitTestSeparator(controlX, controlY);
        if (separator is { } split)
        {
            Cursor = GetSeparatorCursor(
                split.Vertical,
                split.Horizontal);
            return;
        }

        if (TryGetHeaderResizeHandle(
                controlX,
                controlY,
                out var resize))
        {
            Cursor = GetHeaderResizeCursor(resize.Axis);
            return;
        }
        Cursor = TryGetHeaderReorderSource(
            controlX,
            controlY,
            out _)
            ? Cursors.SizeAll
            : null;
    }

    private static Cursor GetHeaderResizeCursor(WorksheetAxis axis) =>
        axis == WorksheetAxis.Row ? Cursors.SizeNS : Cursors.SizeWE;

    private static SpreadsheetSplitPaneChromeLayout[]
        CreatePaneChromeLayouts(SpreadsheetSplitViewportFrame frame)
    {
        var paneLayouts = new SpreadsheetSplitPaneChromeLayout[
            frame.Panes.Count];
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
