using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
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

    public OpenXmlSerializerCapabilities Capabilities { get; } = new(
        ReadsBasicCells: true,
        WritesBasicCells: true,
        ReadsFormulas: true,
        WritesFormulas: true,
        ReadsBasicDimensions: true,
        WritesBasicDimensions: true,
        PreservesUnknownParts: false,
        ReadsMergedCells: true,
        WritesMergedCells: true);

    public Task<NeraWorkbook> LoadAsync(
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
        if (options.PreserveUnknownParts)
        {
            throw new NotSupportedException("Unknown-part preservation is not implemented yet. Set PreserveUnknownParts to false.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var document = SpreadsheetDocument.Open(source, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
        var openXmlWorkbook = workbookPart.Workbook
            ?? throw new InvalidDataException("The XLSX workbook part does not contain workbook markup.");
        var sheets = openXmlWorkbook.GetFirstChild<Sheets>()
            ?? throw new InvalidDataException("The XLSX workbook does not contain a sheets collection.");

        var workbook = new NeraWorkbook(createDefaultWorksheet: false);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        foreach (var sheet in sheets.Elements<Sheet>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheet.Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                continue;
            }
            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(sheet.Name?.Value)
                ? $"Sheet{workbook.Worksheets.Count + 1}"
                : sheet.Name!.Value!;
            var worksheet = workbook.AddWorksheet(name);
            ImportDimensions(worksheetPart, worksheet);
            ImportCells(worksheetPart, worksheet, sharedStrings, options, cancellationToken);
            ImportMergedCells(worksheetPart, worksheet, cancellationToken);
        }

        if (workbook.Worksheets.Count == 0)
        {
            workbook.AddWorksheet("Sheet1");
        }
        return Task.FromResult(workbook);
    }

    public Task SaveAsync(
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
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }
        if (options.PreserveUnknownParts)
        {
            throw new NotSupportedException("Unknown-part preservation is not implemented yet. Set PreserveUnknownParts to false.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var document = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook, true);
        var workbookPart = document.AddWorkbookPart();
        var openXmlWorkbook = new OpenXmlWorkbook();
        workbookPart.Workbook = openXmlWorkbook;
        var sheets = openXmlWorkbook.AppendChild(new Sheets());
        uint sheetId = 1;

        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = BuildWorksheet(worksheet, options, cancellationToken);
            worksheetPart.Worksheet.Save();
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = worksheet.Name,
            });
        }

        openXmlWorkbook.Save();
        return Task.CompletedTask;
    }

    private static void ImportCells(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        SharedStringTable? sharedStrings,
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

        var changes = new List<KeyValuePair<CellAddress, CellData>>();
        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var cell in row.Elements<Cell>())
            {
                var reference = cell.CellReference?.Value;
                if (string.IsNullOrWhiteSpace(reference) || !CellAddress.TryParseA1(reference, out var address))
                {
                    continue;
                }

                var formulaText = cell.CellFormula?.Text;
                var formula = string.IsNullOrWhiteSpace(formulaText) ? null : $"={formulaText}";
                var value = formula is not null && !options.LoadCachedFormulaValues
                    ? NeraCellValue.Blank
                    : ReadValue(cell, sharedStrings);
                var data = new CellData(value, formula);
                if (!data.IsEmpty)
                {
                    changes.Add(new KeyValuePair<CellAddress, CellData>(address, data));
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

        foreach (var mergeCells in openXmlWorksheet.Elements<MergeCells>())
        {
            foreach (var mergeCell in mergeCells.Elements<MergeCell>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryParseCellRange(mergeCell.Reference?.Value, out var range))
                {
                    worksheet.MergeCells(range, clearNonTopLeftCells: false);
                }
            }
        }
    }

    private static NeraCellValue ReadValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var dataType = cell.DataType?.Value;
        var raw = cell.CellValue?.Text;
        if (dataType == CellValues.InlineString)
        {
            return NeraCellValue.FromText(cell.InlineString?.InnerText);
        }
        if (dataType == CellValues.SharedString)
        {
            if (sharedStrings is null || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
            {
                return NeraCellValue.Blank;
            }
            var item = sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(index);
            return NeraCellValue.FromText(item?.InnerText);
        }
        if (dataType == CellValues.Boolean)
        {
            return NeraCellValue.FromBoolean(raw is "1" or "true" or "TRUE");
        }
        if (dataType == CellValues.Error)
        {
            return string.IsNullOrWhiteSpace(raw) ? NeraCellValue.FromError("#VALUE!") : NeraCellValue.FromError(raw);
        }
        if (dataType == CellValues.Date)
        {
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)
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
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? NeraCellValue.FromNumber(number)
            : NeraCellValue.FromText(raw);
    }

    private static void ImportDimensions(WorksheetPart worksheetPart, NeraWorksheet worksheet)
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
                if (minimum is null || maximum is null || minimum == 0 || maximum < minimum)
                {
                    continue;
                }

                var size = column.Hidden?.Value == true
                    ? 0d
                    : column.Width?.Value is double width
                        ? ExcelColumnWidthToPixels(width)
                        : worksheet.Dimensions.DefaultColumnWidth;
                var last = Math.Min((int)maximum.Value, SpreadsheetLimits.MaxColumns);
                for (var oneBased = Math.Max(1, (int)minimum.Value); oneBased <= last; oneBased++)
                {
                    worksheet.Dimensions.SetColumnWidth(oneBased - 1, size);
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
            if (row.RowIndex?.Value is not uint oneBased || oneBased == 0 || oneBased > SpreadsheetLimits.MaxRows)
            {
                continue;
            }
            if (row.Hidden?.Value == true)
            {
                worksheet.Dimensions.SetRowHeight((int)oneBased - 1, 0d);
            }
            else if (row.Height?.Value is double points)
            {
                worksheet.Dimensions.SetRowHeight((int)oneBased - 1, points * PixelsPerPoint);
            }
        }
    }

    private static OpenXmlWorksheet BuildWorksheet(
        NeraWorksheet worksheet,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken)
    {
        var result = new OpenXmlWorksheet();
        var columns = BuildColumns(worksheet);
        if (columns.HasChildren)
        {
            result.Append(columns);
        }

        var sheetData = new SheetData();
        var usedByRow = worksheet.EnumerateUsedCells()
            .GroupBy(pair => pair.Key.RowIndex)
            .ToDictionary(group => group.Key, group => group.OrderBy(pair => pair.Key.ColumnIndex).ToArray());
        var rowIndexes = usedByRow.Keys
            .Concat(worksheet.Dimensions.GetRowOverrides().Keys)
            .Distinct()
            .OrderBy(index => index);

        foreach (var rowIndex in rowIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Row { RowIndex = (uint)(rowIndex + 1) };
            if (worksheet.Dimensions.GetRowOverrides().TryGetValue(rowIndex, out var height))
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
            if (usedByRow.TryGetValue(rowIndex, out var cells))
            {
                foreach (var pair in cells)
                {
                    row.Append(BuildCell(pair.Key, pair.Value, options));
                }
            }
            sheetData.Append(row);
        }

        result.Append(sheetData);
        AppendMergedCells(result, worksheet);
        return result;
    }

    private static void AppendMergedCells(OpenXmlWorksheet result, NeraWorksheet worksheet)
    {
        if (worksheet.MergedCells.Count == 0)
        {
            return;
        }

        var mergeCells = new MergeCells { Count = (uint)worksheet.MergedCells.Count };
        foreach (var range in worksheet.MergedCells.Ranges)
        {
            mergeCells.Append(new MergeCell { Reference = ToA1Range(range) });
        }
        result.Append(mergeCells);
    }

    private static Columns BuildColumns(NeraWorksheet worksheet)
    {
        var columns = new Columns();
        foreach (var pair in worksheet.Dimensions.GetColumnOverrides().OrderBy(pair => pair.Key))
        {
            var oneBased = (uint)(pair.Key + 1);
            var column = new Column { Min = oneBased, Max = oneBased, CustomWidth = true };
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

    private static Cell BuildCell(CellAddress address, CellData data, OpenXmlExportOptions options)
    {
        var cell = new Cell { CellReference = address.ToA1() };
        if (data.Formula is not null)
        {
            cell.CellFormula = new CellFormula(data.Formula.StartsWith('=') ? data.Formula[1..] : data.Formula);
            if (options.WriteCachedFormulaValues)
            {
                ApplyValue(cell, data.Value, isFormulaResult: true);
            }
            return cell;
        }
        ApplyValue(cell, data.Value, isFormulaResult: false);
        return cell;
    }

    private static void ApplyValue(Cell cell, NeraCellValue value, bool isFormulaResult)
    {
        switch (value.Kind)
        {
            case CellValueKind.Blank:
                return;
            case CellValueKind.Number:
                cell.CellValue = new OpenXmlCellValue(((double)value.RawValue!).ToString("R", CultureInfo.InvariantCulture));
                if (!isFormulaResult) cell.DataType = CellValues.Number;
                return;
            case CellValueKind.Text:
                if (isFormulaResult)
                {
                    cell.DataType = CellValues.String;
                    cell.CellValue = new OpenXmlCellValue((string)value.RawValue!);
                }
                else
                {
                    cell.DataType = CellValues.InlineString;
                    cell.InlineString = new InlineString(new Text((string)value.RawValue!) { Space = SpaceProcessingModeValues.Preserve });
                }
                return;
            case CellValueKind.Boolean:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new OpenXmlCellValue((bool)value.RawValue! ? "1" : "0");
                return;
            case CellValueKind.DateTime:
                cell.DataType = CellValues.Date;
                cell.CellValue = new OpenXmlCellValue(((DateTime)value.RawValue!).ToString("O", CultureInfo.InvariantCulture));
                return;
            case CellValueKind.Error:
                cell.DataType = CellValues.Error;
                cell.CellValue = new OpenXmlCellValue((string)value.RawValue!);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static bool TryParseCellRange(string? reference, out CellRange range)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            range = default;
            return false;
        }

        var separatorIndex = reference.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= reference.Length - 1 ||
            !CellAddress.TryParseA1(reference[..separatorIndex], out var first) ||
            !CellAddress.TryParseA1(reference[(separatorIndex + 1)..], out var second))
        {
            range = default;
            return false;
        }

        range = new CellRange(first, second);
        return range.RowCount > 1 || range.ColumnCount > 1;
    }

    private static string ToA1Range(CellRange range) => $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";

    private static double ExcelColumnWidthToPixels(double width) => Math.Max(0d, (width * 7d) + 5d);

    private static double PixelsToExcelColumnWidth(double pixels) => Math.Max(0d, (pixels - 5d) / 7d);
}
