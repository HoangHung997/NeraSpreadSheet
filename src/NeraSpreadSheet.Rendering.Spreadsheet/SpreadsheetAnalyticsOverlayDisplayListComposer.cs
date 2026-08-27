using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

/// <summary>
/// Composes chart/pivot content in document-space placements and projects it into the current
/// body viewport. Rendering stays inside the shared display-list pipeline so all hosts consume
/// identical analytics visuals.
/// </summary>
public static class SpreadsheetAnalyticsOverlayDisplayListComposer
{
    public const double SelectionStrokeWidth = 2d;
    public const double SelectionHandleSize = 8d;

    private static readonly ColorRgba SelectionColor =
        new(33, 115, 70);

    public static DisplayList Compose(
        Worksheet worksheet,
        IReadOnlyList<SpreadsheetChartDefinition> charts,
        IReadOnlyList<SpreadsheetPivotDefinition> pivots,
        IReadOnlyList<SpreadsheetAnalyticsPlacement> placements,
        ViewportLayout layout,
        SpreadsheetAnalyticsItemKey? selectedItem = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(pivots);
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(layout);

        var chartMap = charts.ToDictionary(static chart => chart.Id);
        var pivotMap = pivots.ToDictionary(static pivot => pivot.Id);
        var builder = new DisplayListBuilder();

        foreach (var placement in placements
                     .OrderBy(static value => value.ZIndex)
                     .ThenBy(static value => value.Item.Kind)
                     .ThenBy(static value => value.Item.Id))
        {
            var fragments = SpreadsheetAnalyticsViewportMapper.Map(
                placement,
                layout);
            if (fragments.Count == 0)
            {
                continue;
            }

            var content = ComposeItem(
                worksheet,
                chartMap,
                pivotMap,
                placement);
            if (content is null)
            {
                continue;
            }

            var isSelected = selectedItem.HasValue &&
                             selectedItem.Value == placement.Item;
            foreach (var fragment in fragments)
            {
                builder.PushClip(fragment.ClipBounds);
                builder.PushTranslation(
                    fragment.TranslationX,
                    fragment.TranslationY);
                builder.DrawDisplayList(content);
                if (isSelected)
                {
                    DrawSelectionFrame(
                        builder,
                        placement.DocumentBounds.Width,
                        placement.DocumentBounds.Height);
                }
                builder.PopTranslation();
                builder.PopClip();
            }
        }

        return builder.Build();
    }

    private static DisplayList? ComposeItem(
        Worksheet worksheet,
        IReadOnlyDictionary<Guid, SpreadsheetChartDefinition> charts,
        IReadOnlyDictionary<Guid, SpreadsheetPivotDefinition> pivots,
        SpreadsheetAnalyticsPlacement placement)
    {
        var bounds = new RectD(
            0d,
            0d,
            placement.DocumentBounds.Width,
            placement.DocumentBounds.Height);
        return placement.Item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart =>
                charts.TryGetValue(placement.Item.Id, out var chart)
                    ? SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
                        SpreadsheetChartProjector.Project(worksheet, chart),
                        bounds)
                    : null,
            SpreadsheetAnalyticsItemKind.Pivot =>
                pivots.TryGetValue(placement.Item.Id, out var pivot)
                    ? SpreadsheetAnalyticsDisplayListComposer.ComposePivot(
                        SpreadsheetPivotProjector.Project(worksheet, pivot),
                        bounds)
                    : null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(placement),
                placement.Item.Kind,
                "Unknown analytics item kind."),
        };
    }

    private static void DrawSelectionFrame(
        DisplayListBuilder builder,
        double width,
        double height)
    {
        var topLeft = new PointD(0d, 0d);
        var topRight = new PointD(width, 0d);
        var bottomLeft = new PointD(0d, height);
        var bottomRight = new PointD(width, height);
        builder.DrawLine(
            topLeft,
            topRight,
            SelectionStrokeWidth,
            SelectionColor);
        builder.DrawLine(
            topRight,
            bottomRight,
            SelectionStrokeWidth,
            SelectionColor);
        builder.DrawLine(
            bottomRight,
            bottomLeft,
            SelectionStrokeWidth,
            SelectionColor);
        builder.DrawLine(
            bottomLeft,
            topLeft,
            SelectionStrokeWidth,
            SelectionColor);

        var half = SelectionHandleSize / 2d;
        var centers = new[]
        {
            topLeft,
            new PointD(width / 2d, 0d),
            topRight,
            new PointD(width, height / 2d),
            bottomRight,
            new PointD(width / 2d, height),
            bottomLeft,
            new PointD(0d, height / 2d),
        };
        foreach (var center in centers)
        {
            builder.FillRectangle(
                new RectD(
                    center.X - half,
                    center.Y - half,
                    SelectionHandleSize,
                    SelectionHandleSize),
                SelectionColor);
        }
    }
}
