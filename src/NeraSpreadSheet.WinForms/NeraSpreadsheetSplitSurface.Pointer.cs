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
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SynchronizeSession();
        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }

        var paneId = ResolvePaneAtClientPoint(e.X, e.Y, frame) ?? frame.ActivePane;
        GetEngine().SetActivePane(paneId);
        var notches = e.Delta / 120d;
        var delta = -notches * _owner.WheelPixelsPerNotch;
        GetEngine().QueuePaneScroll(
            paneId,
            (ModifierKeys & Keys.Shift) != 0
                ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
                : new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        StartFrameLoop();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        SynchronizeSession();
        if (e.Button != MouseButtons.Left || _session is null)
        {
            return;
        }

        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        if (TryBeginSeparatorDrag(e.X, e.Y))
        {
            return;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            e.X,
            e.Y,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                return;
            case SpreadsheetChromeRegion.RowHeader:
                if (TryHitTestRowHeader(frame, chromeHit.BodyY, out var rowPane, out var rowIndex))
                {
                    GetEngine().SetActivePane(rowPane);
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendRowsTo(rowIndex);
                    }
                    else
                    {
                        _session.Selection.SelectRow(
                            rowIndex,
                            additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (TryHitTestColumnHeader(
                    frame,
                    chromeHit.BodyX,
                    out var columnPane,
                    out var columnIndex))
                {
                    GetEngine().SetActivePane(columnPane);
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendColumnsTo(columnIndex);
                    }
                    else
                    {
                        _session.Selection.SelectColumn(
                            columnIndex,
                            additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        GetEngine().TryActivatePaneAt(chromeHit.BodyX, chromeHit.BodyY);
        if (!GetEngine().TryHitTest(
            chromeHit.BodyX,
            chromeHit.BodyY,
            out _,
            out var address))
        {
            return;
        }

        if ((ModifierKeys & Keys.Shift) != 0)
        {
            _session.Selection.ExtendTo(address);
        }
        else if ((ModifierKeys & Keys.Control) != 0)
        {
            _session.Selection.AddRange(new CellRange(address, address));
        }
        else
        {
            _session.Selection.SetActiveCell(address);
        }

        if (e.Clicks >= 2)
        {
            BeginEdit();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_splitDrag is { } drag)
        {
            ApplySeparatorDrag(drag, e.X, e.Y);
            Cursor = GetSeparatorCursor(drag.Vertical, drag.Horizontal);
            return;
        }

        var separator = HitTestSeparator(e.X, e.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _splitDrag is not { } drag)
        {
            return;
        }

        ApplySeparatorDrag(drag, e.X, e.Y);
        _splitDrag = null;
        Capture = false;
        var separator = HitTestSeparator(e.X, e.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : Cursors.Default;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture || _splitDrag is null)
        {
            return;
        }

        _splitDrag = null;
        Cursor = Cursors.Default;
    }

    private bool TryBeginSeparatorDrag(double clientX, double clientY)
    {
        var hit = HitTestSeparator(clientX, clientY);
        if (hit is null)
        {
            return false;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return false;
        }

        var chrome = GetChromeMetrics();
        var bodyX = clientX - chrome.RowHeaderWidth;
        var bodyY = clientY - chrome.ColumnHeaderHeight;
        _splitDrag = new SplitDragState(
            hit.Value.Vertical,
            hit.Value.Horizontal,
            hit.Value.Vertical && frame.Layout.SplitX is { } splitX ? bodyX - splitX : 0d,
            hit.Value.Horizontal && frame.Layout.SplitY is { } splitY ? bodyY - splitY : 0d);
        Capture = true;
        Cursor = GetSeparatorCursor(hit.Value.Vertical, hit.Value.Horizontal);
        return true;
    }

    private SeparatorHit? HitTestSeparator(double clientX, double clientY)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            return null;
        }

        var chrome = GetChromeMetrics();
        var bodyX = clientX - chrome.RowHeaderWidth;
        var bodyY = clientY - chrome.ColumnHeaderHeight;
        var vertical = frame.Layout.HasVerticalSplit &&
            clientX >= chrome.RowHeaderWidth &&
            clientY >= 0d &&
            clientY < chrome.FullHeight &&
            bodyX >= frame.Layout.VerticalSeparator.Left &&
            bodyX < frame.Layout.VerticalSeparator.Right;
        var horizontal = frame.Layout.HasHorizontalSplit &&
            clientY >= chrome.ColumnHeaderHeight &&
            clientX >= 0d &&
            clientX < chrome.FullWidth &&
            bodyY >= frame.Layout.HorizontalSeparator.Top &&
            bodyY < frame.Layout.HorizontalSeparator.Bottom;
        return vertical || horizontal ? new SeparatorHit(vertical, horizontal) : null;
    }

    private void ApplySeparatorDrag(
        SplitDragState drag,
        double clientX,
        double clientY)
    {
        var chrome = GetChromeMetrics();
        var nextX = drag.Vertical
            ? clientX - chrome.RowHeaderWidth - drag.GrabOffsetX
            : _splitX;
        var nextY = drag.Horizontal
            ? clientY - chrome.ColumnHeaderHeight - drag.GrabOffsetY
            : _splitY;
        SetSplit(nextX, nextY);
    }

    private SpreadsheetPaneId? ResolvePaneAtClientPoint(
        double clientX,
        double clientY,
        SpreadsheetSplitViewportFrame frame)
    {
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Body:
            {
                var hit = frame.Layout.HitTest(new PointD(chromeHit.BodyX, chromeHit.BodyY));
                return hit.RegionKind == SpreadsheetSplitHitRegionKind.Pane ? hit.PaneId : null;
            }
            case SpreadsheetChromeRegion.RowHeader:
                return TryResolveLeftPane(frame, chromeHit.BodyY, out var rowPane) ? rowPane.Pane.PaneId : null;
            case SpreadsheetChromeRegion.ColumnHeader:
                return TryResolveTopPane(frame, chromeHit.BodyX, out var columnPane) ? columnPane.Pane.PaneId : null;
            default:
                return frame.ActivePane;
        }
    }

    private static bool TryHitTestRowHeader(
        SpreadsheetSplitViewportFrame frame,
        double bodyY,
        out SpreadsheetPaneId paneId,
        out int rowIndex)
    {
        if (TryResolveLeftPane(frame, bodyY, out var pane) &&
            TryHitAxisSlot(
                pane.ViewportFrame.Layout.Rows,
                bodyY - pane.Pane.Bounds.Y,
                out rowIndex))
        {
            paneId = pane.Pane.PaneId;
            return true;
        }

        paneId = default;
        rowIndex = default;
        return false;
    }

    private static bool TryHitTestColumnHeader(
        SpreadsheetSplitViewportFrame frame,
        double bodyX,
        out SpreadsheetPaneId paneId,
        out int columnIndex)
    {
        if (TryResolveTopPane(frame, bodyX, out var pane) &&
            TryHitAxisSlot(
                pane.ViewportFrame.Layout.Columns,
                bodyX - pane.Pane.Bounds.X,
                out columnIndex))
        {
            paneId = pane.Pane.PaneId;
            return true;
        }

        paneId = default;
        columnIndex = default;
        return false;
    }

    private static bool TryResolveLeftPane(
        SpreadsheetSplitViewportFrame frame,
        double bodyY,
        out SpreadsheetSplitPaneFrame pane)
    {
        foreach (var candidate in frame.Panes)
        {
            if (Math.Abs(candidate.Pane.Bounds.Left) <= GeometryEpsilon &&
                bodyY >= candidate.Pane.Bounds.Top &&
                bodyY < candidate.Pane.Bounds.Bottom)
            {
                pane = candidate;
                return true;
            }
        }

        pane = null!;
        return false;
    }

    private static bool TryResolveTopPane(
        SpreadsheetSplitViewportFrame frame,
        double bodyX,
        out SpreadsheetSplitPaneFrame pane)
    {
        foreach (var candidate in frame.Panes)
        {
            if (Math.Abs(candidate.Pane.Bounds.Top) <= GeometryEpsilon &&
                bodyX >= candidate.Pane.Bounds.Left &&
                bodyX < candidate.Pane.Bounds.Right)
            {
                pane = candidate;
                return true;
            }
        }

        pane = null!;
        return false;
    }

    private static bool TryHitAxisSlot(
        IReadOnlyList<AxisSlot> slots,
        double coordinate,
        out int index)
    {
        foreach (var slot in slots)
        {
            if (coordinate >= slot.Start && coordinate < slot.End)
            {
                index = slot.Index;
                return true;
            }
        }

        index = default;
        return false;
    }

    private static Cursor GetSeparatorCursor(bool vertical, bool horizontal) =>
        (vertical, horizontal) switch
        {
            (true, true) => Cursors.SizeAll,
            (true, false) => Cursors.VSplit,
            (false, true) => Cursors.HSplit,
            _ => Cursors.Default,
        };

    private readonly record struct SeparatorHit(bool Vertical, bool Horizontal);

    private readonly record struct SplitDragState(
        bool Vertical,
        bool Horizontal,
        double GrabOffsetX,
        double GrabOffsetY);

}
