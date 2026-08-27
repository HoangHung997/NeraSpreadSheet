using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Interaction;

public static class SpreadsheetAnalyticsInteractionProjection
{
    public static IReadOnlyList<SpreadsheetAnalyticsPlacement> ApplyPreview(
        IReadOnlyList<SpreadsheetAnalyticsPlacement> placements,
        SpreadsheetAnalyticsInteractionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsTransforming ||
            !snapshot.SelectedItem.HasValue ||
            !snapshot.PreviewDocumentBounds.HasValue)
        {
            return placements;
        }

        var item = snapshot.SelectedItem.Value;
        var bounds = snapshot.PreviewDocumentBounds.Value;
        var result = new SpreadsheetAnalyticsPlacement[placements.Count];
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            result[index] = placement.Item == item
                ? placement.WithBounds(bounds)
                : placement;
        }
        return result;
    }
}
