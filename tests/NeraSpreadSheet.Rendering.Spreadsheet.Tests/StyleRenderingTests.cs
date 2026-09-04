using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class StyleRenderingTests
{
    [TestMethod]
    public void ComposeUsesInternedFillFontAndBorderStyle()
    {
        var workbook = new Workbook();
        var style = CellStyle.Default with
        {
            Font = CellStyle.Default.Font with
            {
                Weight = 700,
                Italic = true,
                Underline = true,
                StrikeThrough = true,
                Color = new ColorRgba(12, 34, 56),
            },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(220, 230, 240) },
            Border = new CellBorderStyle
            {
                Bottom = new CellBorderSide { Style = CellBorderLineStyle.Thick, Color = new ColorRgba(1, 2, 3), Width = 1d },
            },
        };
        var styleId = workbook.Styles.Intern(style);
        workbook.Worksheets[0].SetCell(default, new CellData(CellValue.FromText("Styled"), styleId: styleId));
        var layout = new ViewportLayoutEngine(new SparseAxisMetricIndex(10, 20d), new SparseAxisMetricIndex(10, 80d))
            .Compute(new ViewportRequest(0d, 0d, new SizeD(200d, 100d), 0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(workbook.Worksheets[0]),
            layout,
            styles: workbook.Styles);

        Assert.IsTrue(displayList.Commands.OfType<FillRectangleCommand>().Any(command => command.Color == style.Fill.Color));
        Assert.IsTrue(displayList.Commands.OfType<DrawTextCommand>().Any(command =>
            command.Text == "Styled" &&
            command.Style.FontWeight == 700 &&
            command.Style.Italic &&
            command.Style.Underline &&
            command.Style.Strikethrough &&
            command.Style.Color == style.Font.Color));
        Assert.IsTrue(displayList.Commands.OfType<DrawLineCommand>().Any(command => command.Color == style.Border.Bottom.Color && command.StrokeWidth >= 2d));
    }

    [TestMethod]
    public void ComposeFormatsExcelDateSerialBeforeCreatingTextCommand()
    {
        var workbook = new Workbook();
        var style = CellStyle.Default with
        {
            NumberFormat = new CellNumberFormatStyle { FormatCode = "m/d/yyyy" },
        };
        var styleId = workbook.Styles.Intern(style);
        workbook.Worksheets[0].SetCell(
            default,
            new CellData(CellValue.FromNumber(45_751d), styleId: styleId));
        var layout = new ViewportLayoutEngine(
                new SparseAxisMetricIndex(10, 20d),
                new SparseAxisMetricIndex(10, 80d))
            .Compute(new ViewportRequest(0d, 0d, new SizeD(200d, 100d), 0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(workbook.Worksheets[0]),
            layout,
            styles: workbook.Styles);

        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(static command => command.Text != "m/d/yyyy" && command.Text.Contains("2025", StringComparison.Ordinal)));
    }
}
