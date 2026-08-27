using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetAnalyticsViewportFragment(
    SpreadsheetAnalyticsPlacement Placement,
    RectD DocumentFragment,
    RectD VisibleBounds,
    RectD ClipBounds,
    double TranslationX,
    double TranslationY,
    bool IsFrozenX,
    bool IsFrozenY);

/// <summary>
/// Maps document-space floating analytics placements into body-viewport fragments.
/// A placement can yield up to four fragments when it crosses frozen row/column boundaries.
/// </summary>
public static class SpreadsheetAnalyticsViewportMapper
{
    public static IReadOnlyList<SpreadsheetAnalyticsViewportFragment> Map(
        SpreadsheetAnalyticsPlacement placement,
        ViewportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.ViewportSize.Width <= 0d ||
            layout.ViewportSize.Height <= 0d)
        {
            return [];
        }

        var horizontal = BuildAxisRegions(
            layout.ViewportSize.Width,
            layout.FrozenWidth,
            layout.ScrollX);
        var vertical = BuildAxisRegions(
            layout.ViewportSize.Height,
            layout.FrozenHeight,
            layout.ScrollY);
        var result = new List<SpreadsheetAnalyticsViewportFragment>(4);

        foreach (var xRegion in horizontal)
        {
            foreach (var yRegion in vertical)
            {
                var documentWindow = new RectD(
                    xRegion.DocumentStart,
                    yRegion.DocumentStart,
                    xRegion.DocumentExtent,
                    yRegion.DocumentExtent);
                var documentFragment =
                    placement.DocumentBounds.Intersect(documentWindow);
                if (documentFragment.IsEmpty)
                {
                    continue;
                }

                var clipBounds = new RectD(
                    xRegion.ViewportStart,
                    yRegion.ViewportStart,
                    xRegion.ViewportExtent,
                    yRegion.ViewportExtent);
                var visibleBounds = documentFragment.Translate(
                    xRegion.TranslationOffset,
                    yRegion.TranslationOffset);
                result.Add(new SpreadsheetAnalyticsViewportFragment(
                    placement,
                    documentFragment,
                    visibleBounds,
                    clipBounds,
                    placement.DocumentBounds.X + xRegion.TranslationOffset,
                    placement.DocumentBounds.Y + yRegion.TranslationOffset,
                    xRegion.IsFrozen,
                    yRegion.IsFrozen));
            }
        }

        return result;
    }

    public static IReadOnlyList<SpreadsheetAnalyticsViewportFragment> Map(
        IEnumerable<SpreadsheetAnalyticsPlacement> placements,
        ViewportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(layout);

        var result = new List<SpreadsheetAnalyticsViewportFragment>();
        foreach (var placement in placements
                     .OrderBy(static value => value.ZIndex)
                     .ThenBy(static value => value.Item.Kind)
                     .ThenBy(static value => value.Item.Id))
        {
            result.AddRange(Map(placement, layout));
        }
        return result;
    }

    private static IReadOnlyList<AxisRegion> BuildAxisRegions(
        double viewportExtent,
        double frozenExtent,
        double scrollOffset)
    {
        var result = new List<AxisRegion>(2);
        var resolvedFrozen = Math.Clamp(frozenExtent, 0d, viewportExtent);
        if (resolvedFrozen > 0d)
        {
            result.Add(new AxisRegion(
                0d,
                resolvedFrozen,
                0d,
                resolvedFrozen,
                0d,
                IsFrozen: true));
        }

        var scrollingExtent = Math.Max(0d, viewportExtent - resolvedFrozen);
        if (scrollingExtent > 0d)
        {
            var documentStart = resolvedFrozen + scrollOffset;
            result.Add(new AxisRegion(
                documentStart,
                scrollingExtent,
                resolvedFrozen,
                scrollingExtent,
                resolvedFrozen - documentStart,
                IsFrozen: false));
        }

        return result;
    }

    private readonly record struct AxisRegion(
        double DocumentStart,
        double DocumentExtent,
        double ViewportStart,
        double ViewportExtent,
        double TranslationOffset,
        bool IsFrozen);
}
