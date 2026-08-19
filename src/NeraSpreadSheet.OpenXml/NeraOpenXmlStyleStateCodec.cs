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
    private static readonly XNamespace Namespace = NamespaceName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static ExactStyleState? Read(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        foreach (var part in workbookPart.CustomXmlParts.Where(static part =>
                     string.Equals(part.ContentType, ContentType, StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
            });
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root?.Name != Namespace + "styleState" ||
                !string.Equals((string?)root.Attribute("version"), "1", StringComparison.Ordinal))
            {
                continue;
            }
            var payload = root.Element(Namespace + "payload")?.Value;
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidDataException("The Nera style-state part is missing its payload.");
            }
            try
            {
                var json = Convert.FromBase64String(payload.Trim());
                var dto = JsonSerializer.Deserialize<StyleStateDto>(json, JsonOptions)
                    ?? throw new InvalidDataException("The Nera style-state payload is empty.");
                return ValidateAndConvert(dto);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The Nera style-state payload is not valid base64.", exception);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The Nera style-state payload is not valid JSON.", exception);
            }
        }
        return null;
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
        using var stream = customPart.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        });
        new XDocument(root).Save(writer);
    }

    public static void RestoreCatalog(Workbook workbook, ExactStyleState state)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(state);
        if (state.Catalog.Length == 0 || state.Catalog[0] != CellStyle.Default)
        {
            throw new InvalidDataException("The Nera style catalog must start with the default style.");
        }
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
        if (!state.Worksheets.TryGetValue(worksheet.Name, out var worksheetState))
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
        Operations = span.Operations.Select(static operation => new AxisOperationDto
        {
            Sequence = operation.Sequence,
            Patch = operation.Patch,
        }).ToArray(),
    };

    private static ExactStyleState ValidateAndConvert(StyleStateDto dto)
    {
        if (dto.Catalog is null || dto.Catalog.Length == 0)
        {
            throw new InvalidDataException("The Nera style-state catalog is missing.");
        }
        if (dto.Worksheets is null)
        {
            throw new InvalidDataException("The Nera style-state worksheet collection is missing.");
        }
        var worksheets = new Dictionary<string, WorksheetAxisStyleState>(StringComparer.OrdinalIgnoreCase);
        foreach (var worksheet in dto.Worksheets)
        {
            if (string.IsNullOrWhiteSpace(worksheet.Name))
            {
                throw new InvalidDataException("A Nera style-state worksheet name is missing.");
            }
            if (!worksheets.TryAdd(
                    worksheet.Name,
                    new WorksheetAxisStyleState(
                        ConvertSpans(worksheet.RowSpans, SpreadsheetLimits.MaxRows),
                        ConvertSpans(worksheet.ColumnSpans, SpreadsheetLimits.MaxColumns),
                        worksheet.NextSequence)))
            {
                throw new InvalidDataException(
                    $"The Nera style-state contains duplicate worksheet '{worksheet.Name}'.");
            }
        }
        return new ExactStyleState(dto.Catalog, worksheets);
    }

    private static WorksheetAxisStyleSpan[] ConvertSpans(
        AxisSpanDto[]? spans,
        int axisLength)
    {
        if (spans is null)
        {
            throw new InvalidDataException("A Nera axis-style span collection is missing.");
        }
        var converted = new WorksheetAxisStyleSpan[spans.Length];
        for (var index = 0; index < spans.Length; index++)
        {
            var span = spans[index];
            if (span.StartIndex < 0 ||
                span.EndIndex < span.StartIndex ||
                span.EndIndex >= axisLength ||
                span.Operations is null ||
                span.Operations.Length == 0)
            {
                throw new InvalidDataException("A Nera axis-style span is invalid.");
            }
            converted[index] = new WorksheetAxisStyleSpan(
                span.StartIndex,
                span.EndIndex,
                span.Operations.Select(static operation =>
                    new WorksheetAxisStyleOperation(
                        operation.Sequence,
                        operation.Patch ?? throw new InvalidDataException(
                            "A Nera axis-style operation patch is missing."))));
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
