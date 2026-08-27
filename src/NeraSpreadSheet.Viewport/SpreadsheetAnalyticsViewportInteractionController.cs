using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport;

/// <summary>
/// Host-neutral bridge between viewport coordinates and the analytics interaction/editing
/// state. Hosts feed body-local pointer coordinates and normalized keyboard events here instead
/// of reimplementing chart/pivot hit-testing, transform history, or accessibility ordering.
/// </summary>
public sealed class SpreadsheetAnalyticsViewportInteractionController
{
    public const double DefaultKeyboardNudge =
        SpreadsheetAnalyticsKeyboardMapper.DefaultNudgeStep;
    public const double LargeKeyboardNudge =
        SpreadsheetAnalyticsKeyboardMapper.AcceleratedNudgeStep;

    private readonly SpreadsheetViewportEngine _viewport;
    private readonly SpreadsheetSession _session;

    public SpreadsheetAnalyticsViewportInteractionController(
        SpreadsheetViewportEngine viewport)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _session = viewport.Session;
    }

    public bool IsTransforming => _session.AnalyticsInteraction.IsTransforming;

    public SpreadsheetAnalyticsInteractionSnapshot Snapshot =>
        _session.AnalyticsInteraction.Snapshot;

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

        try
        {
            return !commit.HasChanges ||
                   SpreadsheetAnalyticsInteractionEditing.ApplyTransformCommit(
                       _session.AnalyticsPlacements,
                       commit);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool Cancel() => _session.AnalyticsInteraction.CancelTransform();

    public bool Keyboard(
        SpreadsheetAnalyticsKeyboardKey key,
        SpreadsheetAnalyticsKeyboardModifiers modifiers =
            SpreadsheetAnalyticsKeyboardModifiers.None,
        double minimumWidth =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight) =>
        Keyboard(
            SpreadsheetAnalyticsKeyboardMapper.Map(key, modifiers),
            minimumWidth,
            minimumHeight);

    public bool Keyboard(
        SpreadsheetAnalyticsKeyboardIntent intent,
        double minimumWidth =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight =
            SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight) =>
        SpreadsheetAnalyticsKeyboardEditing.Execute(
            intent,
            _session.AnalyticsInteraction,
            _session.AnalyticsPlacements,
            _session.Analytics,
            minimumWidth,
            minimumHeight);

    public IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> GetAccessibilityNodes(
        ViewportLayout layout,
        Func<SpreadsheetAnalyticsItemKey, string?>? nameResolver = null) =>
        SpreadsheetAnalyticsAccessibilityProjector.Project(
            _viewport.GetAnalyticsInteractionTargets(layout),
            _session.AnalyticsInteraction.SelectedItem,
            nameResolver);

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
