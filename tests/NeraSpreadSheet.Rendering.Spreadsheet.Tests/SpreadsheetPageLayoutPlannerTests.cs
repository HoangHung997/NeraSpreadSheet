using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPageLayoutPlannerTests
{
    [TestMethod]
    public void SmallAreaProducesOneA4PortraitPage()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 2);

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)));

        Assert.AreEqual(1, plan.Pages.Count);
        Assert.AreEqual(1, plan.HorizontalPageCount);
        Assert.AreEqual(1, plan.VerticalPageCount);
        Assert.AreEqual(1d, plan.EffectiveScale, 0.000001d);
        Assert.AreEqual(
            SpreadsheetPageLayoutPlanner.DipsPerInch *
            SpreadsheetPaperSize.A4.WidthInches,
            plan.PaperSizeDips.Width,
            0.000001d);
    }

    [TestMethod]
    public void NaturalScalePaginatesAcrossColumns()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 4);
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(1.5d, 1.5d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 3)),
            setup);

        Assert.AreEqual(4, plan.HorizontalPageCount);
        Assert.AreEqual(1, plan.VerticalPageCount);
        Assert.AreEqual(4, plan.Pages.Count);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            plan.Pages
                .Select(static page => page.DataRange.Left)
                .ToArray());
    }

    [TestMethod]
    public void FitToOnePageWideCalculatesScaleBeforePagination()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 4);
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(1.5d, 1.5d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            FitToPagesWide = 1,
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 3)),
            setup);

        Assert.AreEqual(1, plan.HorizontalPageCount);
        Assert.AreEqual(0.45d, plan.EffectiveScale, 0.000001d);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 3)),
            plan.Pages.Single().DataRange);
    }

    [TestMethod]
    public void RepeatedColumnsAreReservedOnEveryHorizontalPage()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 3);
        var printArea = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(1, 2));
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(1.5d, 1.5d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            RepeatTitles = new SpreadsheetRepeatTitles(
                columns: new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 0))),
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            printArea,
            setup);

        Assert.AreEqual(2, plan.HorizontalPageCount);
        Assert.IsTrue(plan.Pages.All(page =>
            page.RepeatedColumns == new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0))));
        CollectionAssert.AreEqual(
            new[] { 1, 2 },
            plan.Pages
                .Select(static page => page.DataRange.Left)
                .ToArray());
    }

    [TestMethod]
    public void ManualColumnBreakStartsANewPage()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 4);
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(4d, 4d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            ManualColumnBreaks = [2],
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 3)),
            setup);

        Assert.AreEqual(2, plan.HorizontalPageCount);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)),
            plan.Pages[0].DataRange);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 2),
                new CellAddress(1, 3)),
            plan.Pages[1].DataRange);
    }

    [TestMethod]
    public void FirstPageBreakDoesNotSplitMergedCells()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 3);
        worksheet.MergeCells(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 1)));
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(1.25d, 2d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 2)),
            setup);

        Assert.AreEqual(2, plan.HorizontalPageCount);
        Assert.AreEqual(1, plan.Pages[0].DataRange.Right);
        Assert.AreEqual(2, plan.Pages[1].DataRange.Left);
    }

    [TestMethod]
    public void LandscapeSwapsPaperDimensions()
    {
        var worksheet = CreateWorksheet(rows: 1, columns: 1);
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = SpreadsheetPaperSize.Letter,
            Orientation = SpreadsheetPageOrientation.Landscape,
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 0)),
            setup);

        Assert.AreEqual(
            11d * SpreadsheetPageLayoutPlanner.DipsPerInch,
            plan.PaperSizeDips.Width,
            0.000001d);
        Assert.AreEqual(
            8.5d * SpreadsheetPageLayoutPlanner.DipsPerInch,
            plan.PaperSizeDips.Height,
            0.000001d);
    }

    [TestMethod]
    public void InvalidFitAndBreakSettingsAreRejected()
    {
        var worksheet = CreateWorksheet(rows: 2, columns: 2);
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var area = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(1, 1));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetPageLayoutPlanner.CreatePlan(
                snapshot,
                area,
                new SpreadsheetPageSetup
                {
                    FitToPagesWide = 0,
                }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            SpreadsheetPageLayoutPlanner.CreatePlan(
                snapshot,
                area,
                new SpreadsheetPageSetup
                {
                    ManualColumnBreaks = [0],
                }));
    }

    private static Worksheet CreateWorksheet(int rows, int columns)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < rows; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 20d);
            for (var column = 0; column < columns; column++)
            {
                if (row == 0)
                {
                    worksheet.Dimensions.SetColumnWidth(column, 80d);
                }
                worksheet.SetValue(
                    new CellAddress(row, column),
                    $"R{row}C{column}");
            }
        }
        return worksheet;
    }
}
