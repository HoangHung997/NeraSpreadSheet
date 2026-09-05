using System.Windows.Documents;
using System.Windows.Input;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private ScrollBarDragState? _scrollBarDrag;

    private bool TryBeginScrollBarInteraction(double controlX, double controlY)
    {
        if (!TryGetScrollBarHit(
            controlX,
            controlY,
            out var bar,
            out var part,
            out var bodyPoint))
        {
            return false;
        }

        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();
        SetActivePaneCore(bar.PaneId);
        switch (part)
        {
            case SpreadsheetScrollBarPart.Thumb:
            {
                var coordinate = GetAxisCoordinate(bar.Orientation, bodyPoint);
                var thumbStart = GetAxisStart(bar.Orientation, bar.ThumbBounds);
                _scrollBarDrag = new ScrollBarDragState(
                    bar,
                    coordinate - thumbStart);
                CaptureMouse();
                Cursor = Cursors.Hand;
                break;
            }
            case SpreadsheetScrollBarPart.DecreaseButton:
                SetScrollBarOffset(
                    bar,
                    bar.GetLineOffset(
                        increase: false,
                        _owner.RenderTheme.ScrollBarLineStep),
                    persist: true);
                break;
            case SpreadsheetScrollBarPart.IncreaseButton:
                SetScrollBarOffset(
                    bar,
                    bar.GetLineOffset(
                        increase: true,
                        _owner.RenderTheme.ScrollBarLineStep),
                    persist: true);
                break;
            case SpreadsheetScrollBarPart.TrackBeforeThumb:
                SetScrollBarOffset(
                    bar,
                    bar.GetPageOffset(
                        increase: false,
                        _owner.RenderTheme.ScrollBarPageFactor),
                    persist: true);
                break;
            case SpreadsheetScrollBarPart.TrackAfterThumb:
                SetScrollBarOffset(
                    bar,
                    bar.GetPageOffset(
                        increase: true,
                        _owner.RenderTheme.ScrollBarPageFactor),
                    persist: true);
                break;
            default:
                return false;
        }

        return true;
    }

    private bool TryGetScrollBarHit(
        double controlX,
        double controlY,
        out SpreadsheetPaneScrollBarLayout bar,
        out SpreadsheetScrollBarPart part,
        out PointD bodyPoint)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            bar = default;
            part = SpreadsheetScrollBarPart.None;
            bodyPoint = default;
            return false;
        }

        var chrome = GetChromeMetrics();
        bodyPoint = new PointD(
            controlX - chrome.RowHeaderWidth,
            controlY - chrome.ColumnHeaderHeight);
        if (bodyPoint.X < 0d ||
            bodyPoint.Y < 0d ||
            !frame.ScrollBars.TryHitTest(bodyPoint, out var hit) ||
            !frame.ScrollBars.TryGetBar(
                hit.PaneId,
                hit.Orientation,
                out bar))
        {
            bar = default;
            part = SpreadsheetScrollBarPart.None;
            return false;
        }

        part = hit.Part;
        return true;
    }

    private void UpdateScrollBarDrag(double controlX, double controlY)
    {
        if (_scrollBarDrag is not { } drag)
        {
            return;
        }

        var chrome = GetChromeMetrics();
        var bodyPoint = new PointD(
            controlX - chrome.RowHeaderWidth,
            controlY - chrome.ColumnHeaderHeight);
        var coordinate = GetAxisCoordinate(
            drag.Bar.Orientation,
            bodyPoint);
        SetScrollBarOffset(
            drag.Bar,
            drag.Bar.GetOffsetForThumbStart(
                coordinate - drag.GrabOffset),
            persist: false);
    }

    private void EndScrollBarDrag(bool persist)
    {
        if (_scrollBarDrag is null)
        {
            return;
        }

        _scrollBarDrag = null;
        if (persist)
        {
            PersistCurrentSplitState(
                SpreadsheetSplitViewChangeKind.PaneScroll);
        }
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void SetScrollBarOffset(
        SpreadsheetPaneScrollBarLayout bar,
        double offset,
        bool persist)
    {
        var engine = GetEngine();
        var current = engine.GetPaneScroll(bar.PaneId);
        if (bar.Orientation == SpreadsheetScrollBarOrientation.Horizontal)
        {
            engine.ScrollPaneTo(
                bar.PaneId,
                offset,
                current.Y,
                animated: false);
        }
        else
        {
            engine.ScrollPaneTo(
                bar.PaneId,
                current.X,
                offset,
                animated: false);
        }

        _lastFrame = null;
        UpdateEditorBounds();
        InvalidateVisual();
        if (persist)
        {
            PersistCurrentSplitState(
                SpreadsheetSplitViewChangeKind.PaneScroll);
        }
        PaneScrollChanged?.Invoke(
            this,
            new SpreadsheetPaneScrollChangedEventArgs(
                bar.PaneId,
                engine.GetPaneScrollSnapshot(bar.PaneId)));
    }

    private static double GetAxisCoordinate(
        SpreadsheetScrollBarOrientation orientation,
        PointD point) =>
        orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? point.X
            : point.Y;

    private static double GetAxisStart(
        SpreadsheetScrollBarOrientation orientation,
        RectD bounds) =>
        orientation == SpreadsheetScrollBarOrientation.Horizontal
            ? bounds.Left
            : bounds.Top;

    private readonly record struct ScrollBarDragState(
        SpreadsheetPaneScrollBarLayout Bar,
        double GrabOffset);
}
