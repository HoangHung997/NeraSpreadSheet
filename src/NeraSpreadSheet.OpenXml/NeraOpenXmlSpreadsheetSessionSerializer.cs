using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
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
        var session = await Task.Run(
            () => new SpreadsheetSession(workbook),
            cancellationToken).ConfigureAwait(false);

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
            PatchStandardSheetView(document, mapping.WorksheetPart, mapping.Worksheet,
                session.View.GetWorksheetState(mapping.Worksheet));
        }
        var workbook = document.WorkbookPart?.Workbook
            ?? throw new InvalidDataException("Workbook XML is missing.");
        var activeIndex = session.Workbook.Worksheets.ToList().IndexOf(session.ActiveWorksheet);
        var bookViews = workbook.GetFirstChild<BookViews>();
        if (bookViews is not null || activeIndex != 0)
        {
            bookViews ??= EnsureBookViews(workbook);
            var viewId = SelectedWorkbookViewId(document, session.Workbook);
            var view = bookViews.Elements<WorkbookView>().ElementAtOrDefault(checked((int)viewId))
                ?? throw new InvalidDataException("A sheet view refers to an absent workbook view.");
            view.ActiveTab = checked((uint)activeIndex);
            workbook.Save();
        }
    }

    private static BookViews EnsureBookViews(DocumentFormat.OpenXml.Spreadsheet.Workbook workbook)
    {
        var views = workbook.GetFirstChild<BookViews>();
        if (views is null)
        {
            views = new BookViews(new WorkbookView());
            workbook.AddChild(views, true);
        }
        else if (!views.Elements<WorkbookView>().Any())
        {
            views.Append(new WorkbookView());
        }
        return views;
    }

    private static SheetView? SelectedSheetView(WorksheetPart part) =>
        part.Worksheet?.GetFirstChild<SheetViews>()?.Elements<SheetView>().LastOrDefault();

    private static uint SelectedWorkbookViewId(SpreadsheetDocument document, Workbook workbook) =>
        EnumerateWorksheetMappings(document, workbook)
            .Select(mapping => SelectedSheetView(mapping.WorksheetPart)?.WorkbookViewId?.Value)
            .FirstOrDefault(id => id.HasValue) ?? 0U;

    private static void PatchStandardSheetView(SpreadsheetDocument document, WorksheetPart part,
        NeraWorksheet worksheet, SpreadsheetWorksheetViewState viewState)
    {
        var xml = part.Worksheet ?? throw new InvalidDataException("Worksheet XML is missing.");
        if (xml.Elements<SheetViews>().Skip(1).Any())
            throw new InvalidDataException("A worksheet contains duplicate SheetViews collections.");
        var views = xml.GetFirstChild<SheetViews>();
        var view = views?.Elements<SheetView>().LastOrDefault();
        if (view is null && !HasPersistentViewState(viewState)) return;
        if (view is null)
        {
            EnsureBookViews(document.WorkbookPart?.Workbook ?? throw new InvalidDataException("Workbook XML is missing."));
            views ??= new SheetViews();
            if (views.Parent is null) xml.AddChild(views, true);
            view = new SheetView { WorkbookViewId = 0U };
            views.Append(view);
        }
        var workbookXml = document.WorkbookPart?.Workbook ?? throw new InvalidDataException("Workbook XML is missing.");
        var bookViews = workbookXml.GetFirstChild<BookViews>();
        var viewId = view.WorkbookViewId?.Value ?? 0U;
        if (bookViews is null && viewId == 0U) bookViews = EnsureBookViews(workbookXml);
        if (bookViews is null || viewId >= bookViews.Elements<WorkbookView>().Count())
            throw new InvalidDataException("A worksheet view refers to an absent workbook view.");
        view.WorkbookViewId ??= 0U;
        // The workbook serializer preserves the original XML envelope. Patch only the
        // selected view; never remove its siblings, view ID, extension children or unknown attributes.
        var state = viewState.SplitState;
        var pane = view.GetFirstChild<Pane>();
        var frozen = viewState.FrozenRows > 0 || viewState.FrozenColumns > 0;
        PaneValues activePane;
        if (state.HasSplitPanes || frozen)
        {
            pane ??= new Pane();
            if (pane.Parent is null) view.AddChild(pane, true);
            if (state.HasSplitPanes)
            {
                pane.State = PaneStateValues.Split;
                pane.HorizontalSplit = state.SplitX is { } x ? Math.Max(1d, x * TwipsPerPixel) : null;
                pane.VerticalSplit = state.SplitY is { } y ? Math.Max(1d, y * TwipsPerPixel) : null;
                activePane = ToOpenXmlPane(state.ActivePane);
                pane.TopLeftCell = ResolveStandardTopLeftCell(worksheet, state);
            }
            else
            {
                pane.State = PaneStateValues.Frozen;
                pane.HorizontalSplit = viewState.FrozenColumns > 0 ? (double)viewState.FrozenColumns : null;
                pane.VerticalSplit = viewState.FrozenRows > 0 ? (double)viewState.FrozenRows : null;
                activePane = viewState.FrozenRows > 0 && viewState.FrozenColumns > 0 ? PaneValues.BottomRight :
                    viewState.FrozenRows > 0 ? PaneValues.BottomLeft : PaneValues.TopRight;
                pane.TopLeftCell = ResolveViewportCell(worksheet, viewState, includeFreeze: true);
            }
            pane.ActivePane = activePane;
        }
        else
        {
            pane?.Remove();
            activePane = PaneValues.TopLeft;
        }
        view.TopLeftCell = ResolveViewportCell(worksheet, viewState, includeFreeze: false);
        view.ZoomScale = checked((uint)Math.Round(viewState.Zoom * 100d, MidpointRounding.AwayFromZero));
        var selection = view.Elements<Selection>().FirstOrDefault(item =>
            (item.Pane?.Value ?? PaneValues.TopLeft) == activePane);
        if (selection is null)
        {
            if (view.Elements<Selection>().Count() >= 4)
                throw new InvalidDataException("Cannot add a fifth pane selection to a sheet view.");
            selection = new Selection();
            view.AddChild(selection, true);
        }
        selection.Pane = state.HasSplitPanes || frozen ? activePane : null;
        var snapshot = viewState.Selection;
        var selectedRanges = snapshot.Ranges.ToArray();
        if (selectedRanges.Length > 1024)
            throw new InvalidDataException("Worksheet view persistence supports at most 1024 selection ranges.");
        var activeRange = selection.ActiveCellId?.Value;
        if (activeRange is null || activeRange.Value >= selectedRanges.Length ||
            !selectedRanges[checked((int)activeRange.Value)].Contains(snapshot.ActiveCell))
        {
            var index = Array.FindIndex(selectedRanges, range => range.Contains(snapshot.ActiveCell));
            if (index < 0) throw new InvalidDataException("The active cell must belong to a selected range.");
            activeRange = checked((uint)index);
        }
        selection.ActiveCell = snapshot.ActiveCell.ToA1();
        selection.ActiveCellId = activeRange.Value;
        selection.SequenceOfReferences = new ListValue<StringValue>
        {
            InnerText = string.Join(" ", selectedRanges.Select(FormatRange)),
        };
        xml.Save();
    }

    private static string ResolveViewportCell(NeraWorksheet worksheet, SpreadsheetWorksheetViewState state, bool includeFreeze)
    {
        var offsetX = state.OffsetX;
        var offsetY = state.OffsetY;
        if (includeFreeze)
        {
            offsetX += GetAxisOffset(state.FrozenColumns, worksheet.Dimensions.DefaultColumnWidth,
                worksheet.Dimensions.GetColumnOverrides(), worksheet.Dimensions.GetHiddenColumnRanges());
            offsetY += GetAxisOffset(state.FrozenRows, worksheet.Dimensions.DefaultRowHeight,
                worksheet.Dimensions.GetRowOverrides(), worksheet.Dimensions.GetHiddenRowRanges());
        }
        return ResolveStandardTopLeftCell(worksheet, default(SpreadsheetSplitViewState)
            .WithPaneScroll(SpreadsheetSplitViewPane.TopLeft, offsetX, offsetY));
    }

    private static bool HasPersistentViewState(SpreadsheetWorksheetViewState state) =>
        state.Zoom != 1d || state.SplitState != default || state.FrozenRows != 0 || state.FrozenColumns != 0 ||
        state.Selection.ActiveCell != default || state.Selection.AnchorCell != default ||
        state.Selection.Ranges.Count != 1 || state.Selection.Ranges[0] != new CellRange(default, default);

    private static string FormatRange(CellRange range) => range.TopLeft == range.BottomRight
        ? range.TopLeft.ToA1() : $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";

    private static string ResolveStandardTopLeftCell(
        NeraWorksheet worksheet,
        SpreadsheetSplitViewState state)
    {
        var scroll = state.Mode switch
        {
            SpreadsheetSplitViewMode.Vertical => state.TopRightScroll,
            SpreadsheetSplitViewMode.Horizontal => state.BottomLeftScroll,
            SpreadsheetSplitViewMode.Both => state.BottomRightScroll,
            _ => state.TopLeftScroll,
        };
        var rowIndex = FindAxisIndexAtOffset(
            scroll.OffsetY,
            SpreadsheetLimits.MaxRows,
            worksheet.Dimensions.DefaultRowHeight,
            worksheet.Dimensions.GetRowOverrides(),
            worksheet.Dimensions.GetHiddenRowRanges());
        var columnIndex = FindAxisIndexAtOffset(
            scroll.OffsetX,
            SpreadsheetLimits.MaxColumns,
            worksheet.Dimensions.DefaultColumnWidth,
            worksheet.Dimensions.GetColumnOverrides(),
            worksheet.Dimensions.GetHiddenColumnRanges());
        return new CellAddress(rowIndex, columnIndex).ToA1();
    }

    private static void ImportStandardSplitViews(SpreadsheetDocument document, SpreadsheetSession session)
    {
        foreach (var mapping in EnumerateWorksheetMappings(document, session.Workbook))
        {
            var view = SelectedSheetView(mapping.WorksheetPart);
            if (view is null) continue;
            var pane = view.GetFirstChild<Pane>();
            var split = ReadStandardSplitView(mapping.WorksheetPart, mapping.Worksheet);
            var frozen = pane?.State?.Value == PaneStateValues.Frozen || pane?.State?.Value == PaneStateValues.FrozenSplit;
            var frozenRows = frozen ? ParseFreezeCount(pane?.VerticalSplit?.Value, SpreadsheetLimits.MaxRows) : 0;
            var frozenColumns = frozen ? ParseFreezeCount(pane?.HorizontalSplit?.Value, SpreadsheetLimits.MaxColumns) : 0;
            var topLeft = !split.HasSplitPanes && frozen ? pane?.TopLeftCell?.Value : view.TopLeftCell?.Value;
            if (TryParseStandardScroll(topLeft, mapping.Worksheet, out var scroll))
            {
                if (frozen)
                {
                    scroll = new SpreadsheetPaneScrollOffset(
                        Math.Max(0, scroll.OffsetX - GetAxisOffset(frozenColumns, mapping.Worksheet.Dimensions.DefaultColumnWidth,
                            mapping.Worksheet.Dimensions.GetColumnOverrides(), mapping.Worksheet.Dimensions.GetHiddenColumnRanges())),
                        Math.Max(0, scroll.OffsetY - GetAxisOffset(frozenRows, mapping.Worksheet.Dimensions.DefaultRowHeight,
                            mapping.Worksheet.Dimensions.GetRowOverrides(), mapping.Worksheet.Dimensions.GetHiddenRowRanges())));
                }
                split = split.WithPaneScroll(SpreadsheetSplitViewPane.TopLeft, scroll.OffsetX, scroll.OffsetY);
            }
            var activePane = pane?.ActivePane?.Value ?? PaneValues.TopLeft;
            var selection = view.Elements<Selection>().FirstOrDefault(item => (item.Pane?.Value ?? PaneValues.TopLeft) == activePane)
                ?? view.Elements<Selection>().FirstOrDefault();
            var ranges = ParseViewRanges(selection?.SequenceOfReferences?.InnerText);
            var active = CellAddress.TryParseA1(selection?.ActiveCell?.Value, out var cell) ? cell : ranges[0].TopLeft;
            if (!ranges.Any(range => range.Contains(active)))
                throw new InvalidDataException("The selected view's active cell is outside its ranges.");
            var activeRangeId = selection?.ActiveCellId?.Value ?? 0U;
            var anchor = activeRangeId < ranges.Length && ranges[checked((int)activeRangeId)].Contains(active)
                ? ranges[checked((int)activeRangeId)].TopLeft : ranges.First(range => range.Contains(active)).TopLeft;
            var zoom = view.ZoomScale?.Value is { } percent ? percent / 100d : 1d;
            try
            {
                session.View.SetWorksheetState(mapping.Worksheet, new SpreadsheetWorksheetViewState(
                    new SelectionSnapshot(active, anchor, ranges, 0), zoom, split, frozenRows, frozenColumns));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The XLSX sheet view contains invalid selection, zoom or pane state.", exception);
            }
        }
        var viewId = SelectedWorkbookViewId(document, session.Workbook);
        var workbookView = document.WorkbookPart?.Workbook.GetFirstChild<BookViews>()?
            .Elements<WorkbookView>().ElementAtOrDefault(checked((int)viewId));
        if (workbookView?.ActiveTab?.Value is { } activeTab && activeTab < session.Workbook.Worksheets.Count)
            session.ActivateWorksheet(session.Workbook.Worksheets[checked((int)activeTab)]);
    }

    private static int ParseFreezeCount(double? value, int limit)
    {
        if (value is null) return 0;
        if (!double.IsFinite(value.Value) || value < 0 || value >= limit || value != Math.Truncate(value.Value))
            throw new InvalidDataException("Frozen row/column counts must be finite integral worksheet indices.");
        return checked((int)value.Value);
    }

    private static CellRange[] ParseViewRanges(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [new CellRange(default, default)];
        if (text.Length > 65536) throw new InvalidDataException("Worksheet selection text exceeds the supported limit.");
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1024) throw new InvalidDataException("A worksheet view has too many selection ranges.");
        return tokens.Select(token =>
        {
            var ends = token.Replace("$", string.Empty, StringComparison.Ordinal).Split(':');
            if (ends.Length == 2 && ends.All(end => end.Length > 0 && end.All(char.IsAsciiLetter)) &&
                CellAddress.TryParseA1(ends[0] + "1", out var firstColumn) && CellAddress.TryParseA1(ends[1] + "1", out var lastColumn))
                return new CellRange(firstColumn, new CellAddress(SpreadsheetLimits.MaxRows - 1, lastColumn.ColumnIndex));
            if (ends.Length == 2 && ends.All(end => end.Length > 0 && end.All(char.IsAsciiDigit)) &&
                int.TryParse(ends[0], NumberStyles.None, CultureInfo.InvariantCulture, out var firstRow) &&
                int.TryParse(ends[1], NumberStyles.None, CultureInfo.InvariantCulture, out var lastRow) &&
                firstRow > 0 && lastRow > 0 && firstRow <= SpreadsheetLimits.MaxRows && lastRow <= SpreadsheetLimits.MaxRows)
                return new CellRange(new CellAddress(firstRow - 1, 0), new CellAddress(lastRow - 1, SpreadsheetLimits.MaxColumns - 1));
            if (ends.Length > 2 || !CellAddress.TryParseA1(ends[0], out var first) ||
                !CellAddress.TryParseA1(ends[^1], out var last))
                throw new InvalidDataException($"Unsupported worksheet selection reference '{token}'.");
            return new CellRange(first, last);
        }).ToArray();
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
                worksheet.Dimensions.GetColumnOverrides(),
                worksheet.Dimensions.GetHiddenColumnRanges()),
            GetAxisOffset(
                address.RowIndex,
                worksheet.Dimensions.DefaultRowHeight,
                worksheet.Dimensions.GetRowOverrides(),
                worksheet.Dimensions.GetHiddenRowRanges()));
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
                session.View.GetSplitState(worksheet),
                session.View.GetWorksheetState(worksheet)))
            .Where(static item => HasPersistentViewState(item.ViewState!))
            .ToArray();
        if (states.Length == 0 && ReferenceEquals(session.ActiveWorksheet, session.Workbook.Worksheets[0]))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var root = new XElement(
            NeraNamespace + "worksheetViews",
            new XAttribute("version", "1"),
            new XAttribute("activeWorksheet", session.ActiveWorksheet.Name));
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
        if (item.ViewState is { } view)
        {
            if (view.Selection.Ranges.Count > 1024)
                throw new InvalidDataException("Worksheet view persistence supports at most 1024 selection ranges.");
            element.Add(new XAttribute("zoom", FormatDouble(view.Zoom)),
                new XAttribute("frozenRows", view.FrozenRows), new XAttribute("frozenColumns", view.FrozenColumns));
            element.Add(new XElement(NeraNamespace + "selection",
                new XAttribute("active", view.Selection.ActiveCell.ToA1()),
                new XAttribute("anchor", view.Selection.AnchorCell.ToA1()),
                view.Selection.Ranges.Select(range => new XElement(NeraNamespace + "range", new XAttribute("ref", FormatRange(range))))));
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

        var states = new Dictionary<string, (SpreadsheetSplitViewState Split, XElement Element)>(StringComparer.OrdinalIgnoreCase);
        string? activeWorksheet = null;
        foreach (var part in workbookPart.CustomXmlParts.Where(static part => string.Equals(
                     part.ContentType,
                     NeraViewStateContentType,
                     StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                MaxCharactersInDocument = 16L * 1024L * 1024L,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
            });
            var documentXml = XDocument.Load(reader, LoadOptions.None);
            if (documentXml.Root?.Name != NeraNamespace + "worksheetViews")
            {
                continue;
            }
            if ((string?)documentXml.Root.Attribute("version") is { } version && version != "1")
                throw new InvalidDataException("Unsupported native worksheet-view metadata version.");
            activeWorksheet = (string?)documentXml.Root.Attribute("activeWorksheet") ?? activeWorksheet;
            foreach (var worksheetElement in documentXml.Root.Elements(NeraNamespace + "worksheet"))
            {
                var item = DeserializeWorksheetState(worksheetElement);
                if (!states.TryAdd(item.WorksheetName, (item.State, worksheetElement)))
                    throw new InvalidDataException("Duplicate native worksheet view entries.");
            }
        }

        foreach (var worksheet in session.Workbook.Worksheets)
        {
            if (states.TryGetValue(worksheet.Name, out var state))
            {
                var fallback = session.View.GetWorksheetState(worksheet);
                session.View.SetWorksheetState(worksheet, ReadNativeWorksheetState(state.Element, state.Split, fallback));
            }
        }
        if (activeWorksheet is not null)
        {
            var selected = session.Workbook.Worksheets.FirstOrDefault(worksheet =>
                string.Equals(worksheet.Name, activeWorksheet, StringComparison.OrdinalIgnoreCase));
            if (selected is null) throw new InvalidDataException("Native view metadata refers to an absent active worksheet.");
            session.ActivateWorksheet(selected);
        }
    }

    private static SpreadsheetWorksheetViewState ReadNativeWorksheetState(XElement element,
        SpreadsheetSplitViewState split, SpreadsheetWorksheetViewState fallback)
    {
        var name = (string?)element.Attribute("name") ?? string.Empty;
        var zoom = ParseOptionalDouble(element.Attribute("zoom"), name, "zoom") ?? fallback.Zoom;
        var rows = element.Attribute("frozenRows") is { } rowAttribute
            ? ParseFreezeCount(ParseRequiredDouble(rowAttribute, name, "frozenRows"), SpreadsheetLimits.MaxRows) : fallback.FrozenRows;
        var columns = element.Attribute("frozenColumns") is { } columnAttribute
            ? ParseFreezeCount(ParseRequiredDouble(columnAttribute, name, "frozenColumns"), SpreadsheetLimits.MaxColumns) : fallback.FrozenColumns;
        var selection = fallback.Selection;
        if (element.Element(NeraNamespace + "selection") is { } selected)
        {
            var rangeElements = selected.Elements(NeraNamespace + "range").Take(1025).ToArray();
            if (rangeElements.Length == 0 || rangeElements.Length > 1024)
                throw new InvalidDataException("Native selection range count is invalid.");
            var ranges = ParseViewRanges(string.Join(" ", rangeElements.Select(range =>
                (string?)range.Attribute("ref") ?? throw new InvalidDataException("Native selection range is missing its reference."))));
            if (!CellAddress.TryParseA1((string?)selected.Attribute("active"), out var active) ||
                !CellAddress.TryParseA1((string?)selected.Attribute("anchor"), out var anchor) ||
                !ranges.Any(range => range.Contains(active)))
                throw new InvalidDataException("Native active/anchor selection metadata is invalid.");
            selection = new SelectionSnapshot(active, anchor, ranges, 0);
        }
        try { return new SpreadsheetWorksheetViewState(selection, zoom, split, rows, columns); }
        catch (ArgumentException exception) { throw new InvalidDataException("Native worksheet view state is invalid.", exception); }
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
        IReadOnlyDictionary<int, double> overrides,
        IReadOnlyList<WorksheetAxisInterval> hiddenRanges)
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
            if (GetAxisOffset(middle, defaultSize, overrides, hiddenRanges) <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        var candidate = Math.Min(low, axisLength - 1);
        foreach (var range in hiddenRanges)
        {
            if (candidate < range.Start)
            {
                break;
            }
            if (candidate <= range.End)
            {
                return range.End < axisLength - 1
                    ? range.End + 1
                    : Math.Max(0, range.Start - 1);
            }
        }
        return candidate;
    }

    private static double GetAxisOffset(
        int index,
        double defaultSize,
        IReadOnlyDictionary<int, double> overrides,
        IReadOnlyList<WorksheetAxisInterval> hiddenRanges)
    {
        var offset = GetRawAxisOffset(index, defaultSize, overrides);
        foreach (var range in hiddenRanges)
        {
            if (range.Start >= index)
            {
                break;
            }
            var endExclusive = Math.Min(index, checked(range.End + 1));
            offset -= GetRawAxisOffset(endExclusive, defaultSize, overrides) -
                      GetRawAxisOffset(range.Start, defaultSize, overrides);
        }
        return offset;
    }

    private static double GetRawAxisOffset(
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
        SpreadsheetSplitViewState State,
        SpreadsheetWorksheetViewState? ViewState = null);
}
