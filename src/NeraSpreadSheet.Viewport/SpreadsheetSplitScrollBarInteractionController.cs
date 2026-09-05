using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport;

public readonly record struct SpreadsheetSplitScrollRequest(
    SpreadsheetPaneId PaneId,
    SpreadsheetScrollBarAxis Axis,
    double Offset);

public readonly record struct SpreadsheetSplitScrollBarPointerResult(
    bool Handled,
    SpreadsheetSplitScrollRequest? ScrollRequest,
    bool IsDragging);

public sealed class SpreadsheetSplitScrollBarInteractionController
{
    private DragState? _drag;

    public bool IsDragging => _drag is not null;

    public SpreadsheetPaneId? DragPaneId => _drag?.ScrollBar.PaneId;

    public SpreadsheetScrollBarAxis? DragAxis => _drag?.ScrollBar.Axis;

    public SpreadsheetSplitScrollBarPointerResult BeginPointer(
        SpreadsheetSplitScrollBarLayout layout,
        PointD point)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var hit = layout.HitTest(point);
        if (!hit.IsHit)
        {
            _drag = null;
            return default;
        }

        if (hit.Kind == SpreadsheetScrollBarHitKind.Thumb)
        {
            var coordinate = GetCoordinate(hit.Axis, point);
            _drag = new DragState(
                hit.ScrollBar,
                coordinate - hit.ScrollBar.ThumbStart);
            return new SpreadsheetSplitScrollBarPointerResult(
                Handled: true,
                ScrollRequest: null,
                IsDragging: true);
        }

        _drag = null;
        var offset = SpreadsheetSplitScrollBarGeometry.GetPagedOffset(
            hit,
            layout.Style.PageFactor);
        return new SpreadsheetSplitScrollBarPointerResult(
            Handled: true,
            new SpreadsheetSplitScrollRequest(
                hit.PaneId,
                hit.Axis,
                offset),
            IsDragging: false);
    }

    public SpreadsheetSplitScrollBarPointerResult MovePointer(PointD point)
    {
        if (_drag is not { } drag ||
            !double.IsFinite(point.X) ||
            !double.IsFinite(point.Y))
        {
            return default;
        }

        var coordinate = GetCoordinate(drag.ScrollBar.Axis, point);
        var offset = SpreadsheetSplitScrollBarGeometry.GetOffsetFromThumb(
            drag.ScrollBar,
            coordinate,
            drag.GrabOffset);
        return new SpreadsheetSplitScrollBarPointerResult(
            Handled: true,
            new SpreadsheetSplitScrollRequest(
                drag.ScrollBar.PaneId,
                drag.ScrollBar.Axis,
                offset),
            IsDragging: true);
    }

    public bool EndPointer()
    {
        if (_drag is null)
        {
            return false;
        }

        _drag = null;
        return true;
    }

    public void Cancel() => _drag = null;

    private static double GetCoordinate(
        SpreadsheetScrollBarAxis axis,
        PointD point) =>
        axis == SpreadsheetScrollBarAxis.Horizontal
            ? point.X
            : point.Y;

    private readonly record struct DragState(
        SpreadsheetSplitScrollBar ScrollBar,
        double GrabOffset);
}

public static class SpreadsheetSplitScrollBarViewportExtensions
{
    public static SpreadsheetSplitScrollBarLayout CreateScrollBarLayout(
        this SpreadsheetSplitViewportFrame frame,
        SizeD contentExtent,
        SpreadsheetSplitScrollBarStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var panes = new SpreadsheetSplitPaneScrollBarState[frame.Panes.Count];
        for (var index = 0; index < frame.Panes.Count; index++)
        {
            var pane = frame.Panes[index];
            panes[index] = new SpreadsheetSplitPaneScrollBarState(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ScrollX,
                pane.ScrollY,
                contentExtent.Width,
                contentExtent.Height);
        }

        return SpreadsheetSplitScrollBarGeometry.Create(
            frame.Layout.ViewportSize,
            panes,
            style);
    }

    public static void ApplyScrollRequest(
        this SpreadsheetSplitViewportEngine engine,
        SpreadsheetSplitScrollRequest request,
        bool animated = false)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var current = engine.GetPaneScroll(request.PaneId);
        engine.ScrollPaneTo(
            request.PaneId,
            request.Axis == SpreadsheetScrollBarAxis.Horizontal
                ? request.Offset
                : current.X,
            request.Axis == SpreadsheetScrollBarAxis.Vertical
                ? request.Offset
                : current.Y,
            animated);
    }
}
