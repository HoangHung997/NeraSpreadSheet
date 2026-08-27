using System.Globalization;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public static class SpreadsheetAnalyticsDisplayListComposer
{
    private const double TitleHeight = 28d;
    private const double AxisLabelWidth = 44d;
    private const double CategoryLabelHeight = 24d;
    private const double PlotPadding = 8d;
    private const double PivotRowHeight = 24d;

    private static readonly ColorRgba[] SeriesPalette =
    [
        new ColorRgba(33, 115, 70),
        new ColorRgba(68, 114, 196),
        new ColorRgba(237, 125, 49),
        new ColorRgba(165, 165, 165),
        new ColorRgba(112, 48, 160),
        new ColorRgba(91, 155, 213),
        new ColorRgba(112, 173, 71),
        new ColorRgba(255, 192, 0),
    ];

    private static readonly TextStyle TitleStyle = new(
        "Segoe UI",
        14d,
        600,
        ColorRgba.Black);

    private static readonly TextStyle LabelStyle = new(
        "Segoe UI",
        11d,
        400,
        ColorRgba.Black);

    private static readonly TextStyle HeaderStyle = new(
        "Segoe UI",
        11d,
        600,
        ColorRgba.Black);

    public static DisplayList ComposeChart(
        SpreadsheetChartProjection projection,
        RectD bounds)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (bounds.IsEmpty)
        {
            return new DisplayListBuilder().Build();
        }
        if (projection.ChartType == SpreadsheetChartType.Pie)
        {
            throw new NotSupportedException(
                "Pie chart display-list rendering requires a filled-path primitive. " +
                "The chart model and projection are supported, but phase Q003A " +
                "does not approximate sectors with host-specific drawing.");
        }

        var builder = new DisplayListBuilder();
        builder.PushClip(bounds);
        builder.FillRectangle(bounds, ColorRgba.White);

        var contentTop = bounds.Top + PlotPadding;
        if (!string.IsNullOrEmpty(projection.Title))
        {
            builder.DrawText(
                projection.Title,
                new RectD(
                    bounds.Left + PlotPadding,
                    bounds.Top + PlotPadding,
                    Math.Max(0d, bounds.Width - (PlotPadding * 2d)),
                    Math.Min(TitleHeight, bounds.Height)),
                TitleStyle);
            contentTop += TitleHeight;
        }

        var plot = CreatePlotBounds(bounds, contentTop);
        if (plot.IsEmpty || projection.Series.Count == 0)
        {
            DrawEmptyState(builder, bounds, contentTop, "No chart data");
            builder.PopClip();
            return builder.Build();
        }

        var categoryCount = projection.Series
            .Max(static series => series.Points.Count);
        if (categoryCount == 0)
        {
            DrawEmptyState(builder, bounds, contentTop, "No chart data");
            builder.PopClip();
            return builder.Build();
        }

        var numericValues = projection.Series
            .SelectMany(static series => series.Points)
            .Where(static point => point.Value.HasValue)
            .Select(static point => point.Value!.Value)
            .Where(double.IsFinite)
            .ToArray();
        if (numericValues.Length == 0)
        {
            DrawEmptyState(builder, bounds, contentTop, "No numeric values");
            builder.PopClip();
            return builder.Build();
        }

        var minimum = Math.Min(0d, numericValues.Min());
        var maximum = Math.Max(0d, numericValues.Max());
        if (minimum == maximum)
        {
            minimum -= 1d;
            maximum += 1d;
        }

        DrawChartFrame(builder, plot, minimum, maximum);
        switch (projection.ChartType)
        {
            case SpreadsheetChartType.Column:
                DrawColumnChart(
                    builder,
                    projection,
                    plot,
                    categoryCount,
                    minimum,
                    maximum);
                break;
            case SpreadsheetChartType.Bar:
                DrawBarChart(
                    builder,
                    projection,
                    plot,
                    categoryCount,
                    minimum,
                    maximum);
                break;
            case SpreadsheetChartType.Line:
                DrawLineChart(
                    builder,
                    projection,
                    plot,
                    categoryCount,
                    minimum,
                    maximum);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(projection),
                    projection.ChartType,
                    "Unsupported chart type.");
        }

        DrawCategoryLabels(builder, projection, plot, categoryCount);
        builder.PopClip();
        return builder.Build();
    }

    public static DisplayList ComposePivot(
        SpreadsheetPivotProjection projection,
        RectD bounds)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (bounds.IsEmpty)
        {
            return new DisplayListBuilder().Build();
        }

        var builder = new DisplayListBuilder();
        builder.PushClip(bounds);
        builder.FillRectangle(bounds, ColorRgba.White);

        var headerHeight = Math.Min(PivotRowHeight, bounds.Height);
        var labelWidth = Math.Max(0d, bounds.Width * 0.58d);
        var valueWidth = Math.Max(0d, bounds.Width - labelWidth);
        var headerFill = new ColorRgba(242, 242, 242);
        var headerBounds = new RectD(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            headerHeight);
        builder.FillRectangle(headerBounds, headerFill);
        builder.DrawText(
            projection.RowFieldName,
            new RectD(
                bounds.Left + 4d,
                bounds.Top + 3d,
                Math.Max(0d, labelWidth - 8d),
                Math.Max(0d, headerHeight - 6d)),
            HeaderStyle);
        builder.DrawText(
            $"{projection.Aggregation} of {projection.ValueFieldName}",
            new RectD(
                bounds.Left + labelWidth + 4d,
                bounds.Top + 3d,
                Math.Max(0d, valueWidth - 8d),
                Math.Max(0d, headerHeight - 6d)),
            HeaderStyle);

        var y = bounds.Top + headerHeight;
        var renderedRows = 0;
        foreach (var row in projection.Rows)
        {
            if (y >= bounds.Bottom)
            {
                break;
            }

            var rowHeight = Math.Min(PivotRowHeight, bounds.Bottom - y);
            var rowBounds = new RectD(bounds.Left, y, bounds.Width, rowHeight);
            if ((renderedRows & 1) == 1)
            {
                builder.FillRectangle(
                    rowBounds,
                    new ColorRgba(250, 250, 250));
            }
            builder.DrawText(
                row.Label,
                new RectD(
                    bounds.Left + 4d,
                    y + 3d,
                    Math.Max(0d, labelWidth - 8d),
                    Math.Max(0d, rowHeight - 6d)),
                LabelStyle);
            builder.DrawText(
                row.Value.ToString("G15", CultureInfo.InvariantCulture),
                new RectD(
                    bounds.Left + labelWidth + 4d,
                    y + 3d,
                    Math.Max(0d, valueWidth - 8d),
                    Math.Max(0d, rowHeight - 6d)),
                LabelStyle);
            builder.DrawLine(
                new PointD(bounds.Left, y + rowHeight),
                new PointD(bounds.Right, y + rowHeight),
                1d,
                ColorRgba.GridLine);
            y += rowHeight;
            renderedRows++;
        }

        builder.DrawLine(
            new PointD(bounds.Left + labelWidth, bounds.Top),
            new PointD(bounds.Left + labelWidth, Math.Min(bounds.Bottom, y)),
            1d,
            ColorRgba.GridLine);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Top + headerHeight),
            new PointD(bounds.Right, bounds.Top + headerHeight),
            1d,
            ColorRgba.GridLine);
        builder.PopClip();
        return builder.Build();
    }

    private static RectD CreatePlotBounds(RectD bounds, double contentTop)
    {
        var left = bounds.Left + AxisLabelWidth;
        var top = Math.Max(bounds.Top, contentTop);
        var right = bounds.Right - PlotPadding;
        var bottom = bounds.Bottom - CategoryLabelHeight;
        return right <= left || bottom <= top
            ? RectD.Empty
            : new RectD(left, top, right - left, bottom - top);
    }

    private static void DrawEmptyState(
        DisplayListBuilder builder,
        RectD bounds,
        double contentTop,
        string text)
    {
        var top = Math.Min(bounds.Bottom, Math.Max(bounds.Top, contentTop));
        builder.DrawText(
            text,
            new RectD(
                bounds.Left + PlotPadding,
                top,
                Math.Max(0d, bounds.Width - (PlotPadding * 2d)),
                Math.Max(0d, bounds.Bottom - top)),
            LabelStyle);
    }

    private static void DrawChartFrame(
        DisplayListBuilder builder,
        RectD plot,
        double minimum,
        double maximum)
    {
        builder.FillRectangle(plot, new ColorRgba(252, 252, 252));
        var zeroY = MapY(0d, plot, minimum, maximum);
        builder.DrawLine(
            new PointD(plot.Left, zeroY),
            new PointD(plot.Right, zeroY),
            1d,
            new ColorRgba(128, 128, 128));
        builder.DrawLine(
            new PointD(plot.Left, plot.Top),
            new PointD(plot.Left, plot.Bottom),
            1d,
            ColorRgba.GridLine);
        builder.DrawText(
            maximum.ToString("G6", CultureInfo.InvariantCulture),
            new RectD(
                Math.Max(0d, plot.Left - AxisLabelWidth + 2d),
                plot.Top,
                Math.Max(0d, AxisLabelWidth - 6d),
                18d),
            LabelStyle);
        builder.DrawText(
            minimum.ToString("G6", CultureInfo.InvariantCulture),
            new RectD(
                Math.Max(0d, plot.Left - AxisLabelWidth + 2d),
                Math.Max(plot.Top, plot.Bottom - 18d),
                Math.Max(0d, AxisLabelWidth - 6d),
                18d),
            LabelStyle);
    }

    private static void DrawColumnChart(
        DisplayListBuilder builder,
        SpreadsheetChartProjection projection,
        RectD plot,
        int categoryCount,
        double minimum,
        double maximum)
    {
        var groupWidth = plot.Width / categoryCount;
        var seriesCount = projection.Series.Count;
        var barWidth = Math.Max(
            1d,
            (groupWidth * 0.8d) / Math.Max(1, seriesCount));
        var baselineY = MapY(0d, plot, minimum, maximum);

        for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
        {
            var series = projection.Series[seriesIndex];
            var color = GetSeriesColor(seriesIndex);
            for (var categoryIndex = 0;
                 categoryIndex < series.Points.Count;
                 categoryIndex++)
            {
                var value = series.Points[categoryIndex].Value;
                if (!value.HasValue || !double.IsFinite(value.Value))
                {
                    continue;
                }

                var valueY = MapY(value.Value, plot, minimum, maximum);
                var x = plot.Left +
                        (categoryIndex * groupWidth) +
                        (groupWidth * 0.1d) +
                        (seriesIndex * barWidth);
                var top = Math.Min(valueY, baselineY);
                var height = Math.Max(1d, Math.Abs(valueY - baselineY));
                builder.FillRectangle(
                    new RectD(
                        x,
                        top,
                        Math.Min(barWidth, Math.Max(0d, plot.Right - x)),
                        Math.Min(height, Math.Max(0d, plot.Bottom - top))),
                    color);
            }
        }
    }

    private static void DrawBarChart(
        DisplayListBuilder builder,
        SpreadsheetChartProjection projection,
        RectD plot,
        int categoryCount,
        double minimum,
        double maximum)
    {
        var groupHeight = plot.Height / categoryCount;
        var seriesCount = projection.Series.Count;
        var barHeight = Math.Max(
            1d,
            (groupHeight * 0.8d) / Math.Max(1, seriesCount));
        var baselineX = MapX(0d, plot, minimum, maximum);

        for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
        {
            var series = projection.Series[seriesIndex];
            var color = GetSeriesColor(seriesIndex);
            for (var categoryIndex = 0;
                 categoryIndex < series.Points.Count;
                 categoryIndex++)
            {
                var value = series.Points[categoryIndex].Value;
                if (!value.HasValue || !double.IsFinite(value.Value))
                {
                    continue;
                }

                var valueX = MapX(value.Value, plot, minimum, maximum);
                var left = Math.Min(valueX, baselineX);
                var width = Math.Max(1d, Math.Abs(valueX - baselineX));
                var y = plot.Top +
                        (categoryIndex * groupHeight) +
                        (groupHeight * 0.1d) +
                        (seriesIndex * barHeight);
                builder.FillRectangle(
                    new RectD(
                        left,
                        y,
                        Math.Min(width, Math.Max(0d, plot.Right - left)),
                        Math.Min(barHeight, Math.Max(0d, plot.Bottom - y))),
                    color);
            }
        }
    }

    private static void DrawLineChart(
        DisplayListBuilder builder,
        SpreadsheetChartProjection projection,
        RectD plot,
        int categoryCount,
        double minimum,
        double maximum)
    {
        for (var seriesIndex = 0;
             seriesIndex < projection.Series.Count;
             seriesIndex++)
        {
            var series = projection.Series[seriesIndex];
            var color = GetSeriesColor(seriesIndex);
            PointD? previous = null;
            for (var categoryIndex = 0;
                 categoryIndex < series.Points.Count;
                 categoryIndex++)
            {
                var value = series.Points[categoryIndex].Value;
                if (!value.HasValue || !double.IsFinite(value.Value))
                {
                    previous = null;
                    continue;
                }

                var x = categoryCount <= 1
                    ? plot.Left + (plot.Width / 2d)
                    : plot.Left +
                      ((plot.Width * categoryIndex) /
                       (categoryCount - 1d));
                var point = new PointD(
                    x,
                    MapY(value.Value, plot, minimum, maximum));
                if (previous.HasValue)
                {
                    builder.DrawLine(previous.Value, point, 2d, color);
                }
                builder.FillRectangle(
                    new RectD(
                        Math.Max(plot.Left, point.X - 2d),
                        Math.Max(plot.Top, point.Y - 2d),
                        4d,
                        4d).Intersect(plot),
                    color);
                previous = point;
            }
        }
    }

    private static void DrawCategoryLabels(
        DisplayListBuilder builder,
        SpreadsheetChartProjection projection,
        RectD plot,
        int categoryCount)
    {
        if (projection.Series.Count == 0)
        {
            return;
        }
        var firstSeries = projection.Series[0];
        if (firstSeries.Points.Count == 0)
        {
            return;
        }

        var labelWidth = plot.Width / categoryCount;
        for (var index = 0;
             index < Math.Min(categoryCount, firstSeries.Points.Count);
             index++)
        {
            builder.DrawText(
                firstSeries.Points[index].Category,
                new RectD(
                    plot.Left + (index * labelWidth),
                    plot.Bottom + 2d,
                    labelWidth,
                    Math.Max(0d, CategoryLabelHeight - 2d)),
                LabelStyle);
        }
    }

    private static double MapY(
        double value,
        RectD plot,
        double minimum,
        double maximum)
    {
        var ratio = (value - minimum) / (maximum - minimum);
        return plot.Bottom - (Math.Clamp(ratio, 0d, 1d) * plot.Height);
    }

    private static double MapX(
        double value,
        RectD plot,
        double minimum,
        double maximum)
    {
        var ratio = (value - minimum) / (maximum - minimum);
        return plot.Left + (Math.Clamp(ratio, 0d, 1d) * plot.Width);
    }

    private static ColorRgba GetSeriesColor(int index) =>
        SeriesPalette[index % SeriesPalette.Length];
}
