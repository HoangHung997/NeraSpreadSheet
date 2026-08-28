using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml;

internal static class NeraOpenXmlAnalyticsStateCodec
{
    internal const string ContentType = "application/vnd.neraspreadsheet.analytics-state+xml";
    internal const string NamespaceUri = "urn:neraspreadsheet:analytics-state:1";
    private const string CurrentVersion = "1";
    private static readonly XNamespace NeraNamespace = NamespaceUri;

    internal static void Export(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");

        foreach (var existing in workbookPart.CustomXmlParts
                     .Where(static part => string.Equals(
                         part.ContentType,
                         ContentType,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            workbookPart.DeletePart(existing);
        }

        var worksheetElements = new List<XElement>();
        foreach (var worksheet in session.Workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var charts = session.Analytics.GetCharts(worksheet);
            var pivots = session.Analytics.GetPivots(worksheet);
            var placements = session.AnalyticsPlacements.GetPlacements(worksheet);
            if (charts.Count == 0 && pivots.Count == 0 && placements.Count == 0)
            {
                continue;
            }

            var worksheetElement = new XElement(
                NeraNamespace + "worksheet",
                new XAttribute("name", worksheet.Name));
            foreach (var chart in charts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worksheetElement.Add(SerializeChart(chart));
            }
            foreach (var pivot in pivots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worksheetElement.Add(SerializePivot(pivot));
            }
            foreach (var placement in placements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worksheetElement.Add(SerializePlacement(placement));
            }
            worksheetElements.Add(worksheetElement);
        }

        if (worksheetElements.Count == 0)
        {
            return;
        }

        var root = new XElement(
            NeraNamespace + "analyticsState",
            new XAttribute("version", CurrentVersion),
            worksheetElements);
        var customPart = workbookPart.AddCustomXmlPart(ContentType);
        using var stream = customPart.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        });
        new XDocument(root).Save(writer);
    }

    internal static void Import(
        SpreadsheetDocument document,
        SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        var workbookPart = document.WorkbookPart;
        if (workbookPart is null)
        {
            return;
        }

        var worksheets = session.Workbook.Worksheets.ToDictionary(
            static worksheet => worksheet.Name,
            StringComparer.OrdinalIgnoreCase);
        foreach (var part in workbookPart.CustomXmlParts.Where(static part => string.Equals(
                     part.ContentType,
                     ContentType,
                     StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
            });
            var documentXml = XDocument.Load(reader, LoadOptions.None);
            var root = documentXml.Root;
            if (root?.Name != NeraNamespace + "analyticsState")
            {
                continue;
            }
            if (!string.Equals(
                    (string?)root.Attribute("version"),
                    CurrentVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The Nera analytics metadata uses an unsupported version.");
            }

            foreach (var worksheetElement in root.Elements(NeraNamespace + "worksheet"))
            {
                var worksheetName = RequiredString(
                    worksheetElement,
                    "name",
                    "A Nera analytics worksheet entry is missing its worksheet name.");
                if (!worksheets.TryGetValue(worksheetName, out var worksheet))
                {
                    continue;
                }

                RestoreDefinitions(session, worksheet, worksheetElement);
                RestorePlacements(session, worksheet, worksheetElement);
            }
        }
    }

    private static void RestoreDefinitions(
        SpreadsheetSession session,
        Worksheet worksheet,
        XElement worksheetElement)
    {
        foreach (var chartElement in worksheetElement.Elements(NeraNamespace + "chart"))
        {
            var chart = DeserializeChart(chartElement, worksheet.Name);
            try
            {
                session.Analytics.RestoreChart(worksheet, chart);
            }
            catch (ArgumentException exception)
            {
                throw InvalidMetadata(worksheet.Name, "chart", exception);
            }
            catch (InvalidOperationException exception)
            {
                throw InvalidMetadata(worksheet.Name, "chart", exception);
            }
        }

        foreach (var pivotElement in worksheetElement.Elements(NeraNamespace + "pivot"))
        {
            var pivot = DeserializePivot(pivotElement, worksheet.Name);
            try
            {
                session.Analytics.RestorePivot(worksheet, pivot);
            }
            catch (ArgumentException exception)
            {
                throw InvalidMetadata(worksheet.Name, "pivot", exception);
            }
            catch (InvalidOperationException exception)
            {
                throw InvalidMetadata(worksheet.Name, "pivot", exception);
            }
        }
    }

    private static void RestorePlacements(
        SpreadsheetSession session,
        Worksheet worksheet,
        XElement worksheetElement)
    {
        var seen = new HashSet<SpreadsheetAnalyticsItemKey>();
        foreach (var placementElement in worksheetElement.Elements(NeraNamespace + "placement"))
        {
            var placement = DeserializePlacement(placementElement, worksheet.Name);
            if (!seen.Add(placement.Item))
            {
                throw new InvalidDataException(
                    $"Worksheet '{worksheet.Name}' contains duplicate placement metadata for " +
                    $"'{placement.Item.Kind}:{placement.Item.Id}'.");
            }
            try
            {
                session.AnalyticsPlacements.RestorePlacement(worksheet, placement);
            }
            catch (InvalidOperationException exception)
            {
                throw InvalidMetadata(worksheet.Name, "placement", exception);
            }
        }
    }

    private static XElement SerializeChart(SpreadsheetChartDefinition chart)
    {
        var element = new XElement(
            NeraNamespace + "chart",
            new XAttribute("id", chart.Id.ToString("N", CultureInfo.InvariantCulture)),
            new XAttribute("name", chart.Name),
            new XAttribute("type", chart.ChartType),
            new XAttribute("firstRowContainsSeriesNames", chart.FirstRowContainsSeriesNames),
            new XAttribute("firstColumnContainsCategories", chart.FirstColumnContainsCategories));
        AddRangeAttributes(element, chart.SourceRange);
        if (chart.Title is { } title)
        {
            element.Add(new XAttribute("title", title));
        }
        return element;
    }

    private static XElement SerializePivot(SpreadsheetPivotDefinition pivot)
    {
        var element = new XElement(
            NeraNamespace + "pivot",
            new XAttribute("id", pivot.Id.ToString("N", CultureInfo.InvariantCulture)),
            new XAttribute("name", pivot.Name),
            new XAttribute("rowFieldColumnIndex", pivot.RowFieldColumnIndex),
            new XAttribute("valueFieldColumnIndex", pivot.ValueFieldColumnIndex),
            new XAttribute("aggregation", pivot.Aggregation),
            new XAttribute("firstRowContainsHeaders", pivot.FirstRowContainsHeaders));
        AddRangeAttributes(element, pivot.SourceRange);
        return element;
    }

    private static XElement SerializePlacement(SpreadsheetAnalyticsPlacement placement) =>
        new(
            NeraNamespace + "placement",
            new XAttribute("kind", placement.Item.Kind),
            new XAttribute("id", placement.Item.Id.ToString("N", CultureInfo.InvariantCulture)),
            new XAttribute("x", FormatDouble(placement.DocumentBounds.X)),
            new XAttribute("y", FormatDouble(placement.DocumentBounds.Y)),
            new XAttribute("width", FormatDouble(placement.DocumentBounds.Width)),
            new XAttribute("height", FormatDouble(placement.DocumentBounds.Height)),
            new XAttribute("zIndex", placement.ZIndex));

    private static SpreadsheetChartDefinition DeserializeChart(
        XElement element,
        string worksheetName)
    {
        var id = RequiredGuid(element, "id", worksheetName, "chart");
        var name = RequiredString(
            element,
            "name",
            $"Worksheet '{worksheetName}' contains a chart without a name.");
        var chartType = RequiredEnum<SpreadsheetChartType>(
            element,
            "type",
            worksheetName,
            "chart");
        var range = ReadRange(element, worksheetName, "chart");
        var firstRowContainsSeriesNames = RequiredBoolean(
            element,
            "firstRowContainsSeriesNames",
            worksheetName,
            "chart");
        var firstColumnContainsCategories = RequiredBoolean(
            element,
            "firstColumnContainsCategories",
            worksheetName,
            "chart");
        try
        {
            return new SpreadsheetChartDefinition(
                id,
                name,
                chartType,
                range,
                (string?)element.Attribute("title"),
                firstRowContainsSeriesNames,
                firstColumnContainsCategories);
        }
        catch (ArgumentException exception)
        {
            throw InvalidMetadata(worksheetName, "chart", exception);
        }
    }

    private static SpreadsheetPivotDefinition DeserializePivot(
        XElement element,
        string worksheetName)
    {
        var id = RequiredGuid(element, "id", worksheetName, "pivot");
        var name = RequiredString(
            element,
            "name",
            $"Worksheet '{worksheetName}' contains a pivot without a name.");
        var range = ReadRange(element, worksheetName, "pivot");
        var rowFieldColumnIndex = RequiredInt32(
            element,
            "rowFieldColumnIndex",
            worksheetName,
            "pivot",
            minimum: 0);
        var valueFieldColumnIndex = RequiredInt32(
            element,
            "valueFieldColumnIndex",
            worksheetName,
            "pivot",
            minimum: 0);
        var aggregation = RequiredEnum<SpreadsheetPivotAggregation>(
            element,
            "aggregation",
            worksheetName,
            "pivot");
        var firstRowContainsHeaders = RequiredBoolean(
            element,
            "firstRowContainsHeaders",
            worksheetName,
            "pivot");
        try
        {
            return new SpreadsheetPivotDefinition(
                id,
                name,
                range,
                rowFieldColumnIndex,
                valueFieldColumnIndex,
                aggregation,
                firstRowContainsHeaders);
        }
        catch (ArgumentException exception)
        {
            throw InvalidMetadata(worksheetName, "pivot", exception);
        }
    }

    private static SpreadsheetAnalyticsPlacement DeserializePlacement(
        XElement element,
        string worksheetName)
    {
        var kind = RequiredEnum<SpreadsheetAnalyticsItemKind>(
            element,
            "kind",
            worksheetName,
            "placement");
        var id = RequiredGuid(element, "id", worksheetName, "placement");
        var x = RequiredDouble(element, "x", worksheetName, "placement", allowZero: true);
        var y = RequiredDouble(element, "y", worksheetName, "placement", allowZero: true);
        var width = RequiredDouble(element, "width", worksheetName, "placement", allowZero: false);
        var height = RequiredDouble(element, "height", worksheetName, "placement", allowZero: false);
        var zIndex = RequiredInt32(
            element,
            "zIndex",
            worksheetName,
            "placement",
            minimum: 0);
        try
        {
            return new SpreadsheetAnalyticsPlacement(
                new SpreadsheetAnalyticsItemKey(kind, id),
                new RectD(x, y, width, height),
                zIndex);
        }
        catch (ArgumentException exception)
        {
            throw InvalidMetadata(worksheetName, "placement", exception);
        }
    }

    private static CellRange ReadRange(
        XElement element,
        string worksheetName,
        string itemKind)
    {
        var top = RequiredInt32(element, "top", worksheetName, itemKind, minimum: 0);
        var left = RequiredInt32(element, "left", worksheetName, itemKind, minimum: 0);
        var bottom = RequiredInt32(element, "bottom", worksheetName, itemKind, minimum: 0);
        var right = RequiredInt32(element, "right", worksheetName, itemKind, minimum: 0);
        if (top > bottom || left > right)
        {
            throw new InvalidDataException(
                $"Worksheet '{worksheetName}' contains an invalid {itemKind} source range.");
        }
        try
        {
            return new CellRange(
                new CellAddress(top, left),
                new CellAddress(bottom, right));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw InvalidMetadata(worksheetName, itemKind, exception);
        }
    }

    private static void AddRangeAttributes(XElement element, CellRange range)
    {
        element.Add(
            new XAttribute("top", range.Top),
            new XAttribute("left", range.Left),
            new XAttribute("bottom", range.Bottom),
            new XAttribute("right", range.Right));
    }

    private static Guid RequiredGuid(
        XElement element,
        string attributeName,
        string worksheetName,
        string itemKind)
    {
        var text = (string?)element.Attribute(attributeName);
        if (!Guid.TryParse(text, out var value) || value == Guid.Empty)
        {
            throw InvalidField(worksheetName, itemKind, attributeName);
        }
        return value;
    }

    private static TEnum RequiredEnum<TEnum>(
        XElement element,
        string attributeName,
        string worksheetName,
        string itemKind)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(
                (string?)element.Attribute(attributeName),
                ignoreCase: true,
                out var value) ||
            !Enum.IsDefined(value))
        {
            throw InvalidField(worksheetName, itemKind, attributeName);
        }
        return value;
    }

    private static bool RequiredBoolean(
        XElement element,
        string attributeName,
        string worksheetName,
        string itemKind)
    {
        if (!bool.TryParse((string?)element.Attribute(attributeName), out var value))
        {
            throw InvalidField(worksheetName, itemKind, attributeName);
        }
        return value;
    }

    private static int RequiredInt32(
        XElement element,
        string attributeName,
        string worksheetName,
        string itemKind,
        int minimum)
    {
        if (!int.TryParse(
                (string?)element.Attribute(attributeName),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) ||
            value < minimum)
        {
            throw InvalidField(worksheetName, itemKind, attributeName);
        }
        return value;
    }

    private static double RequiredDouble(
        XElement element,
        string attributeName,
        string worksheetName,
        string itemKind,
        bool allowZero)
    {
        if (!double.TryParse(
                (string?)element.Attribute(attributeName),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ||
            !double.IsFinite(value) ||
            (allowZero ? value < 0d : value <= 0d))
        {
            throw InvalidField(worksheetName, itemKind, attributeName);
        }
        return value;
    }

    private static string RequiredString(
        XElement element,
        string attributeName,
        string errorMessage)
    {
        var value = (string?)element.Attribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(errorMessage);
        }
        return value.Trim();
    }

    private static InvalidDataException InvalidField(
        string worksheetName,
        string itemKind,
        string fieldName) =>
        new($"Worksheet '{worksheetName}' contains an invalid {itemKind} '{fieldName}' value.");

    private static InvalidDataException InvalidMetadata(
        string worksheetName,
        string itemKind,
        Exception innerException) =>
        new(
            $"Worksheet '{worksheetName}' contains inconsistent Nera {itemKind} metadata.",
            innerException);

    private static string FormatDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
