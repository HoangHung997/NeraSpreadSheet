using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraCellStyle = NeraSpreadSheet.Core.CellStyle;
using NeraCellValue = NeraSpreadSheet.Core.CellValue;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;
using OpenXmlCellValue = DocumentFormat.OpenXml.Spreadsheet.CellValue;
using OpenXmlWorkbook = DocumentFormat.OpenXml.Spreadsheet.Workbook;
using OpenXmlWorksheet = DocumentFormat.OpenXml.Spreadsheet.Worksheet;

namespace NeraSpreadSheet.OpenXml;

public sealed class NeraOpenXmlWorkbookSerializer : IOpenXmlWorkbookSerializer
{
    private const double PixelsPerPoint = 96d / 72d;
    private const int MaxPreservedPackageBytes = 512 * 1024 * 1024;

    public OpenXmlSerializerCapabilities Capabilities { get; } = new(
        ReadsBasicCells: true,
        WritesBasicCells: true,
        ReadsFormulas: true,
        WritesFormulas: true,
        ReadsBasicDimensions: true,
        WritesBasicDimensions: true,
        PreservesUnknownParts: true,
        ReadsMergedCells: true,
        WritesMergedCells: true);

    public async Task<NeraWorkbook> LoadAsync(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!source.CanRead)
        {
            throw new ArgumentException(
                "Source stream must be readable.",
                nameof(source));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!options.PreserveUnknownParts)
        {
            return LoadCore(
                source,
                options,
                cancellationToken);
        }

        var packageBytes = await ReadPreservedPackageAsync(
            source,
            cancellationToken).ConfigureAwait(false);
        OpenXmlPackageGraphValidator.Validate(packageBytes);
        using var buffer = new MemoryStream(
            packageBytes,
            writable: false);
        var workbook = LoadCore(
            buffer,
            options,
            cancellationToken);
        OpenXmlPackageEnvelopeStore.Attach(
            workbook,
            OpenXmlPackageEnvelope.CaptureValidated(
                packageBytes,
                workbook));
        return workbook;
    }

    public async Task SaveAsync(
        NeraWorkbook workbook,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!options.PreserveUnknownParts)
        {
            SaveCore(
                workbook,
                destination,
                options,
                cancellationToken);
            OpenXmlPackageEnvelopeStore.Detach(workbook);
            return;
        }

        OpenXmlPackageEnvelope? envelope = null;
        if (OpenXmlPackageEnvelopeStore.TryGet(
                workbook,
                out var capturedEnvelope) &&
            capturedEnvelope is not null)
        {
            envelope = capturedEnvelope;
            envelope.ValidateWorkbookTopology(workbook);
        }

        await using var generated = new MemoryStream();
        SaveCore(
            workbook,
            generated,
            options,
            cancellationToken);
        var generatedBytes = generated.ToArray();
        var outputBytes = envelope is null
            ? generatedBytes
            : OpenXmlPackagePreserver.Merge(
                workbook,
                envelope,
                generatedBytes,
                cancellationToken);
        var outputEnvelope = OpenXmlPackageEnvelope.Capture(
            outputBytes,
            workbook);
        await WritePackageAsync(
            destination,
            outputBytes,
            cancellationToken).ConfigureAwait(false);
        OpenXmlPackageEnvelopeStore.Attach(
            workbook,
            outputEnvelope);
    }

    private static NeraWorkbook LoadCore(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken)
    {
        using var document = SpreadsheetDocument.Open(source, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The XLSX package does not contain a workbook part.");
        var openXmlWorkbook = workbookPart.Workbook
            ?? throw new InvalidDataException(
                "The XLSX workbook part does not contain workbook markup.");
        var sheets = openXmlWorkbook.GetFirstChild<Sheets>()
            ?? throw new InvalidDataException(
                "The XLSX workbook does not contain a sheets collection.");

        var workbook = new NeraWorkbook(
            createDefaultWorksheet: false);
        var exactStyleState = NeraOpenXmlStyleStateCodec.Read(workbookPart);
        if (exactStyleState is not null)
        {
            NeraOpenXmlStyleStateCodec.RestoreCatalog(
                workbook,
                exactStyleState);
        }
        var styleTable = OpenXmlStyleTable.Read(
            workbookPart,
            workbook.Styles,
            exactStyleState?.Catalog);
        var sharedStrings =
            workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var sheet in sheets.Elements<Sheet>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheet.Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                continue;
            }
            if (workbookPart.GetPartById(
                    relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(sheet.Name?.Value)
                ? $"Sheet{workbook.Worksheets.Count + 1}"
                : sheet.Name!.Value!;
            var worksheet = workbook.AddWorksheet(name);
            ImportDimensions(
                worksheetPart,
                worksheet,
                styleTable,
                workbook.Styles,
                importAxisStyles: exactStyleState is null);
            ImportCells(
                worksheetPart,
                worksheet,
                sharedStrings,
                styleTable,
                workbook.Styles,
                options,
                cancellationToken);
            ImportMergedCells(
                worksheetPart,
                worksheet,
                cancellationToken);
            if (exactStyleState is not null)
            {
                NeraOpenXmlStyleStateCodec.RestoreWorksheet(
                    worksheet,
                    exactStyleState);
            }
        }

        if (workbook.Worksheets.Count == 0)
        {
            workbook.AddWorksheet("Sheet1");
        }
        return workbook;
    }

    private static void SaveCore(
        NeraWorkbook workbook,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken)
    {
        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }

        using var document = SpreadsheetDocument.Create(
            destination,
            SpreadsheetDocumentType.Workbook,
            true);
        var workbookPart = document.AddWorkbookPart();
        var openXmlWorkbook = new OpenXmlWorkbook();
        workbookPart.Workbook = openXmlWorkbook;
        var sheets = openXmlWorkbook.AppendChild(new Sheets());
        var styleTable = OpenXmlStyleTable.CreateForExport(workbook);
        styleTable.Write(workbookPart);
        uint sheetId = 1;

        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = BuildWorksheet(
                worksheet,
                workbook.Styles,
                styleTable,
                options,
                cancellationToken);
            worksheetPart.Worksheet.Save();
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = worksheet.Name,
            });
        }

        NeraOpenXmlStyleStateCodec.Write(
            workbookPart,
            workbook);
        openXmlWorkbook.Save();
    }

    private static async Task<byte[]> ReadPreservedPackageAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(
                chunk.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > MaxPreservedPackageBytes)
            {
                throw new InvalidDataException(
                    $"The XLSX package exceeds the preservation limit of {MaxPreservedPackageBytes} bytes.");
            }
            await buffer.WriteAsync(
                chunk.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }

    private static async Task WritePackageAsync(
        Stream destination,
        byte[] packageBytes,
        CancellationToken cancellationToken)
    {
        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }
        await destination.WriteAsync(
            packageBytes.AsMemory(),
            cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(
            cancellationToken).ConfigureAwait(false);
    }

    private static void ImportCells(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        SharedStringTable? sharedStrings,
        OpenXmlStyleTable styleTable,
        CellStyleCatalog catalog,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken)
    {
        var openXmlWorksheet = worksheetPart.Worksheet;
        if (openXmlWorksheet is null)
        {
            return;
        }
        var sheetData = openXmlWorksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return;
        }

        var formulaResolver = OpenXmlSharedFormulaImportResolver.Create(
            sheetData,
            cancellationToken);
        var changes = new List<KeyValuePair<CellAddress, CellData>>();
        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var cell in row.Elements<Cell>())
            {
                var reference = cell.CellReference?.Value;
                if (string.IsNullOrWhiteSpace(reference) ||
                    !CellAddress.TryParseA1(reference, out var address))
                {
                    continue;
                }

                var formula = formulaResolver.Resolve(cell, address);
                var value = formula is not null &&
                            !options.LoadCachedFormulaValues
                    ? NeraCellValue.Blank
                    : ReadValue(cell, sharedStrings);
                var styleId = cell.StyleIndex?.Value is uint styleIndex
                    ? catalog.Intern(styleTable.GetStyle(styleIndex))
                    : CellStyleCatalog.DefaultStyleId;
                var data = new CellData(
                    value,
                    formula,
                    styleId);
                if (!data.IsEmpty)
                {
                    changes.Add(
                        new KeyValuePair<CellAddress, CellData>(
                            address,
                            data));
                }
            }
        }
        worksheet.SetCells(changes);
    }

    private static void ImportMergedCells(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        CancellationToken cancellationToken)
    {
        var openXmlWorksheet = worksheetPart.Worksheet;
        if (openXmlWorksheet is null)
        {
            return;
        }

        foreach (var mergeCells in
                 openXmlWorksheet.Elements<MergeCells>())
        {
            foreach (var mergeCell in
                     mergeCells.Elements<MergeCell>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryParseCellRange(
                        mergeCell.Reference?.Value,
                        out var range))
                {
                    worksheet.MergeCells(
                        range,
                        clearNonTopLeftCells: false);
                }
            }
        }
    }

    private static NeraCellValue ReadValue(
        Cell cell,
        SharedStringTable? sharedStrings)
    {
        var dataType = cell.DataType?.Value;
        var raw = cell.CellValue?.Text;
        if (dataType == CellValues.InlineString)
        {
            return NeraCellValue.FromText(
                cell.InlineString?.InnerText);
        }
        if (dataType == CellValues.SharedString)
        {
            if (sharedStrings is null ||
                !int.TryParse(
                    raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var index) ||
                index < 0)
            {
                return NeraCellValue.Blank;
            }
            var item = sharedStrings
                .Elements<SharedStringItem>()
                .ElementAtOrDefault(index);
            return NeraCellValue.FromText(item?.InnerText);
        }
        if (dataType == CellValues.Boolean)
        {
            return NeraCellValue.FromBoolean(
                raw is "1" or "true" or "TRUE");
        }
        if (dataType == CellValues.Error)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? NeraCellValue.FromError("#VALUE!")
                : NeraCellValue.FromError(raw);
        }
        if (dataType == CellValues.Date)
        {
            return DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTime)
                ? NeraCellValue.FromDateTime(dateTime)
                : NeraCellValue.Blank;
        }
        if (dataType == CellValues.String)
        {
            return NeraCellValue.FromText(raw);
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            return NeraCellValue.Blank;
        }
        return double.TryParse(
                   raw,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var number) &&
               double.IsFinite(number)
            ? NeraCellValue.FromNumber(number)
            : NeraCellValue.FromText(raw);
    }

    private static void ImportDimensions(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        OpenXmlStyleTable styleTable,
        CellStyleCatalog catalog,
        bool importAxisStyles)
    {
        var openXmlWorksheet = worksheetPart.Worksheet;
        if (openXmlWorksheet is null)
        {
            return;
        }

        foreach (var columns in openXmlWorksheet.Elements<Columns>())
        {
            foreach (var column in columns.Elements<Column>())
            {
                var minimum = column.Min?.Value;
                var maximum = column.Max?.Value;
                if (minimum is null ||
                    maximum is null ||
                    minimum == 0 ||
                    maximum < minimum)
                {
                    continue;
                }

                var first = Math.Max(1, (int)minimum.Value) - 1;
                var last = Math.Min(
                               (int)maximum.Value,
                               SpreadsheetLimits.MaxColumns) -
                           1;
                if (last < first)
                {
                    continue;
                }
                var size = column.Hidden?.Value == true
                    ? 0d
                    : column.Width?.Value is double width
                        ? ExcelColumnWidthToPixels(width)
                        : worksheet.Dimensions.DefaultColumnWidth;
                for (var index = first; index <= last; index++)
                {
                    if (column.Width is not null ||
                        column.Hidden?.Value == true)
                    {
                        worksheet.Dimensions.SetColumnWidth(
                            index,
                            size);
                    }
                }
                if (importAxisStyles &&
                    column.Style?.Value is uint styleIndex)
                {
                    var style = styleTable.GetStyle(styleIndex);
                    catalog.Intern(style);
                    worksheet.ApplyAxisStyle(
                        WorksheetAxis.Column,
                        first,
                        last,
                        CellStylePatch.FromDifference(
                            NeraCellStyle.Default,
                            style));
                }
            }
        }

        var sheetData = openXmlWorksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return;
        }
        foreach (var row in sheetData.Elements<Row>())
        {
            if (row.RowIndex?.Value is not uint oneBased ||
                oneBased == 0 ||
                oneBased > SpreadsheetLimits.MaxRows)
            {
                continue;
            }
            var rowIndex = (int)oneBased - 1;
            if (row.Hidden?.Value == true)
            {
                worksheet.Dimensions.SetRowHeight(
                    rowIndex,
                    0d);
            }
            else if (row.Height?.Value is double points)
            {
                worksheet.Dimensions.SetRowHeight(
                    rowIndex,
                    points * PixelsPerPoint);
            }
            if (importAxisStyles &&
                row.StyleIndex?.Value is uint styleIndex)
            {
                var style = styleTable.GetStyle(styleIndex);
                catalog.Intern(style);
                worksheet.ApplyAxisStyle(
                    WorksheetAxis.Row,
                    rowIndex,
                    rowIndex,
                    CellStylePatch.FromDifference(
                        NeraCellStyle.Default,
                        style));
            }
        }
    }

    private static OpenXmlWorksheet BuildWorksheet(
        NeraWorksheet worksheet,
        CellStyleCatalog catalog,
        OpenXmlStyleTable styleTable,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken)
    {
        var result = new OpenXmlWorksheet();
        var axisState = worksheet.CaptureAxisStyleState();
        var columns = BuildColumns(
            worksheet,
            axisState,
            styleTable);
        if (columns.HasChildren)
        {
            result.Append(columns);
        }

        var sheetData = new SheetData();
        var usedByRow = worksheet
            .EnumerateUsedCells()
            .GroupBy(pair => pair.Key.RowIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(pair => pair.Key.ColumnIndex)
                    .ToArray());
        var rowIndexes = usedByRow.Keys
            .Concat(
                worksheet.Dimensions
                    .GetRowOverrides()
                    .Keys)
            .Distinct()
            .OrderBy(index => index);

        foreach (var rowIndex in rowIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Row
            {
                RowIndex = (uint)(rowIndex + 1),
            };
            if (worksheet.Dimensions
                .GetRowOverrides()
                .TryGetValue(
                    rowIndex,
                    out var height))
            {
                if (height <= 0d)
                {
                    row.Hidden = true;
                }
                else
                {
                    row.Height = height / PixelsPerPoint;
                    row.CustomHeight = true;
                }
            }
            var rowOperations = FindAxisOperations(
                axisState.RowSpans,
                rowIndex);
            if (rowOperations.Length > 0)
            {
                row.StyleIndex = styleTable.GetOrAddStyle(
                    ComposeAxisOperations(rowOperations));
                row.CustomFormat = true;
            }
            if (usedByRow.TryGetValue(
                    rowIndex,
                    out var cells))
            {
                foreach (var pair in cells)
                {
                    row.Append(BuildCell(
                        pair.Key,
                        pair.Value,
                        catalog,
                        styleTable,
                        options));
                }
            }
            sheetData.Append(row);
        }

        result.Append(sheetData);
        AppendMergedCells(result, worksheet);
        return result;
    }

    private static void AppendMergedCells(
        OpenXmlWorksheet result,
        NeraWorksheet worksheet)
    {
        if (worksheet.MergedCells.Count == 0)
        {
            return;
        }

        var mergeCells = new MergeCells
        {
            Count = (uint)worksheet.MergedCells.Count,
        };
        foreach (var range in worksheet.MergedCells.Ranges)
        {
            mergeCells.Append(new MergeCell
            {
                Reference = ToA1Range(range),
            });
        }
        result.Append(mergeCells);
    }

    private static Columns BuildColumns(
        NeraWorksheet worksheet,
        WorksheetAxisStyleState axisState,
        OpenXmlStyleTable styleTable)
    {
        var columns = new Columns();
        foreach (var span in axisState.ColumnSpans)
        {
            columns.Append(new Column
            {
                Min = checked((uint)(span.StartIndex + 1)),
                Max = checked((uint)(span.EndIndex + 1)),
                Style = styleTable.GetOrAddStyle(
                    ComposeAxisOperations(span.Operations)),
            });
        }
        foreach (var pair in worksheet.Dimensions
                     .GetColumnOverrides()
                     .OrderBy(pair => pair.Key))
        {
            var oneBased = checked((uint)(pair.Key + 1));
            var operations = FindAxisOperations(
                axisState.ColumnSpans,
                pair.Key);
            var column = new Column
            {
                Min = oneBased,
                Max = oneBased,
                CustomWidth = true,
                Style = operations.Length > 0
                    ? styleTable.GetOrAddStyle(
                        ComposeAxisOperations(operations))
                    : null,
            };
            if (pair.Value <= 0d)
            {
                column.Hidden = true;
                column.Width = 0d;
            }
            else
            {
                column.Width = PixelsToExcelColumnWidth(pair.Value);
            }
            columns.Append(column);
        }
        return columns;
    }

    private static Cell BuildCell(
        CellAddress address,
        CellData data,
        CellStyleCatalog catalog,
        OpenXmlStyleTable styleTable,
        OpenXmlExportOptions options)
    {
        var cell = new Cell
        {
            CellReference = address.ToA1(),
        };
        if (data.StyleId != CellStyleCatalog.DefaultStyleId)
        {
            cell.StyleIndex = styleTable.GetOrAddStyle(
                catalog.Get(data.StyleId));
        }
        if (data.Formula is not null)
        {
            cell.CellFormula = new CellFormula(
                data.Formula.StartsWith('=')
                    ? data.Formula[1..]
                    : data.Formula);
            if (options.WriteCachedFormulaValues)
            {
                ApplyValue(
                    cell,
                    data.Value,
                    isFormulaResult: true);
            }
            return cell;
        }
        ApplyValue(
            cell,
            data.Value,
            isFormulaResult: false);
        return cell;
    }

    private static WorksheetAxisStyleOperation[] FindAxisOperations(
        WorksheetAxisStyleSpan[] spans,
        int index)
    {
        var low = 0;
        var high = spans.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var span = spans[middle];
            if (index < span.StartIndex)
            {
                high = middle - 1;
            }
            else if (index > span.EndIndex)
            {
                low = middle + 1;
            }
            else
            {
                return span.Operations;
            }
        }
        return [];
    }

    private static NeraCellStyle ComposeAxisOperations(
        WorksheetAxisStyleOperation[] operations)
    {
        var style = NeraCellStyle.Default;
        foreach (var operation in operations)
        {
            style = operation.Patch.Apply(style);
        }
        return style;
    }

    private static void ApplyValue(
        Cell cell,
        NeraCellValue value,
        bool isFormulaResult)
    {
        switch (value.Kind)
        {
            case CellValueKind.Blank:
                return;
            case CellValueKind.Number:
                cell.CellValue = new OpenXmlCellValue(
                    ((double)value.RawValue!).ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                if (!isFormulaResult)
                {
                    cell.DataType = CellValues.Number;
                }
                return;
            case CellValueKind.Text:
                if (isFormulaResult)
                {
                    cell.DataType = CellValues.String;
                    cell.CellValue = new OpenXmlCellValue(
                        (string)value.RawValue!);
                }
                else
                {
                    cell.DataType = CellValues.InlineString;
                    cell.InlineString = new InlineString(
                        new Text((string)value.RawValue!)
                        {
                            Space = SpaceProcessingModeValues.Preserve,
                        });
                }
                return;
            case CellValueKind.Boolean:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new OpenXmlCellValue(
                    (bool)value.RawValue!
                        ? "1"
                        : "0");
                return;
            case CellValueKind.DateTime:
                cell.DataType = CellValues.Date;
                cell.CellValue = new OpenXmlCellValue(
                    ((DateTime)value.RawValue!).ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                return;
            case CellValueKind.Error:
                cell.DataType = CellValues.Error;
                cell.CellValue = new OpenXmlCellValue(
                    (string)value.RawValue!);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static bool TryParseCellRange(
        string? reference,
        out CellRange range)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            range = default;
            return false;
        }
        var separatorIndex = reference.IndexOf(':');
        if (separatorIndex <= 0 ||
            separatorIndex >= reference.Length - 1 ||
            !CellAddress.TryParseA1(
                reference[..separatorIndex],
                out var first) ||
            !CellAddress.TryParseA1(
                reference[(separatorIndex + 1)..],
                out var second))
        {
            range = default;
            return false;
        }
        range = new CellRange(first, second);
        return range.RowCount > 1 ||
               range.ColumnCount > 1;
    }

    private static string ToA1Range(CellRange range) =>
        $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";

    private static double ExcelColumnWidthToPixels(double width) =>
        Math.Max(0d, (width * 7d) + 5d);

    private static double PixelsToExcelColumnWidth(double pixels) =>
        Math.Max(0d, (pixels - 5d) / 7d);
}
