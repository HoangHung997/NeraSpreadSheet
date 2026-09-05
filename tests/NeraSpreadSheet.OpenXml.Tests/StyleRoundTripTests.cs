using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraCellStyle = NeraSpreadSheet.Core.CellStyle;
using NeraCellValue = NeraSpreadSheet.Core.CellValue;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using OpenXmlCell = DocumentFormat.OpenXml.Spreadsheet.Cell;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class StyleRoundTripTests
{
    [TestMethod]
    public async Task DirectCellStyleWritesStandardStyleIndexAndRoundTripsExactNeraStyle()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        var border = new CellBorderSide
        {
            Style = CellBorderLineStyle.DoubleLine,
            Color = new ColorRgba(25, 90, 160),
            Width = 2.25d,
        };
        var style = new NeraCellStyle
        {
            Font = new CellFontStyle
            {
                Family = "Segoe UI",
                Size = 15.5d,
                Weight = 550,
                Italic = true,
                Underline = true,
                DoubleUnderline = true,
                StrikeThrough = true,
                Outline = true,
                Shadow = true,
                VerticalAlignment = CellFontVerticalAlignment.Superscript,
                Color = new ColorRgba(150, 20, 70),
            },
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Color = new ColorRgba(230, 240, 200),
                BackgroundColor = new ColorRgba(240, 225, 210),
                Pattern = CellFillPattern.LightTrellis,
            },
            Border = new CellBorderStyle
            {
                Left = border,
                Top = border,
                Right = border,
                Bottom = border,
                Diagonal = border,
                DiagonalUp = true,
                DiagonalDown = true,
            },
            Alignment = new CellAlignmentStyle
            {
                Horizontal = CellHorizontalAlignment.Center,
                Vertical = CellVerticalAlignment.Top,
                WrapText = true,
                ShrinkToFit = true,
                JustifyLastLine = true,
                Indent = 2,
                RelativeIndent = 1,
                ReadingOrder = CellReadingOrder.LeftToRight,
                TextRotationDegrees = -35,
            },
            Protection = new CellProtectionStyle
            {
                Locked = false,
                FormulaHidden = true,
            },
            NumberFormat = new CellNumberFormatStyle
            {
                FormatCode = "#,##0.000_);[Red](#,##0.000)",
            },
        };
        var styleId = workbook.Styles.Intern(style);
        worksheet.SetCell(
            new CellAddress(2, 3),
            new CellData(NeraCellValue.FromNumber(1234.5d), styleId: styleId));

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            AssertSchemaValid(document);
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part was not written.");
            Assert.IsNotNull(workbookPart.WorkbookStylesPart?.Stylesheet);
            var openXmlWorksheet = workbookPart.WorksheetParts
                .Single()
                .Worksheet
                ?? throw new AssertFailedException("Worksheet markup was not written.");
            var cell = openXmlWorksheet
                .Descendants<OpenXmlCell>()
                .Single(candidate => candidate.CellReference?.Value == "D3");
            Assert.IsTrue((cell.StyleIndex?.Value ?? 0U) > 0U);
            Assert.IsTrue(workbookPart.CustomXmlParts.Any(part =>
                part.ContentType.Contains("neraspreadsheet.style-state", StringComparison.OrdinalIgnoreCase)));
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var loadedCell = loaded.Worksheets[0].GetCell(new CellAddress(2, 3));
        Assert.AreEqual(style, loaded.Styles.Get(loadedCell.StyleId));
    }

    [TestMethod]
    public async Task WorkbookDateSystemShouldRoundTripThroughStandardWorkbookProperties()
    {
        var workbook = new NeraWorkbook
        {
            DateSystem = ExcelDateSystem.Date1904,
        };
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            Assert.IsTrue(document.WorkbookPart?.Workbook?.WorkbookProperties?.Date1904?.Value);
            AssertSchemaValid(document);
        }
        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        Assert.AreEqual(ExcelDateSystem.Date1904, loaded.DateSystem);
    }

    [TestMethod]
    public async Task HugeSparseRowAndColumnStylesRoundTripWithoutMaterializingBlankCells()
    {
        var workbook = new NeraWorkbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "anchor");

        session.Selection.Select(new CellRange(
            new CellAddress(10, 0),
            new CellAddress(900_000, SpreadsheetLimits.MaxColumns - 1)));
        session.Styles.SetFontColor(new ColorRgba(180, 30, 45));
        session.Styles.ToggleBold();

        session.Selection.SelectColumn(3);
        session.Styles.SetFontColor(new ColorRgba(20, 80, 210));
        session.Styles.SetFill(new ColorRgba(220, 235, 250));
        session.Styles.SetNumberFormat("0.0000");

        session.Selection.Select(new CellRange(
            new CellAddress(10, 0),
            new CellAddress(900_000, SpreadsheetLimits.MaxColumns - 1)));
        session.Styles.SetFontColor(new ColorRgba(30, 150, 75));

        Assert.AreEqual(1, worksheet.UsedCellCount);
        Assert.IsTrue(worksheet.RowStyleSpanCount > 0);
        Assert.IsTrue(worksheet.ColumnStyleSpanCount > 0);
        var expectedIntersection = worksheet.GetEffectiveStyle(
            new CellAddress(500_000, 3),
            workbook.Styles);
        var expectedColumnOnly = worksheet.GetEffectiveStyle(
            new CellAddress(5, 3),
            workbook.Styles);

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        Assert.IsTrue(stream.Length < 1_000_000L, $"Sparse style XLSX unexpectedly grew to {stream.Length} bytes.");
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            AssertSchemaValid(document);
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part was not written.");
            var worksheetPart = workbookPart.WorksheetParts.Single();
            var openXmlWorksheet = worksheetPart.Worksheet
                ?? throw new AssertFailedException("Worksheet markup was not written.");
            Assert.IsTrue(openXmlWorksheet.Elements<Columns>()
                .SelectMany(static columns => columns.Elements<Column>())
                .Any(column => column.Style?.Value is > 0U));
            var sheetData = openXmlWorksheet.GetFirstChild<SheetData>()
                ?? throw new AssertFailedException("SheetData was not written.");
            Assert.IsTrue(sheetData.Elements<Row>().Count() < 20);
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var loadedSheet = loaded.Worksheets[0];
        Assert.AreEqual(1, loadedSheet.UsedCellCount);
        Assert.AreEqual(worksheet.RowStyleSpanCount, loadedSheet.RowStyleSpanCount);
        Assert.AreEqual(worksheet.ColumnStyleSpanCount, loadedSheet.ColumnStyleSpanCount);
        Assert.AreEqual(
            expectedIntersection,
            loadedSheet.GetEffectiveStyle(new CellAddress(500_000, 3), loaded.Styles));
        Assert.AreEqual(
            expectedColumnOnly,
            loadedSheet.GetEffectiveStyle(new CellAddress(5, 3), loaded.Styles));
    }

    private static void AssertSchemaValid(SpreadsheetDocument document)
    {
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(Environment.NewLine, errors.Select(static error => error.Description)));
    }
}
