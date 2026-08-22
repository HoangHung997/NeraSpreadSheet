using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintDisplayListComposerTests
{
    [TestMethod]
    public void ComposeNestsProductionSheetContentInsidePhysicalPage()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Hello");
        worksheet.Dimensions.SetRowHeight(0, 20d);
        worksheet.Dimensions.SetColumnWidth(0, 80d);
        var plan = CreateSinglePagePlan(
            new SpreadsheetPageSetup
            {
                Margins = new SpreadsheetPageMargins(
                    0.25d,
                    0.25d,
                    0.25d,
                    0.25d,
                    0.1d,
                    0.1d),
                PrintGridlines = true,
                OddHeader = "&A &P/&N",
                OddFooter = "&F",
            });

        var result = SpreadsheetPrintDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            plan,
            pageIndex: 0,
            workbook.Styles,
            new SpreadsheetPrintDisplayListOptions
            {
                WorkbookName = "Book.xlsx",
                Timestamp = new DateTime(2026, 8, 22, 12, 0, 0),
                Culture = CultureInfo.InvariantCulture,
            });

        Assert.AreEqual("Sheet1 1/1", result.HeaderText);
        Assert.AreEqual("Book.xlsx", result.FooterText);
        Assert.IsInstanceOfType<FillRectangleCommand>(
            result.DisplayList.Commands[0]);
        var translation = result.DisplayList.Commands
            .OfType<PushTranslationCommand>()
            .Single();
        Assert.AreEqual(45d, translation.DeltaX, 0.000001d);
        Assert.AreEqual(56d, translation.DeltaY, 0.000001d);
        var nested = result.DisplayList.Commands
            .OfType<DrawDisplayListCommand>()
            .Single()
            .DisplayList;
        Assert.IsTrue(nested.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Hello"));
        CollectionAssert.AreEquivalent(
            new[] { "Sheet1 1/1", "Book.xlsx" },
            result.DisplayList.Commands
                .OfType<DrawTextCommand>()
                .Select(static command => command.Text)
                .ToArray());
    }

    [TestMethod]
    public void DisabledGridlinesRemainTransparentInNestedContent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A");
        var plan = CreateSinglePagePlan(new SpreadsheetPageSetup
        {
            PrintGridlines = false,
        });

        var result = SpreadsheetPrintDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            plan,
            0,
            workbook.Styles);
        var nested = result.DisplayList.Commands
            .OfType<DrawDisplayListCommand>()
            .Single()
            .DisplayList;
        var gridLines = nested.Commands.OfType<DrawLineCommand>().ToArray();

        Assert.IsTrue(gridLines.Length > 0);
        Assert.IsTrue(gridLines.All(static line =>
            line.Color.Alpha == 0));
    }

    [TestMethod]
    public void RepeatedTitlesAndPageDataUseOneSharedCellComposer()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Corner");
        worksheet.SetValue(new CellAddress(0, 1), "Heading");
        worksheet.SetValue(new CellAddress(1, 0), "Label");
        worksheet.SetValue(new CellAddress(1, 1), 42d);
        worksheet.Dimensions.SetRowHeight(0, 20d);
        worksheet.Dimensions.SetRowHeight(1, 20d);
        worksheet.Dimensions.SetColumnWidth(0, 80d);
        worksheet.Dimensions.SetColumnWidth(1, 80d);
        var page = new SpreadsheetPrintPage(
            1,
            0,
            0,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(1, 1)),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 1)),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0)),
            1d,
            new SizeD(400d, 500d),
            new RectD(40d, 50d, 320d, 400d),
            new SizeD(160d, 40d),
            new PointD(0d, 0d));
        var plan = new SpreadsheetPageLayoutPlan(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)),
            new SpreadsheetPageSetup
            {
                PrintGridlines = true,
            },
            1d,
            page.PaperSizeDips,
            page.PrintableBoundsDips,
            [page]);

        var result = SpreadsheetPrintDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            plan,
            0,
            workbook.Styles);
        var texts = result.DisplayList.Commands
            .OfType<DrawDisplayListCommand>()
            .Single()
            .DisplayList.Commands
            .OfType<DrawTextCommand>()
            .Select(static command => command.Text)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "Corner", "Heading", "Label", "42" },
            texts);
    }

    [TestMethod]
    public void PrintedHeadingsAreRejectedUntilPlannerReservesTheirExtent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A");
        var plan = CreateSinglePagePlan(new SpreadsheetPageSetup
        {
            PrintHeadings = true,
        });

        Assert.ThrowsExactly<NotSupportedException>(() =>
            SpreadsheetPrintDisplayListComposer.Compose(
                WorksheetSnapshot.Capture(worksheet),
                plan,
                0,
                workbook.Styles));
    }

    [TestMethod]
    public void InvalidHeaderFooterFontSizeIsRejected()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetPrintDisplayListComposer.Compose(
                WorksheetSnapshot.Capture(worksheet),
                CreateSinglePagePlan(new SpreadsheetPageSetup()),
                0,
                workbook.Styles,
                new SpreadsheetPrintDisplayListOptions
                {
                    HeaderFooterFontSize = 0d,
                }));
    }

    private static SpreadsheetPageLayoutPlan CreateSinglePagePlan(
        SpreadsheetPageSetup setup)
    {
        var paper = new SizeD(400d, 500d);
        var printable = new RectD(40d, 50d, 320d, 400d);
        var page = new SpreadsheetPrintPage(
            1,
            0,
            0,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 0)),
            null,
            null,
            1d,
            paper,
            printable,
            new SizeD(80d, 20d),
            new PointD(5d, 6d));
        return new SpreadsheetPageLayoutPlan(
            page.DataRange,
            setup,
            1d,
            paper,
            printable,
            [page]);
    }
}
