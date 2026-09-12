using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using NeraCellStyle = NeraSpreadSheet.Core.CellStyle;
using NeraCellValue = NeraSpreadSheet.Core.CellValue;
using OpenXmlCell = DocumentFormat.OpenXml.Spreadsheet.Cell;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class ExcelDateSerialRoundTripTests
{
    [TestMethod]
    [DataRow(ExcelDateSystem.Date1900)]
    [DataRow(ExcelDateSystem.Date1904)]
    public async Task LegacyDateTimeExportsAsNumericSerialAndReloadsAsNumber(ExcelDateSystem dateSystem)
    {
        var workbook = new NeraWorkbook { DateSystem = dateSystem };
        var worksheet = workbook.Worksheets[0];
        var date = new DateTime(2026, 8, 21, 12, 30, 0);
        var styleId = workbook.Styles.Intern(NeraCellStyle.Default with
        {
            NumberFormat = new CellNumberFormatStyle { FormatCode = "yyyy-mm-dd hh:mm" },
        });
        worksheet.SetCell(default, new CellData(NeraCellValue.FromDateTime(date), styleId: styleId));

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("Workbook part is required.");
            var cell = workbookPart.WorksheetParts.Single().Worksheet.Descendants<OpenXmlCell>().Single();
            Assert.AreNotEqual(CellValues.Date, cell.DataType?.Value);
            Assert.IsTrue(double.TryParse(cell.CellValue?.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var raw));
            Assert.AreEqual(ExcelDateSerial.ToSerial(date, dateSystem), raw, 1e-9);
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        Assert.AreEqual(dateSystem, loaded.DateSystem);
        var loadedCell = loaded.Worksheets[0].GetCell(default);
        Assert.AreEqual(CellValueKind.Number, loadedCell.Value.Kind);
        var serial = (double)loadedCell.Value.RawValue!;
        Assert.AreEqual(ExcelDateSerial.ToSerial(date, dateSystem), serial, 1e-9);
        Assert.AreEqual("2026-08-21 12:30",
            ExcelCellValueFormatter.Format(loadedCell.Value,
                loaded.Styles.Get(loadedCell.StyleId).NumberFormat.FormatCode,
                loaded.DateSystem,
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task IsoDateCellImportNormalizesToSerialInsteadOfDateTimeKind()
    {
        var workbook = new NeraWorkbook();
        workbook.Worksheets[0].SetValue(default, "placeholder");
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("Workbook part is required.");
            var worksheetPart = workbookPart.WorksheetParts.Single();
            var cell = worksheetPart.Worksheet.Descendants<OpenXmlCell>().Single();
            cell.DataType = CellValues.Date;
            cell.CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue("2026-08-21T00:00:00.0000000");
            workbookPart.Workbook.Save();
            worksheetPart.Worksheet.Save();
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var value = loaded.Worksheets[0].GetCell(default).Value;
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        Assert.AreEqual(ExcelDateSerial.ToSerial(new DateTime(2026, 8, 21)), (double)value.RawValue!, 1e-9);
    }
}
