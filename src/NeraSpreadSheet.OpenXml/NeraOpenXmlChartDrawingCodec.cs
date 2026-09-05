using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace NeraSpreadSheet.OpenXml;

internal static class NeraOpenXmlChartDrawingCodec
{
    private const string ManagedDescriptionPrefix = "NeraSpreadSheet:Chart:";
    private const string ChartGraphicDataUri =
        "http://schemas.openxmlformats.org/drawingml/2006/chart";
    private const double EmusPerPixel = 9_525d;
    private const uint CategoryAxisId = 48_650_112U;
    private const uint ValueAxisId = 48_672_768U;

    internal static void Export(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The XLSX package does not contain a workbook part.");
        var sheets = workbookPart.Workbook?.GetFirstChild<S.Sheets>()?
            .Elements<S.Sheet>()
            .ToArray()
            ?? throw new InvalidDataException(
                "The XLSX workbook does not contain a sheets collection.");
        var count = Math.Min(sheets.Length, session.Workbook.Worksheets.Count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            ExportWorksheet(
                worksheetPart,
                session,
                session.Workbook.Worksheets[index],
                cancellationToken);
        }
    }

    private static void ExportWorksheet(
        WorksheetPart worksheetPart,
        SpreadsheetSession session,
        Worksheet worksheet,
        CancellationToken cancellationToken)
    {
        var worksheetMarkup = worksheetPart.Worksheet
            ?? throw new InvalidDataException(
                "The XLSX worksheet part does not contain worksheet markup.");
        var drawingsPart = worksheetPart.DrawingsPart;
        if (drawingsPart is not null)
        {
            RemoveManagedCharts(drawingsPart);
        }

        var charts = session.Analytics.GetCharts(worksheet);
        if (charts.Count == 0)
        {
            RemoveEmptyManagedDrawingPart(worksheetPart, drawingsPart);
            return;
        }

        drawingsPart ??= EnsureDrawingsPart(worksheetPart);
        drawingsPart.WorksheetDrawing ??= new Xdr.WorksheetDrawing();
        var worksheetDrawing = drawingsPart.WorksheetDrawing
            ?? throw new InvalidDataException(
                "The XLSX drawings part could not initialize worksheet drawing markup.");
        var nextDrawingId = GetNextDrawingId(worksheetDrawing);
        var placements = session.AnalyticsPlacements.GetPlacements(worksheet)
            .ToDictionary(static placement => placement.Item);

        foreach (var chart in charts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
            if (!placements.TryGetValue(item, out var placement))
            {
                throw new InvalidDataException(
                    $"Chart '{chart.Name}' is missing floating placement metadata.");
            }

            var chartPart = drawingsPart.AddNewPart<ChartPart>();
            BuildChartPart(chartPart, worksheet, chart);
            var anchor = BuildAnchor(
                drawingsPart,
                chartPart,
                chart,
                placement,
                nextDrawingId++);
            worksheetDrawing.Append(anchor);
        }

        worksheetDrawing.Save();
        worksheetMarkup.Save();
    }

    private static DrawingsPart EnsureDrawingsPart(WorksheetPart worksheetPart)
    {
        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();
        var relationshipId = worksheetPart.GetIdOfPart(drawingsPart);
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException(
                "The XLSX worksheet part does not contain worksheet markup.");
        foreach (var drawing in worksheet.Elements<S.Drawing>().ToArray())
        {
            if (string.Equals(drawing.Id?.Value, relationshipId, StringComparison.Ordinal))
            {
                drawing.Remove();
            }
        }
        worksheet.Append(new S.Drawing { Id = relationshipId });
        worksheet.Save();
        return drawingsPart;
    }

    private static void RemoveManagedCharts(DrawingsPart drawingsPart)
    {
        var worksheetDrawing = drawingsPart.WorksheetDrawing;
        if (worksheetDrawing is null)
        {
            return;
        }

        foreach (var anchor in worksheetDrawing.ChildElements
                     .OfType<OpenXmlCompositeElement>()
                     .ToArray())
        {
            var nonVisual = anchor
                .Descendants<Xdr.NonVisualDrawingProperties>()
                .FirstOrDefault();
            if (nonVisual?.Description?.Value is not { } description ||
                !description.StartsWith(
                    ManagedDescriptionPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var relationshipId = anchor
                .Descendants<C.ChartReference>()
                .Select(static reference => reference.Id?.Value)
                .FirstOrDefault(static id => !string.IsNullOrWhiteSpace(id));
            anchor.Remove();
            if (relationshipId is null)
            {
                continue;
            }

            var chartPart = drawingsPart.Parts
                .Where(part => string.Equals(
                    part.RelationshipId,
                    relationshipId,
                    StringComparison.Ordinal))
                .Select(static part => part.OpenXmlPart)
                .OfType<ChartPart>()
                .FirstOrDefault();
            if (chartPart is not null)
            {
                drawingsPart.DeletePart(chartPart);
            }
        }
        worksheetDrawing.Save();
    }

    private static void RemoveEmptyManagedDrawingPart(
        WorksheetPart worksheetPart,
        DrawingsPart? drawingsPart)
    {
        if (drawingsPart?.WorksheetDrawing is not { } worksheetDrawing ||
            worksheetDrawing.ChildElements.Count != 0)
        {
            return;
        }

        var worksheetMarkup = worksheetPart.Worksheet
            ?? throw new InvalidDataException(
                "The XLSX worksheet part does not contain worksheet markup.");
        var relationshipId = worksheetPart.GetIdOfPart(drawingsPart);
        foreach (var drawing in worksheetMarkup
                     .Elements<S.Drawing>()
                     .Where(drawing => string.Equals(
                         drawing.Id?.Value,
                         relationshipId,
                         StringComparison.Ordinal))
                     .ToArray())
        {
            drawing.Remove();
        }
        worksheetPart.DeletePart(drawingsPart);
        worksheetMarkup.Save();
    }

    private static uint GetNextDrawingId(Xdr.WorksheetDrawing worksheetDrawing)
    {
        var maximum = worksheetDrawing
            .Descendants<Xdr.NonVisualDrawingProperties>()
            .Select(static properties => properties.Id?.Value ?? 0U)
            .DefaultIfEmpty(0U)
            .Max();
        return checked(maximum + 1U);
    }

    private static Xdr.AbsoluteAnchor BuildAnchor(
        DrawingsPart drawingsPart,
        ChartPart chartPart,
        SpreadsheetChartDefinition chart,
        SpreadsheetAnalyticsPlacement placement,
        uint drawingId)
    {
        var chartRelationshipId = drawingsPart.GetIdOfPart(chartPart);
        var bounds = placement.DocumentBounds;
        var graphicFrame = new Xdr.GraphicFrame
        {
            Macro = string.Empty,
        };
        graphicFrame.Append(
            new Xdr.NonVisualGraphicFrameProperties(
                new Xdr.NonVisualDrawingProperties
                {
                    Id = drawingId,
                    Name = chart.Name,
                    Description = ManagedDescriptionPrefix + chart.Id.ToString("N"),
                },
                new Xdr.NonVisualGraphicFrameDrawingProperties()),
            new Xdr.Transform(
                new A.Offset { X = 0L, Y = 0L },
                new A.Extents { Cx = 0L, Cy = 0L }),
            new A.Graphic(
                new A.GraphicData(
                    new C.ChartReference { Id = chartRelationshipId })
                {
                    Uri = ChartGraphicDataUri,
                }));

        return new Xdr.AbsoluteAnchor(
            new Xdr.Position
            {
                X = ToEmu(bounds.X),
                Y = ToEmu(bounds.Y),
            },
            new Xdr.Extent
            {
                Cx = ToPositiveEmu(bounds.Width),
                Cy = ToPositiveEmu(bounds.Height),
            },
            graphicFrame,
            new Xdr.ClientData());
    }

    private static void BuildChartPart(
        ChartPart chartPart,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        var chartSpace = new C.ChartSpace();
        chartSpace.Append(new C.EditingLanguage { Val = "en-US" });
        var chart = chartSpace.AppendChild(new C.Chart());
        if (!string.IsNullOrWhiteSpace(definition.Title))
        {
            chart.Append(CreateTitle(definition.Title));
        }

        var plotArea = chart.AppendChild(new C.PlotArea());
        plotArea.Append(new C.Layout());
        switch (definition.ChartType)
        {
            case SpreadsheetChartType.Column:
                AppendBarChart(
                    plotArea,
                    worksheet,
                    definition,
                    C.BarDirectionValues.Column);
                break;
            case SpreadsheetChartType.Bar:
                AppendBarChart(
                    plotArea,
                    worksheet,
                    definition,
                    C.BarDirectionValues.Bar);
                break;
            case SpreadsheetChartType.Line:
                AppendLineChart(plotArea, worksheet, definition);
                break;
            case SpreadsheetChartType.Pie:
                AppendPieChart(plotArea, worksheet, definition);
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported chart type '{definition.ChartType}' for OOXML export.");
        }

        chart.Append(
            new C.Legend(
                new C.LegendPosition { Val = C.LegendPositionValues.Right },
                new C.Layout()),
            new C.PlotVisibleOnly { Val = true });
        chartPart.ChartSpace = chartSpace;
        chartPart.ChartSpace.Save();
    }

    private static void AppendBarChart(
        C.PlotArea plotArea,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition,
        C.BarDirectionValues direction)
    {
        var barChart = new C.BarChart(
            new C.BarDirection { Val = direction },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
            new C.VaryColors { Val = false });
        AppendBarSeries(barChart, worksheet, definition);
        barChart.Append(
            new C.AxisId { Val = CategoryAxisId },
            new C.AxisId { Val = ValueAxisId });
        plotArea.Append(
            barChart,
            CreateCategoryAxis(
                direction == C.BarDirectionValues.Bar
                    ? C.AxisPositionValues.Left
                    : C.AxisPositionValues.Bottom),
            CreateValueAxis(
                direction == C.BarDirectionValues.Bar
                    ? C.AxisPositionValues.Bottom
                    : C.AxisPositionValues.Left));
    }

    private static void AppendLineChart(
        C.PlotArea plotArea,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        var lineChart = new C.LineChart(
            new C.Grouping { Val = C.GroupingValues.Standard },
            new C.VaryColors { Val = false });
        AppendLineSeries(lineChart, worksheet, definition);
        lineChart.Append(
            new C.AxisId { Val = CategoryAxisId },
            new C.AxisId { Val = ValueAxisId });
        plotArea.Append(
            lineChart,
            CreateCategoryAxis(C.AxisPositionValues.Bottom),
            CreateValueAxis(C.AxisPositionValues.Left));
    }

    private static void AppendPieChart(
        C.PlotArea plotArea,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        var pieChart = new C.PieChart(
            new C.VaryColors { Val = true });
        var seriesIndex = 0U;
        foreach (var columnIndex in EnumerateValueColumns(definition))
        {
            var series = new C.PieChartSeries(
                new C.Index { Val = seriesIndex },
                new C.Order { Val = seriesIndex },
                CreateSeriesText(worksheet, definition, columnIndex, seriesIndex));
            if (CreateCategoryAxisData(worksheet, definition) is { } categories)
            {
                series.Append(categories);
            }
            series.Append(CreateValues(worksheet, definition, columnIndex));
            pieChart.Append(series);
            seriesIndex++;
        }
        plotArea.Append(pieChart);
    }

    private static void AppendBarSeries(
        C.BarChart barChart,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        var seriesIndex = 0U;
        foreach (var columnIndex in EnumerateValueColumns(definition))
        {
            var series = new C.BarChartSeries(
                new C.Index { Val = seriesIndex },
                new C.Order { Val = seriesIndex },
                CreateSeriesText(worksheet, definition, columnIndex, seriesIndex));
            if (CreateCategoryAxisData(worksheet, definition) is { } categories)
            {
                series.Append(categories);
            }
            series.Append(CreateValues(worksheet, definition, columnIndex));
            barChart.Append(series);
            seriesIndex++;
        }
    }

    private static void AppendLineSeries(
        C.LineChart lineChart,
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        var seriesIndex = 0U;
        foreach (var columnIndex in EnumerateValueColumns(definition))
        {
            var series = new C.LineChartSeries(
                new C.Index { Val = seriesIndex },
                new C.Order { Val = seriesIndex },
                CreateSeriesText(worksheet, definition, columnIndex, seriesIndex));
            if (CreateCategoryAxisData(worksheet, definition) is { } categories)
            {
                series.Append(categories);
            }
            series.Append(CreateValues(worksheet, definition, columnIndex));
            lineChart.Append(series);
            seriesIndex++;
        }
    }

    private static IEnumerable<int> EnumerateValueColumns(
        SpreadsheetChartDefinition definition)
    {
        var startColumn = definition.FirstColumnContainsCategories
            ? definition.SourceRange.Left + 1
            : definition.SourceRange.Left;
        for (var column = startColumn;
             column <= definition.SourceRange.Right;
             column++)
        {
            yield return column;
        }
    }

    private static C.SeriesText CreateSeriesText(
        Worksheet worksheet,
        SpreadsheetChartDefinition definition,
        int columnIndex,
        uint seriesIndex)
    {
        if (definition.FirstRowContainsSeriesNames)
        {
            return new C.SeriesText(
                new C.StringReference(
                    new C.Formula(
                        ToCellFormula(
                            worksheet.Name,
                            definition.SourceRange.Top,
                            columnIndex))));
        }

        return new C.SeriesText(
            new C.NumericValue($"Series {seriesIndex + 1U}"));
    }

    private static C.CategoryAxisData? CreateCategoryAxisData(
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        if (!definition.FirstColumnContainsCategories)
        {
            return null;
        }

        var startRow = definition.FirstRowContainsSeriesNames
            ? definition.SourceRange.Top + 1
            : definition.SourceRange.Top;
        return new C.CategoryAxisData(
            new C.StringReference(
                new C.Formula(
                    ToRangeFormula(
                        worksheet.Name,
                        startRow,
                        definition.SourceRange.Left,
                        definition.SourceRange.Bottom,
                        definition.SourceRange.Left))));
    }

    private static C.Values CreateValues(
        Worksheet worksheet,
        SpreadsheetChartDefinition definition,
        int columnIndex)
    {
        var startRow = definition.FirstRowContainsSeriesNames
            ? definition.SourceRange.Top + 1
            : definition.SourceRange.Top;
        return new C.Values(
            new C.NumberReference(
                new C.Formula(
                    ToRangeFormula(
                        worksheet.Name,
                        startRow,
                        columnIndex,
                        definition.SourceRange.Bottom,
                        columnIndex))));
    }

    private static C.CategoryAxis CreateCategoryAxis(C.AxisPositionValues position) =>
        new(
            new C.AxisId { Val = CategoryAxisId },
            new C.Scaling(
                new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = position },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = ValueAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.AutoLabeled { Val = true },
            new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
            new C.LabelOffset { Val = (ushort)100 });

    private static C.ValueAxis CreateValueAxis(C.AxisPositionValues position) =>
        new(
            new C.AxisId { Val = ValueAxisId },
            new C.Scaling(
                new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = position },
            new C.MajorGridlines(),
            new C.NumberingFormat
            {
                FormatCode = "General",
                SourceLinked = true,
            },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = CategoryAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.CrossBetween { Val = C.CrossBetweenValues.Between });

    private static C.Title CreateTitle(string title) =>
        new(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties { Language = "en-US" },
                            new A.Text(title)),
                        new A.EndParagraphRunProperties { Language = "en-US" }))),
            new C.Layout(),
            new C.Overlay { Val = false });

    private static string ToCellFormula(
        string worksheetName,
        int rowIndex,
        int columnIndex) =>
        $"'{EscapeWorksheetName(worksheetName)}'!{ToAbsoluteCellReference(rowIndex, columnIndex)}";

    private static string ToRangeFormula(
        string worksheetName,
        int top,
        int left,
        int bottom,
        int right) =>
        $"'{EscapeWorksheetName(worksheetName)}'!" +
        $"{ToAbsoluteCellReference(top, left)}:{ToAbsoluteCellReference(bottom, right)}";

    private static string EscapeWorksheetName(string worksheetName) =>
        worksheetName.Replace("'", "''", StringComparison.Ordinal);

    private static string ToAbsoluteCellReference(int rowIndex, int columnIndex)
    {
        var columnNumber = checked(columnIndex + 1);
        Span<char> buffer = stackalloc char[8];
        var cursor = buffer.Length;
        while (columnNumber > 0)
        {
            columnNumber--;
            buffer[--cursor] = (char)('A' + (columnNumber % 26));
            columnNumber /= 26;
        }
        return $"${new string(buffer[cursor..])}${rowIndex + 1}";
    }

    private static long ToEmu(double pixels)
    {
        if (!double.IsFinite(pixels) || pixels < 0d)
        {
            throw new InvalidDataException(
                "Chart placement contains a non-finite or negative position.");
        }
        return checked((long)Math.Round(
            pixels * EmusPerPixel,
            MidpointRounding.AwayFromZero));
    }

    private static long ToPositiveEmu(double pixels)
    {
        var value = ToEmu(pixels);
        if (value <= 0L)
        {
            throw new InvalidDataException(
                "Chart placement contains a non-positive extent.");
        }
        return value;
    }
}
