using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class FilteredRowProjectionTests
{
    [TestMethod]
    public void ProjectionCompressesAdjacentFilteredRowsWithoutMaterializingCells()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(1, 1), "Open");
        worksheet.SetValue(new CellAddress(2, 1), "Closed");
        worksheet.SetValue(new CellAddress(3, 1), "Closed");
        worksheet.SetValue(new CellAddress(4, 1), "Open");
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(statusColumnId, "Status"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ])));
        var usedCellCount = worksheet.UsedCellCount;

        var spans = WorksheetSnapshot.Capture(worksheet)
            .GetFilteredOutRowSpans();

        Assert.AreEqual(1, spans.Count);
        Assert.AreEqual(new FilteredRowSpan(2, 3), spans[0]);
        Assert.AreEqual(usedCellCount, worksheet.UsedCellCount);
    }

    [TestMethod]
    public void ProjectionRejectsWorkBeyondConfiguredSafetyLimit()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(10, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(statusColumnId, "Status"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ])));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            snapshot.GetFilteredOutRowSpans(maximumRowsToEvaluate: 5));
    }
}
