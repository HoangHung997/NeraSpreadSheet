using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport;

public readonly record struct SpreadsheetSplitAnalyticsInteractionTarget(
    SpreadsheetPaneId PaneId,
    SpreadsheetAnalyticsInteractionTarget Target);

public readonly record struct SpreadsheetSplitAnalyticsHitTestResult(
    SpreadsheetPaneId PaneId,
    SpreadsheetAnalyticsHitTestResult Hit);

public static class SpreadsheetSplitAnalyticsInteractionTargetMapper
{
    public static IReadOnlyList<SpreadsheetSplitAnalyticsInteractionTarget> Map(
        SpreadsheetSession session,
        SpreadsheetSplitViewportFrame frame)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);

        var placements = SpreadsheetAnalyticsInteractionProjection.ApplyPreview(
            session.AnalyticsPlacements.GetPlacements(session.ActiveWorksheet),
            session.AnalyticsInteraction.Snapshot);
        if (placements.Count == 0)
        {
            return [];
        }

        var result = new List<SpreadsheetSplitAnalyticsInteractionTarget>();
        foreach (var pane in frame.Panes)
        {
            foreach (var localTarget in SpreadsheetAnalyticsInteractionTargetMapper.Map(
                         placements,
                         pane.ViewportFrame.Layout))
            {
                var translated = new SpreadsheetAnalyticsInteractionTarget(
                    localTarget.Item,
                    localTarget.DocumentBounds,
                    localTarget.ViewportBounds.Translate(
                        pane.Pane.Bounds.X,
                        pane.Pane.Bounds.Y),
                    localTarget.ClipBounds.Translate(
                        pane.Pane.Bounds.X,
                        pane.Pane.Bounds.Y),
                    localTarget.ZIndex);
                result.Add(new SpreadsheetSplitAnalyticsInteractionTarget(
                    pane.Pane.PaneId,
                    translated));
            }
        }

        return result;
    }

    public static SpreadsheetSplitAnalyticsHitTestResult? HitTest(
        SpreadsheetSession session,
        SpreadsheetSplitViewportFrame frame,
        PointD viewportPoint,
        double handleHitSize = SpreadsheetAnalyticsHitTester.DefaultHandleHitSize)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        var region = frame.Layout.HitTest(viewportPoint);
        if (region is not
            {
                RegionKind: SpreadsheetSplitHitRegionKind.Pane,
                PaneId: { } paneId,
            })
        {
            return null;
        }

        var targets = Map(session, frame)
            .Where(target => target.PaneId == paneId)
            .Select(static target => target.Target)
            .ToArray();
        var hit = SpreadsheetAnalyticsHitTester.HitTest(
            targets,
            viewportPoint,
            session.AnalyticsInteraction.SelectedItem,
            handleHitSize);
        return hit.HasValue
            ? new SpreadsheetSplitAnalyticsHitTestResult(paneId, hit.Value)
            : null;
    }
}
