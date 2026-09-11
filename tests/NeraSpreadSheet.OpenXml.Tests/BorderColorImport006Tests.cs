using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using OpenXmlColor = DocumentFormat.OpenXml.Spreadsheet.Color;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class BorderColorImport006Tests
{
    [TestMethod]
    public async Task BorderAutomaticAndThemeColorsResolveDuringStyleImport()
    {
        var theme = WorkbookTheme.Office with
        {
            Accent1 = new ColorRgba(20, 100, 180),
        };
        var workbook = new NeraWorkbook
        {
            Theme = theme,
        };
        var style = CellStyle.Default with
        {
            Border = new CellBorderStyle
            {
                Top = new CellBorderSide
                {
                    Style = CellBorderLineStyle.Dotted,
                    Color = ColorRgba.Black,
                },
                Bottom = new CellBorderSide
                {
                    Style = CellBorderLineStyle.Thin,
                    Color = theme.Accent1,
                },
            },
        };
        var styleId = workbook.Styles.Intern(style);
        workbook.Worksheets[0].SetCell(
            new CellAddress(0, 0),
            new CellData(CellValue.FromText("Border"), styleId: styleId));

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part is missing.");
            foreach (var customPart in workbookPart.CustomXmlParts
                         .Where(part => part.ContentType.Contains("neraspreadsheet.style-state", StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                workbookPart.DeletePart(customPart);
            }

            var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet
                ?? throw new AssertFailedException("Stylesheet is missing.");
            var importedBorder = stylesheet.Borders?
                .Elements<Border>()
                .LastOrDefault()
                ?? throw new AssertFailedException("Expected a non-default border.");

            importedBorder.TopBorder ??= new TopBorder();
            importedBorder.TopBorder.RemoveAllChildren<OpenXmlColor>();
            importedBorder.TopBorder.Append(new OpenXmlColor { Indexed = 64U });

            importedBorder.BottomBorder ??= new BottomBorder();
            importedBorder.BottomBorder.RemoveAllChildren<OpenXmlColor>();
            importedBorder.BottomBorder.Append(new OpenXmlColor
            {
                Theme = 4U,
                Tint = 0.25d,
            });
            stylesheet.Save();
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var loadedCell = loaded.Worksheets[0].GetCell(new CellAddress(0, 0));
        var loadedStyle = loaded.Styles.Get(loadedCell.StyleId);

        Assert.AreEqual(ColorRgba.Black, loadedStyle.Border.Top.Color,
            "indexed=64 is Excel Automatic and should use the border fallback color.");
        var expectedThemeColor = TableStyleColor
            .FromTheme(WorkbookThemeColor.Accent1, 0.25d)
            .Resolve(theme);
        Assert.AreEqual(expectedThemeColor, loadedStyle.Border.Bottom.Color,
            "Theme border colors must resolve through the workbook theme and tint.");
    }
}
