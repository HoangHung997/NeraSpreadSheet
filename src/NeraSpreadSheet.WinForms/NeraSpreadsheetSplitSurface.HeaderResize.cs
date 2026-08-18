using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private SpreadsheetSplitHeaderResizeHandle? _headerResize;

    private bool TryBeginHeaderResize(double clientX, double clientY)
    {
        if (!TryGetHeaderResizeHandle(clientX, clientY, out var handle))
        {
            return false;
        }

        _headerResize = handle;
        SetActivePaneCore(handle.PaneId);
        Capture = true;
        Cursor = GetHeaderResizeCursor(handle.Axis);
        return true;
    }

    private bool TryGetHeaderResizeHandle(
        double clientX,
        double clientY,
        out SpreadsheetSplitHeaderResizeHandle handle)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            handle = default;
            return false;
        }

        return SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme,
            CreatePaneChromeLayouts(frame),
            out handle);
    }

    private void ApplyHeaderResize(
        SpreadsheetSplitHeaderResizeHandle handle,
        double clientX,
        double clientY)
    {
        if (_session is null)
        {
            return;
        }

        var size = SpreadsheetSplitHeaderResizeGeometry.CalculateSize(
            handle,
            clientX,
            clientY);
        if (handle.Axis == WorksheetAxis.Row)
        {
            _session.ActiveWorksheet.Dimensions.SetRowHeight(handle.Index, size);
        }
        else
        {
            _session.ActiveWorksheet.Dimensions.SetColumnWidth(handle.Index, size);
        }
    }

    private void UpdatePointerCursor(double clientX, double clientY)
    {
        var separator = HitTestSeparator(clientX, clientY);
        if (separator is { } split)
        {
            Cursor = GetSeparatorCursor(split.Vertical, split.Horizontal);
            return;
        }

        Cursor = TryGetHeaderResizeHandle(clientX, clientY, out var handle)
            ? GetHeaderResizeCursor(handle.Axis)
            : Cursors.Default;
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
