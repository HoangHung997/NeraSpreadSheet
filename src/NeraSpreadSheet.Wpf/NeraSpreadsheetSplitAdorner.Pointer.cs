using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_disposed)
        {
            return;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        var paneId = ResolvePaneAtControlPoint(point.X, point.Y, frame) ?? frame.ActivePane;
        SetActivePaneCore(paneId);
        var notches = e.Delta / 120d;
        var delta = -notches * _owner.WheelPixelsPerNotch;
        GetEngine().QueuePaneScroll(
            paneId,
            (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
                : new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        EnsureFrameLoop();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        SynchronizeSession();
        if (_disposed || _session is null)
        {
            return;
        }

        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        var point = e.GetPosition(this);
        if (TryBeginSeparatorDrag(point.X, point.Y))
        {
            e.Handled = true;
            return;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }

        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            point.X,
            point.Y,
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.RowHeader:
                if (TryHitTestRowHeader(frame, chromeHit.BodyY, out var rowPane, out var rowIndex))
                {
                    SetActivePaneCore(rowPane);
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        _session.Selection.ExtendRowsTo(rowIndex);
                    }
                    else
                    {
                        _session.Selection.SelectRow(
                            rowIndex,
                            additive: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                    }
                }
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (TryHitTestColumnHeader(
                    frame,
                    chromeHit.BodyX,
                    out var columnPane,
                    out var columnIndex))
                {
                    SetActivePaneCore(columnPane);
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        _session.Selection.ExtendColumnsTo(columnIndex);
                    }
                    else
                    {
                        _session.Selection.SelectColumn(
                            columnIndex,
                            additive: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                    }
                }
                e.Handled = true;
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        var engine = GetEngine();
        if (engine.TryActivatePaneAt(chromeHit.BodyX, chromeHit.BodyY))
        {
            PersistCurrentSplitState(SpreadsheetSplitViewChangeKind.ActivePane);
            _lastFrame = null;
            InvalidateVisual();
        }
        if (!engine.TryHitTest(
            chromeHit.BodyX,
            chromeHit.BodyY,
            out _,
            out var address))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            _session.Selection.ExtendTo(address);
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _session.Selection.AddRange(new CellRange(address, address));
        }
        else
        {
            _session.Selection.SetActiveCell(address);
        }

        if (e.ClickCount >= 2)
        {
            BeginEdit();
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_disposed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_splitDrag is { } drag)
        {
            ApplySeparatorDrag(drag, point.X, point.Y);
            Cursor = GetSeparatorCursor(drag.Vertical, drag.Horizontal);
            e.Handled = true;
            return;
        }

        var separator = HitTestSeparator(point.X, point.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_disposed || _splitDrag is not { } drag)
        {
            return;
        }

        var point = e.GetPosition(this);
        ApplySeparatorDrag(drag, point.X, point.Y);
        _splitDrag = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        var separator = HitTestSeparator(point.X, point.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : null;
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_splitDrag is null)
        {
            return;
        }
        _splitDrag = null;
        Cursor = null;
    }

    private bool TryBeginSeparatorDrag(double controlX, double controlY)
    {
        var hit = HitTestSeparator(controlX, controlY);
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
        var bodyX = controlX - chrome.RowHeaderWidth;
        var bodyY = controlY - chrome.ColumnHeaderHeight;
        _splitDrag = new SplitDragState(
            hit.Value.Vertical,
            hit.Value.Horizontal,
            hit.Value.Vertical && frame.Layout.SplitX is { } splitX ? bodyX - splitX : 0d,
            hit.Value.Horizontal && frame.Layout.SplitY is { } splitY ? bodyY - splitY : 0d);
        CaptureMouse();
        Cursor = GetSeparatorCursor(hit.Value.Vertical, hit.Value.Horizontal);
        return true;
    }

    private SeparatorHit? HitTestSeparator(double controlX, double controlY)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            return null;
        }

        var chrome = GetChromeMetrics();
        var bodyX = controlX - chrome.RowHeaderWidth;
        var bodyY = controlY - chrome.ColumnHeaderHeight;
        var vertical = frame.Layout.HasVerticalSplit &&
            controlX >= chrome.RowHeaderWidth &&
            controlY >= 0d &&
            controlY < chrome.FullHeight &&
            bodyX >= frame.Layout.VerticalSeparator.Left &&
            bodyX < frame.Layout.VerticalSeparator.Right;
        var horizontal = frame.Layout.HasHorizontalSplit &&
            controlY >= chrome.ColumnHeaderHeight &&
            controlX >= 0d &&
            controlX < chrome.FullWidth &&
            bodyY >= frame.Layout.HorizontalSeparator.Top &&
            bodyY < frame.Layout.HorizontalSeparator.Bottom;
        return vertical || horizontal ? new SeparatorHit(vertical, horizontal) : null;
    }

    private void ApplySeparatorDrag(
        SplitDragState drag,
        double controlX,
        double controlY)
    {
        var chrome = GetChromeMetrics();
        var nextX = drag.Vertical
            ? controlX - chrome.RowHeaderWidth - drag.GrabOffsetX
            : _splitX;
        var nextY = drag.Horizontal
            ? controlY - chrome.ColumnHeaderHeight - drag.GrabOffsetY
            : _splitY;
        SetSplit(nextX, nextY);
    }

    private SpreadsheetPaneId? ResolvePaneAtControlPoint(
        double controlX,
        double controlY,
        SpreadsheetSplitViewportFrame frame)
    {
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            controlX,
            controlY,
            ActualWidth,
            ActualHeight,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Body:
            {
                var hit = frame.Layout.HitTest(new PointD(chromeHit.BodyX, chromeHit.BodyY));
                return hit.RegionKind == SpreadsheetSplitHitRegionKind.Pane ? hit.PaneId : null;
            }
            case SpreadsheetChromeRegion.RowHeader:
                return TryResolveLeftPane(frame, chromeHit.BodyY, out var rowPane)
                    ? rowPane.Pane.PaneId
                    : null;
            case SpreadsheetChromeRegion.ColumnHeader:
                return TryResolveTopPane(frame, chromeHit.BodyX, out var columnPane)
                    ? columnPane.Pane.PaneId
                    : null;
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
            (true, false) => Cursors.SizeWE,
            (false, true) => Cursors.SizeNS,
            _ => Cursors.Arrow,
        };
}
