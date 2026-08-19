using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

internal static class NeraOpenXmlStyleStateCodec
{
    private const string ContentType = "application/vnd.neraspreadsheet.style-state+xml";
    private const string NamespaceName = "urn:neraspreadsheet:style-state:1";
    private const int MaxPayloadCharacters = 64 * 1024 * 1024;
    private const int MaxCatalogEntries = 1_000_000;
    private const int MaxWorksheetEntries = 16_384;
    private const int MaxSpansPerAxis = 2_000_000;
    private static readonly XNamespace Namespace = NamespaceName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static ExactStyleState? Read(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        var matchingParts = workbookPart.CustomXmlParts
            .Where(static part => string.Equals(
                part.ContentType,
                ContentType,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingParts.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX package contains multiple Nera style-state parts.");
        }
        if (matchingParts.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = matchingParts[0].GetStream(
                FileMode.Open,
                FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
                MaxCharactersInDocument = MaxPayloadCharacters,
            });
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root?.Name != Namespace + "styleState" ||
                !string.Equals(
                    (string?)root.Attribute("version"),
                    "1",
                    StringComparison.Ordinal))
            {
                return null;
            }

            var payload = root.Element(Namespace + "payload")?.Value;
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidDataException(
                    "The Nera style-state part is missing its payload.");
            }
            payload = payload.Trim();
            if (payload.Length > MaxPayloadCharacters)
            {
                throw new InvalidDataException(
                    "The Nera style-state payload exceeds the supported size limit.");
            }

            var json = Convert.FromBase64String(payload);
            var dto = JsonSerializer.Deserialize<StyleStateDto>(json, JsonOptions)
                ?? throw new InvalidDataException(
                    "The Nera style-state payload is empty.");
            return ValidateAndConvert(dto);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "The Nera style-state payload is not valid base64.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The Nera style-state payload is not valid JSON.",
                exception);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                "The Nera style-state part is not valid XML.",
                exception);
        }
    }

    public static void Write(WorkbookPart workbookPart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        foreach (var existing in workbookPart.CustomXmlParts
                     .Where(static part => string.Equals(
                         part.ContentType,
                         ContentType,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            workbookPart.DeletePart(existing);
        }

        var dto = CreateDto(workbook);
        var json = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        var root = new XElement(
            Namespace + "styleState",
            new XAttribute("version", "1"),
            new XElement(
                Namespace + "payload",
                new XAttribute("encoding", "base64-json"),
                Convert.ToBase64String(json)));
        var customPart = workbookPart.AddCustomXmlPart(ContentType);
        using var stream = customPart.GetStream(
            FileMode.Create,
            FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        });
        new XDocument(root).Save(writer);
    }

    public static void RestoreCatalog(
        Workbook workbook,
        ExactStyleState state)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(state);
        ValidateCatalog(state.Catalog);
        for (var index = 1; index < state.Catalog.Length; index++)
        {
            var actual = workbook.Styles.Intern(state.Catalog[index]);
            if (actual != index)
            {
                throw new InvalidDataException(
                    "The Nera style catalog could not be restored with stable style identifiers.");
            }
        }
    }

    public static void RestoreWorksheet(
        Worksheet worksheet,
        ExactStyleState state)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Worksheets.TryGetValue(
                worksheet.Name,
                out var worksheetState))
        {
            return;
        }
        worksheet.RestoreAxisStyleState(
            worksheetState,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    SpreadsheetLimits.MaxColumns - 1)));
    }

    private static StyleStateDto CreateDto(Workbook workbook)
    {
        var worksheetStates = new List<WorksheetStyleStateDto>();
        foreach (var worksheet in workbook.Worksheets)
        {
            var state = worksheet.CaptureAxisStyleState();
            worksheetStates.Add(new WorksheetStyleStateDto
            {
                Name = worksheet.Name,
                NextSequence = state.NextSequence,
                RowSpans = state.RowSpans.Select(ToDto).ToArray(),
                ColumnSpans = state.ColumnSpans.Select(ToDto).ToArray(),
            });
        }
        return new StyleStateDto
        {
            Catalog = workbook.Styles.Snapshot().ToArray(),
            Worksheets = worksheetStates.ToArray(),
        };
    }

    private static AxisSpanDto ToDto(WorksheetAxisStyleSpan span) => new()
    {
        StartIndex = span.StartIndex,
        EndIndex = span.EndIndex,
        Operations = span.Operations.Select(static operation =>
            new AxisOperationDto
            {
                Sequence = operation.Sequence,
                Patch = operation.Patch,
            }).ToArray(),
    };

    private static ExactStyleState ValidateAndConvert(StyleStateDto dto)
    {
        if (dto.Catalog is null || dto.Catalog.Length == 0)
        {
            throw new InvalidDataException(
                "The Nera style-state catalog is missing.");
        }
        ValidateCatalog(dto.Catalog);
        if (dto.Worksheets is null)
        {
            throw new InvalidDataException(
                "The Nera style-state worksheet collection is missing.");
        }
        if (dto.Worksheets.Length > MaxWorksheetEntries)
        {
            throw new InvalidDataException(
                "The Nera style-state contains too many worksheets.");
        }

        var worksheets = new Dictionary<string, WorksheetAxisStyleState>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var worksheet in dto.Worksheets)
        {
            if (string.IsNullOrWhiteSpace(worksheet.Name))
            {
                throw new InvalidDataException(
                    "A Nera style-state worksheet name is missing.");
            }
            if (worksheet.NextSequence <= 0L)
            {
                throw new InvalidDataException(
                    $"The Nera style-state worksheet '{worksheet.Name}' has an invalid next sequence.");
            }

            var rowSpans = ConvertSpans(
                worksheet.RowSpans,
                SpreadsheetLimits.MaxRows,
                worksheet.NextSequence,
                worksheet.Name,
                "row");
            var columnSpans = ConvertSpans(
                worksheet.ColumnSpans,
                SpreadsheetLimits.MaxColumns,
                worksheet.NextSequence,
                worksheet.Name,
                "column");
            WorksheetAxisStyleState state;
            try
            {
                state = new WorksheetAxisStyleState(
                    rowSpans,
                    columnSpans,
                    worksheet.NextSequence);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"The Nera style-state worksheet '{worksheet.Name}' is invalid.",
                    exception);
            }

            if (!worksheets.TryAdd(worksheet.Name, state))
            {
                throw new InvalidDataException(
                    $"The Nera style-state contains duplicate worksheet '{worksheet.Name}'.");
            }
        }
        return new ExactStyleState(dto.Catalog, worksheets);
    }

    private static void ValidateCatalog(CellStyle[] catalog)
    {
        if (catalog.Length == 0 ||
            catalog[0] is not CellStyle first ||
            first != CellStyle.Default)
        {
            throw new InvalidDataException(
                "The Nera style catalog must start with the default style.");
        }
        if (catalog.Length > MaxCatalogEntries)
        {
            throw new InvalidDataException(
                "The Nera style catalog exceeds the supported entry limit.");
        }

        var validationCatalog = new CellStyleCatalog();
        for (var index = 1; index < catalog.Length; index++)
        {
            if (catalog[index] is not CellStyle style)
            {
                throw new InvalidDataException(
                    $"The Nera style catalog entry {index} is missing.");
            }

            int actual;
            try
            {
                actual = validationCatalog.Intern(style);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"The Nera style catalog entry {index} is invalid.",
                    exception);
            }
            if (actual != index)
            {
                throw new InvalidDataException(
                    $"The Nera style catalog entry {index} duplicates an earlier style.");
            }
        }
    }

    private static WorksheetAxisStyleSpan[] ConvertSpans(
        AxisSpanDto[]? spans,
        int axisLength,
        long nextSequence,
        string worksheetName,
        string axisName)
    {
        if (spans is null)
        {
            throw new InvalidDataException(
                $"The Nera {axisName}-style span collection for worksheet '{worksheetName}' is missing.");
        }
        if (spans.Length > MaxSpansPerAxis)
        {
            throw new InvalidDataException(
                $"The Nera {axisName}-style span collection for worksheet '{worksheetName}' exceeds the supported limit.");
        }

        var converted = new WorksheetAxisStyleSpan[spans.Length];
        var previousEndIndex = -1;
        for (var index = 0; index < spans.Length; index++)
        {
            var span = spans[index];
            if (span.StartIndex < 0 ||
                span.EndIndex < span.StartIndex ||
                span.EndIndex >= axisLength ||
                span.StartIndex <= previousEndIndex ||
                span.Operations is null ||
                span.Operations.Length == 0)
            {
                throw new InvalidDataException(
                    $"A Nera {axisName}-style span for worksheet '{worksheetName}' is invalid or overlaps a previous span.");
            }

            var operations = new WorksheetAxisStyleOperation[
                span.Operations.Length];
            var previousSequence = 0L;
            for (var operationIndex = 0;
                 operationIndex < span.Operations.Length;
                 operationIndex++)
            {
                var operation = span.Operations[operationIndex];
                if (operation.Sequence <= previousSequence ||
                    operation.Sequence >= nextSequence)
                {
                    throw new InvalidDataException(
                        $"A Nera {axisName}-style operation for worksheet '{worksheetName}' has an invalid sequence.");
                }
                if (operation.Patch is null || operation.Patch.IsEmpty)
                {
                    throw new InvalidDataException(
                        $"A Nera {axisName}-style operation for worksheet '{worksheetName}' has no style changes.");
                }

                try
                {
                    operations[operationIndex] =
                        new WorksheetAxisStyleOperation(
                            operation.Sequence,
                            operation.Patch);
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException(
                        $"A Nera {axisName}-style operation for worksheet '{worksheetName}' is invalid.",
                        exception);
                }
                previousSequence = operation.Sequence;
            }

            try
            {
                converted[index] = new WorksheetAxisStyleSpan(
                    span.StartIndex,
                    span.EndIndex,
                    operations);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"A Nera {axisName}-style span for worksheet '{worksheetName}' is invalid.",
                    exception);
            }
            previousEndIndex = span.EndIndex;
        }
        return converted;
    }

    internal sealed record ExactStyleState(
        CellStyle[] Catalog,
        IReadOnlyDictionary<string, WorksheetAxisStyleState> Worksheets);

    private sealed class StyleStateDto
    {
        public CellStyle[]? Catalog { get; set; }
        public WorksheetStyleStateDto[]? Worksheets { get; set; }
    }

    private sealed class WorksheetStyleStateDto
    {
        public string? Name { get; set; }
        public long NextSequence { get; set; }
        public AxisSpanDto[]? RowSpans { get; set; }
        public AxisSpanDto[]? ColumnSpans { get; set; }
    }

    private sealed class AxisSpanDto
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public AxisOperationDto[]? Operations { get; set; }
    }

    private sealed class AxisOperationDto
    {
        public long Sequence { get; set; }
        public CellStylePatch? Patch { get; set; }
    }
}
