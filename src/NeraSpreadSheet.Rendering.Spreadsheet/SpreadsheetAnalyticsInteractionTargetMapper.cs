using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

/// <summary>
/// Projects document-space analytics placements into interaction targets using the same
/// freeze-aware viewport fragments as rendering. Hit-testing therefore stays aligned with
/// the pixels users see on every host.
/// </summary>
public static class SpreadsheetAnalyticsInteractionTargetMapper
{
    public static IReadOnlyList<SpreadsheetAnalyticsInteractionTarget> Map(
        IEnumerable<SpreadsheetAnalyticsPlacement> placements,
        ViewportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(layout);

        var result = new List<SpreadsheetAnalyticsInteractionTarget>();
        foreach (var placement in placements
                     .OrderBy(static value => value.ZIndex)
                     .ThenBy(static value => value.Item.Kind)
                     .ThenBy(static value => value.Item.Id))
        {
            foreach (var fragment in SpreadsheetAnalyticsViewportMapper.Map(
                         placement,
                         layout))
            {
                var fullViewportBounds = new RectD(
                    fragment.TranslationX,
                    fragment.TranslationY,
                    placement.DocumentBounds.Width,
                    placement.DocumentBounds.Height);
                result.Add(new SpreadsheetAnalyticsInteractionTarget(
                    placement.Item,
                    placement.DocumentBounds,
                    fullViewportBounds,
                    fragment.ClipBounds,
                    placement.ZIndex));
            }
        }

        return result;
    }
}
