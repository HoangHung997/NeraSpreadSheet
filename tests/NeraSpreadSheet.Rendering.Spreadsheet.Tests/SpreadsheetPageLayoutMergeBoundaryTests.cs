using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPageLayoutMergeBoundaryTests
{
    [TestMethod]
    public void MergeBeginningOnLaterPageIsKeptTogether()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var column = 0; column < 6; column++)
        {
            worksheet.Dimensions.SetColumnWidth(column, 80d);
        }
        worksheet.MergeCells(new CellRange(
            new CellAddress(0, 2),
            new CellAddress(0, 3)));
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(1.25d, 2d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
        };

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 5)),
            setup);

        var mergePage = plan.Pages.Single(page =>
            page.DataRange.Contains(new CellAddress(0, 2)));
        Assert.AreEqual(2, mergePage.DataRange.Left);
        Assert.AreEqual(3, mergePage.DataRange.Right);
        Assert.IsFalse(plan.Pages.Any(page =>
            page.DataRange.Left == 3));
    }

    [TestMethod]
    public void ManualBreakInsideMergeIsRejectedBeforePlanning()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.MergeCells(new CellRange(
            new CellAddress(0, 1),
            new CellAddress(0, 2)));
        var setup = new SpreadsheetPageSetup
        {
            ManualColumnBreaks = [2],
        };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SpreadsheetPageLayoutPlanner.CreatePlan(
                WorksheetSnapshot.Capture(worksheet),
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(0, 3)),
                setup));
    }

    [TestMethod]
    public void PlanCopiesMutableBreakCollections()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var breaks = new List<int> { 2 };
        var setup = new SpreadsheetPageSetup
        {
            PaperSize = new SpreadsheetPaperSize(4d, 4d),
            Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            ManualColumnBreaks = breaks,
        };
        var area = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 3));

        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            WorksheetSnapshot.Capture(worksheet),
            area,
            setup);
        breaks[0] = 3;

        Assert.AreEqual(2, plan.Setup.ManualColumnBreaks.Single());
    }
}
