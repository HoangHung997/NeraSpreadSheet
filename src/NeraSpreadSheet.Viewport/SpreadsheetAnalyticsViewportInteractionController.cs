using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport;

/// <summary>
/// Host-neutral bridge between viewport coordinates and the analytics interaction/editing
/// state. Hosts feed body-local pointer coordinates here instead of reimplementing chart/pivot
/// hit-testing and transform history semantics.
/// </summary>
public sealed class SpreadsheetAnalyticsViewportInteractionController
{
    public const double DefaultKeyboardNudge = 1d;
    public const double LargeKeyboardNudge = 10d;

    private readonly SpreadsheetViewportEngine _viewport;
    private readonly SpreadsheetSession _session;

    public SpreadsheetAnalyticsViewportInteractionController(
        SpreadsheetViewportEngine viewport)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _session = viewport.Session;
    }

    public bool IsTransforming => _session.AnalyticsInteraction.IsTransforming;

    public bool PointerPressed(
        PointD bodyPoint,
        ViewportLayout layout,
        double handleHitSize =
            SpreadsheetAnalyticsHitTester.DefaultHandleHitSize) =>
        _session.AnalyticsInteraction.TryBeginTransform(
            bodyPoint,
            _viewport.GetAnalyticsInteractionTargets(layout),
            handleHitSize);

    public bool PointerMoved(
        PointD bodyPoint,
        double minimumWidth =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight) =>
        _session.AnalyticsInteraction.UpdateTransform(
            bodyPoint,
            minimumWidth,
            minimumHeight);

    public bool PointerReleased(
        PointD bodyPoint,
        double minimumWidth =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight)
    {
        if (!_session.AnalyticsInteraction.TryCompleteTransform(
                bodyPoint,
                out var commit,
                minimumWidth,
                minimumHeight))
        {
            return false;
        }

        SpreadsheetAnalyticsInteractionEditing.ApplyTransformCommit(
            _session.AnalyticsPlacements,
            commit);
        return true;
    }

    public bool Cancel() => _session.AnalyticsInteraction.CancelTransform();

    public bool NudgeSelected(double deltaX, double deltaY)
    {
        var selected = _session.AnalyticsInteraction.SelectedItem;
        return selected.HasValue &&
               _session.AnalyticsPlacements.MoveBy(
                   selected.Value,
                   deltaX,
                   deltaY);
    }

    public bool DeleteSelected()
    {
        var selected = _session.AnalyticsInteraction.SelectedItem;
        if (!selected.HasValue ||
            !SpreadsheetAnalyticsInteractionEditing.RemoveItem(
                _session.Analytics,
                selected.Value))
        {
            return false;
        }

        _session.AnalyticsInteraction.ClearSelection();
        return true;
    }
}
