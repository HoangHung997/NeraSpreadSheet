using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;
using OpenXmlWorksheet = DocumentFormat.OpenXml.Spreadsheet.Worksheet;

namespace NeraSpreadSheet.OpenXml;

public sealed record OpenXmlSessionSerializerCapabilities(
    bool ReadsStandardSplitPanes,
    bool WritesStandardSplitPanes,
    bool ReadsNativeSplitViewState,
    bool WritesNativeSplitViewState);

public interface IOpenXmlSpreadsheetSessionSerializer
{
    Task<SpreadsheetSession> LoadSessionAsync(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken = default);

    Task SaveSessionAsync(
        SpreadsheetSession session,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Adds session-level persistence to the workbook serializer. Standard XLSX
/// pane markup is emitted for split-view interoperability; versioned Nera
/// custom XML parts retain independent pane offsets plus native analytics
/// definitions, identities, and floating placement metadata.
/// </summary>
public sealed class NeraOpenXmlSpreadsheetSessionSerializer : IOpenXmlSpreadsheetSessionSerializer
{
    private const string NeraViewStateContentType = "application/vnd.neraspreadsheet.view-state+xml";
    private const string NeraViewStateNamespace = "urn:neraspreadsheet:view-state:1";
    private const double TwipsPerPixel = 15d;
    private static readonly XNamespace NeraNamespace = NeraViewStateNamespace;
    private readonly NeraOpenXmlWorkbookSerializer _workbookSerializer;

    public NeraOpenXmlSpreadsheetSessionSerializer(
        NeraOpenXmlWorkbookSerializer? workbookSerializer = null)
    {
        _workbookSerializer = workbookSerializer ?? new NeraOpenXmlWorkbookSerializer();
    }

    public OpenXmlSessionSerializerCapabilities Capabilities { get; } = new(
        ReadsStandardSplitPanes: true,
        WritesStandardSplitPanes: true,
        ReadsNativeSplitViewState: true,
        WritesNativeSplitViewState: true);

    public async Task<SpreadsheetSession> LoadSessionAsync(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        await using var buffer = await CopyToBufferAsync(source, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0L;
        var workbook = await _workbookSerializer
            .LoadAsync(buffer, options, cancellationToken)
            .ConfigureAwait(false);
        var session = new SpreadsheetSession(workbook);

        buffer.Position = 0L;
        using var document = SpreadsheetDocument.Open(buffer, false);
        ImportStandardSplitViews(document, session);
        ImportNativeSplitViews(document, session);
        NeraOpenXmlAnalyticsStateCodec.Import(document, session);
        NeraOpenXmlPivotTableCodec.Import(document, session, options.PreserveUnknownParts);
        return session;
    }

    public async Task SaveSessionAsync(
        SpreadsheetSession session,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        await using var buffer = new MemoryStream();
        await _workbookSerializer
            .SaveAsync(session.Workbook, buffer, options, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        buffer.Position = 0L;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            ExportStandardSplitViews(document, session, cancellationToken);
            ExportNativeSplitViews(document, session, cancellationToken);
            NeraOpenXmlAnalyticsStateCodec.Export(document, session, cancellationToken);
            NeraOpenXmlPivotTableCodec.Export(document, session, cancellationToken);
            NeraOpenXmlChartDrawingCodec.Export(document, session, cancellationToken);
        }

        buffer.Position = 0L;
        cancellationToken.ThrowIfCancellationRequested();
        await OpenXmlPackageWriteRecovery.WritePackageAsync(
            destination,
            buffer.ToArray()).ConfigureAwait(false);
    }

    private static async Task<MemoryStream> CopyToBufferAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        try
        {
            await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void ExportStandardSplitViews(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        CancellationToken cancellationToken)
    {
        foreach (var mapping in EnumerateWorksheetMappings(document, session.Workbook))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = session.View.GetSplitState(mapping.Worksheet);
            ReplaceStandardSheetView(mapping.WorksheetPart, mapping.Worksheet, state);
        }
    }

    private static void ReplaceStandardSheetView(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        SpreadsheetSplitViewState state)
    {
        var openXmlWorksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException("The XLSX worksheet part does not contain worksheet markup.");
        foreach (var existing in openXmlWorksheet.Elements<SheetViews>().ToArray())
        {
            existing.Remove();
        }
        if (!state.HasSplitPanes)
        {
            openXmlWorksheet.Save();
            return;
        }

        var sheetView = new SheetView { WorkbookViewId = 0U };
        var pane = new Pane
        {
            State = PaneStateValues.Split,
            ActivePane = ToOpenXmlPane(state.ActivePane),
            TopLeftCell = ResolveStandardTopLeftCell(worksheet, state),
        };
        if (state.SplitX is { } splitX)
        {
            pane.HorizontalSplit = Math.Max(1d, splitX * TwipsPerPixel);
        }
        if (state.SplitY is { } splitY)
        {
            pane.VerticalSplit = Math.Max(1d, splitY * TwipsPerPixel);
        }
        sheetView.Append(pane);
        var sheetViews = new SheetViews(sheetView);
        openXmlWorksheet.PrependChild(sheetViews);
        openXmlWorksheet.Save();
    }

    private static string ResolveStandardTopLeftCell(
        NeraWorksheet worksheet,
        SpreadsheetSplitViewState state)
    {
        var scroll = state.Mode switch
        {
            SpreadsheetSplitViewMode.Vertical => state.TopRightScroll,
            SpreadsheetSplitViewMode.Horizontal => state.BottomLeftScroll,
            SpreadsheetSplitViewMode.Both => state.BottomRightScroll,
            _ => default,
        };
        var rowIndex = FindAxisIndexAtOffset(
            scroll.OffsetY,
            SpreadsheetLimits.MaxRows,
            worksheet.Dimensions.DefaultRowHeight,
            worksheet.Dimensions.GetRowOverrides());
        var columnIndex = FindAxisIndexAtOffset(
            scroll.OffsetX,
            SpreadsheetLimits.MaxColumns,
            worksheet.Dimensions.DefaultColumnWidth,
            worksheet.Dimensions.GetColumnOverrides());
        return new CellAddress(rowIndex, columnIndex).ToA1();
    }

    private static void ImportStandardSplitViews(
        SpreadsheetDocument document,
        SpreadsheetSession session)
    {
        foreach (var mapping in EnumerateWorksheetMappings(document, session.Workbook))
        {
            var state = ReadStandardSplitView(mapping.WorksheetPart, mapping.Worksheet);
            if (state != default)
            {
                session.View.SetSplitState(
                    mapping.Worksheet,
                    state,
                    SpreadsheetSplitViewChangeKind.State,
                    source: null);
            }
        }
    }

    private static SpreadsheetSplitViewState ReadStandardSplitView(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet)
    {
        var sheetViews = worksheetPart.Worksheet?.GetFirstChild<SheetViews>();
        var sheetView = sheetViews?.Elements<SheetView>().LastOrDefault();
        var pane = sheetView?.GetFirstChild<Pane>();
        if (pane is null)
        {
            return default;
        }
        var paneState = pane.State?.Value;
        if (paneState == PaneStateValues.Frozen ||
            paneState == PaneStateValues.FrozenSplit)
        {
            return default;
        }

        var splitXTwips = pane.HorizontalSplit?.Value;
        var splitYTwips = pane.VerticalSplit?.Value;
        double? splitX = splitXTwips is > 0d
            ? splitXTwips.Value / TwipsPerPixel
            : null;
        double? splitY = splitYTwips is > 0d
            ? splitYTwips.Value / TwipsPerPixel
            : null;
        var mode = ResolveMode(splitX, splitY);
        if (mode == SpreadsheetSplitViewMode.None)
        {
            return default;
        }

        var activePane = FromOpenXmlPane(pane.ActivePane?.Value, mode);
        var bottomRightScroll = TryParseStandardScroll(
            pane.TopLeftCell?.Value,
            worksheet,
            out var parsedScroll)
            ? parsedScroll
            : default;
        var topRight = mode is SpreadsheetSplitViewMode.Vertical or SpreadsheetSplitViewMode.Both
            ? new SpreadsheetPaneScrollOffset(bottomRightScroll.OffsetX, 0d)
            : default;
        var bottomLeft = mode is SpreadsheetSplitViewMode.Horizontal or SpreadsheetSplitViewMode.Both
            ? new SpreadsheetPaneScrollOffset(0d, bottomRightScroll.OffsetY)
            : default;
        var bottomRight = mode == SpreadsheetSplitViewMode.Both
            ? bottomRightScroll
            : default;

        return new SpreadsheetSplitViewState(
            mode,
            splitX,
            splitY,
            activePane,
            topRightScroll: topRight,
            bottomLeftScroll: bottomLeft,
            bottomRightScroll: bottomRight);
    }

    private static bool TryParseStandardScroll(
        string? topLeftCell,
        NeraWorksheet worksheet,
        out SpreadsheetPaneScrollOffset scroll)
    {
        if (!CellAddress.TryParseA1(topLeftCell, out var address))
        {
            scroll = default;
            return false;
        }

        scroll = new SpreadsheetPaneScrollOffset(
            GetAxisOffset(
                address.ColumnIndex,
                worksheet.Dimensions.DefaultColumnWidth,
                worksheet.Dimensions.GetColumnOverrides()),
            GetAxisOffset(
                address.RowIndex,
                worksheet.Dimensions.DefaultRowHeight,
                worksheet.Dimensions.GetRowOverrides()));
        return true;
    }

    private static void ExportNativeSplitViews(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        CancellationToken cancellationToken)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
        foreach (var existing in workbookPart.CustomXmlParts
                     .Where(static part => string.Equals(
                         part.ContentType,
                         NeraViewStateContentType,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            workbookPart.DeletePart(existing);
        }

        var states = session.Workbook.Worksheets
            .Select(worksheet => new WorksheetSplitState(
                worksheet.Name,
                session.View.GetSplitState(worksheet)))
            .Where(static item => item.State != default)
            .ToArray();
        if (states.Length == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var root = new XElement(
            NeraNamespace + "worksheetViews",
            new XAttribute("version", "1"));
        foreach (var item in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            root.Add(SerializeWorksheetState(item));
        }

        var customPart = workbookPart.AddCustomXmlPart(NeraViewStateContentType);
        using var stream = customPart.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        });
        new XDocument(root).Save(writer);
    }

    private static XElement SerializeWorksheetState(WorksheetSplitState item)
    {
        var state = item.State;
        var element = new XElement(
            NeraNamespace + "worksheet",
            new XAttribute("name", item.WorksheetName),
            new XAttribute("mode", state.Mode),
            new XAttribute("activePane", state.ActivePane));
        if (state.SplitX is { } splitX)
        {
            element.Add(new XAttribute("splitX", FormatDouble(splitX)));
        }
        if (state.SplitY is { } splitY)
        {
            element.Add(new XAttribute("splitY", FormatDouble(splitY)));
        }
        foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
        {
            var scroll = state.GetPaneScroll(pane);
            element.Add(new XElement(
                NeraNamespace + "pane",
                new XAttribute("id", pane),
                new XAttribute("offsetX", FormatDouble(scroll.OffsetX)),
                new XAttribute("offsetY", FormatDouble(scroll.OffsetY))));
        }
        return element;
    }

    private static void ImportNativeSplitViews(
        SpreadsheetDocument document,
        SpreadsheetSession session)
    {
        var workbookPart = document.WorkbookPart;
        if (workbookPart is null)
        {
            return;
        }

        var states = new Dictionary<string, SpreadsheetSplitViewState>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in workbookPart.CustomXmlParts.Where(static part => string.Equals(
                     part.ContentType,
                     NeraViewStateContentType,
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
            if (documentXml.Root?.Name != NeraNamespace + "worksheetViews")
            {
                continue;
            }
            foreach (var worksheetElement in documentXml.Root.Elements(NeraNamespace + "worksheet"))
            {
                var item = DeserializeWorksheetState(worksheetElement);
                states[item.WorksheetName] = item.State;
            }
        }

        foreach (var worksheet in session.Workbook.Worksheets)
        {
            if (states.TryGetValue(worksheet.Name, out var state))
            {
                session.View.SetSplitState(
                    worksheet,
                    state,
                    SpreadsheetSplitViewChangeKind.State,
                    source: null);
            }
        }
    }

    private static WorksheetSplitState DeserializeWorksheetState(XElement element)
    {
        var worksheetName = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            throw new InvalidDataException("A Nera worksheet-view entry is missing its worksheet name.");
        }
        if (!Enum.TryParse<SpreadsheetSplitViewMode>(
                (string?)element.Attribute("mode"),
                ignoreCase: true,
                out var mode) ||
            !Enum.IsDefined(mode))
        {
            throw new InvalidDataException($"Worksheet '{worksheetName}' has an invalid split mode.");
        }
        if (!Enum.TryParse<SpreadsheetSplitViewPane>(
                (string?)element.Attribute("activePane"),
                ignoreCase: true,
                out var activePane) ||
            !Enum.IsDefined(activePane))
        {
            throw new InvalidDataException($"Worksheet '{worksheetName}' has an invalid active split pane.");
        }

        var splitX = ParseOptionalDouble(element.Attribute("splitX"), worksheetName, "splitX");
        var splitY = ParseOptionalDouble(element.Attribute("splitY"), worksheetName, "splitY");
        var paneOffsets = new Dictionary<SpreadsheetSplitViewPane, SpreadsheetPaneScrollOffset>();
        foreach (var paneElement in element.Elements(NeraNamespace + "pane"))
        {
            if (!Enum.TryParse<SpreadsheetSplitViewPane>(
                    (string?)paneElement.Attribute("id"),
                    ignoreCase: true,
                    out var pane) ||
                !Enum.IsDefined(pane))
            {
                throw new InvalidDataException($"Worksheet '{worksheetName}' contains an invalid pane identifier.");
            }
            if (paneOffsets.ContainsKey(pane))
            {
                throw new InvalidDataException($"Worksheet '{worksheetName}' contains duplicate pane state for '{pane}'.");
            }
            paneOffsets.Add(
                pane,
                new SpreadsheetPaneScrollOffset(
                    ParseRequiredDouble(paneElement.Attribute("offsetX"), worksheetName, "offsetX"),
                    ParseRequiredDouble(paneElement.Attribute("offsetY"), worksheetName, "offsetY")));
        }

        try
        {
            return new WorksheetSplitState(
                worksheetName,
                new SpreadsheetSplitViewState(
                    mode,
                    splitX,
                    splitY,
                    activePane,
                    paneOffsets.GetValueOrDefault(SpreadsheetSplitViewPane.TopLeft),
                    paneOffsets.GetValueOrDefault(SpreadsheetSplitViewPane.TopRight),
                    paneOffsets.GetValueOrDefault(SpreadsheetSplitViewPane.BottomLeft),
                    paneOffsets.GetValueOrDefault(SpreadsheetSplitViewPane.BottomRight)));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Worksheet '{worksheetName}' contains inconsistent Nera split-view metadata.",
                exception);
        }
    }

    private static IEnumerable<WorksheetMapping> EnumerateWorksheetMappings(
        SpreadsheetDocument document,
        Workbook workbook)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
        var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray()
            ?? throw new InvalidDataException("The XLSX workbook does not contain a sheets collection.");
        var count = Math.Min(sheets.Length, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }
            yield return new WorksheetMapping(workbook.Worksheets[index], worksheetPart);
        }
    }

    private static SpreadsheetSplitViewMode ResolveMode(double? splitX, double? splitY) =>
        (splitX, splitY) switch
        {
            (not null, not null) => SpreadsheetSplitViewMode.Both,
            (not null, null) => SpreadsheetSplitViewMode.Vertical,
            (null, not null) => SpreadsheetSplitViewMode.Horizontal,
            _ => SpreadsheetSplitViewMode.None,
        };

    private static PaneValues ToOpenXmlPane(SpreadsheetSplitViewPane pane) => pane switch
    {
        SpreadsheetSplitViewPane.TopLeft => PaneValues.TopLeft,
        SpreadsheetSplitViewPane.TopRight => PaneValues.TopRight,
        SpreadsheetSplitViewPane.BottomLeft => PaneValues.BottomLeft,
        SpreadsheetSplitViewPane.BottomRight => PaneValues.BottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(pane)),
    };

    private static SpreadsheetSplitViewPane FromOpenXmlPane(
        PaneValues? pane,
        SpreadsheetSplitViewMode mode)
    {
        var result = SpreadsheetSplitViewPane.TopLeft;
        if (pane == PaneValues.TopRight)
        {
            result = SpreadsheetSplitViewPane.TopRight;
        }
        else if (pane == PaneValues.BottomLeft)
        {
            result = SpreadsheetSplitViewPane.BottomLeft;
        }
        else if (pane == PaneValues.BottomRight)
        {
            result = SpreadsheetSplitViewPane.BottomRight;
        }
        return SpreadsheetSplitViewState.IsPaneVisible(mode, result)
            ? result
            : SpreadsheetSplitViewPane.TopLeft;
    }

    private static int FindAxisIndexAtOffset(
        double offset,
        int axisLength,
        double defaultSize,
        IReadOnlyDictionary<int, double> overrides)
    {
        if (offset <= 0d)
        {
            return 0;
        }
        var low = 0;
        var high = axisLength;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (GetAxisOffset(middle, defaultSize, overrides) <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return Math.Min(low, axisLength - 1);
    }

    private static double GetAxisOffset(
        int index,
        double defaultSize,
        IReadOnlyDictionary<int, double> overrides)
    {
        var offset = index * defaultSize;
        foreach (var (overrideIndex, size) in overrides)
        {
            if (overrideIndex < index)
            {
                offset += size - defaultSize;
            }
        }
        return offset;
    }

    private static double? ParseOptionalDouble(
        XAttribute? attribute,
        string worksheetName,
        string fieldName) =>
        attribute is null ? null : ParseRequiredDouble(attribute, worksheetName, fieldName);

    private static double ParseRequiredDouble(
        XAttribute? attribute,
        string worksheetName,
        string fieldName)
    {
        var text = attribute?.Value;
        if (string.IsNullOrWhiteSpace(text) ||
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value) ||
            value < 0d)
        {
            throw new InvalidDataException(
                $"Worksheet '{worksheetName}' contains an invalid '{fieldName}' value.");
        }
        return value;
    }

    private static string FormatDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record WorksheetMapping(
        NeraWorksheet Worksheet,
        WorksheetPart WorksheetPart);

    private sealed record WorksheetSplitState(
        string WorksheetName,
        SpreadsheetSplitViewState State);
}
