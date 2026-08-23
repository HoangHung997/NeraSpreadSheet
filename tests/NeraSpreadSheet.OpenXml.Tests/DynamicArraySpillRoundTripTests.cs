using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Editing;
using NeraCellStyle = NeraSpreadSheet.Core.CellStyle;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using CellAddress = NeraSpreadSheet.Core.CellAddress;
using CellAlignmentStyle = NeraSpreadSheet.Core.CellAlignmentStyle;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class DynamicArraySpillRoundTripTests
{
    [TestMethod]
    public async Task DocumentSaveOmitsDerivedChildrenAndPreservesChildStyle()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(0, 0);
        var styledChild = new CellAddress(1, 1);
        var styleId = workbook.Styles.Intern(new NeraCellStyle
        {
            Alignment = new CellAlignmentStyle
            {
                WrapText = true,
            },
        });
        worksheet.SetStyle(styledChild, styleId);
        session.SetFormula(owner, "=SEQUENCE(2,2)");
        Assert.AreEqual(4d, worksheet.GetValue(styledChild));
        await using var stream = new MemoryStream();
        var serializer = new NeraOpenXmlDocumentSerializer();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!
            .WorksheetParts
            .Single();
        var cells = worksheetPart.Worksheet!
            .Descendants<Cell>()
            .ToDictionary(
                static cell => cell.CellReference!.Value!,
                StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(cells.TryGetValue("A1", out var ownerCell));
        Assert.IsNotNull(ownerCell!.CellFormula);
        Assert.AreEqual("SEQUENCE(2,2)", ownerCell.CellFormula!.Text);
        Assert.IsFalse(cells.ContainsKey("B1"));
        Assert.IsFalse(cells.ContainsKey("A2"));
        Assert.IsTrue(cells.TryGetValue("B2", out var styleOnly));
        Assert.IsNotNull(styleOnly!.StyleIndex);
        Assert.IsNull(styleOnly.CellValue);
        Assert.IsNull(styleOnly.InlineString);
        Assert.IsNull(styleOnly.DataType);
        Assert.IsNull(styleOnly.CellFormula);

        var errors = new OpenXmlValidator()
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(static error => error.Description)));
    }

    [TestMethod]
    public async Task LoadThenRecalculateRematerializesTheSpill()
    {
        var workbook = new NeraWorkbook();
        var sourceSession = new SpreadsheetSession(workbook);
        var owner = new CellAddress(0, 0);
        sourceSession.SetFormula(owner, "=SEQUENCE(2,2,10,1)");
        await using var stream = new MemoryStream();
        var serializer = new NeraOpenXmlDocumentSerializer();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;

        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var worksheet = loaded.Worksheets[0];
        Assert.AreEqual("=SEQUENCE(2,2,10,1)", worksheet.GetFormula(owner));
        Assert.IsNull(worksheet.GetValue(new CellAddress(0, 1)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 1)));

        var loadedSession = new SpreadsheetSession(loaded);
        loadedSession.Recalculate();

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(10d, worksheet.GetValue(owner));
        Assert.AreEqual(11d, worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(12d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(13d, worksheet.GetValue(new CellAddress(1, 1)));
    }
}
