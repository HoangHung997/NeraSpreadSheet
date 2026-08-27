using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetAnalyticsInteractionEditing
{
    public static bool ApplyTransformCommit(
        SpreadsheetAnalyticsPlacementController placements,
        SpreadsheetAnalyticsTransformCommit commit)
    {
        ArgumentNullException.ThrowIfNull(placements);
        if (!commit.HasChanges)
        {
            return false;
        }
        if (!placements.TryGetPlacement(commit.Item, out var current))
        {
            return false;
        }
        if (current.DocumentBounds != commit.BeforeBounds)
        {
            throw new InvalidOperationException(
                "Analytics placement changed after the interaction began; " +
                "the stale transform commit was rejected.");
        }

        return placements.SetBounds(commit.Item, commit.AfterBounds);
    }

    public static bool RemoveItem(
        SpreadsheetAnalyticsController analytics,
        SpreadsheetAnalyticsItemKey item)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        return item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart => analytics.RemoveChart(item.Id),
            SpreadsheetAnalyticsItemKind.Pivot => analytics.RemovePivot(item.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
    }
}
