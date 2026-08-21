using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetTableHostTests
{
    [TestMethod]
    public void HostUsesSpreadsheetSessionAndOpensResponsiveFilterSheet()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ]);
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.AddTable(table);

        using var host = new NeraSpreadsheetTableHost
        {
            Workbook = workbook,
        };

        Assert.IsNotNull(host.Session);
        Assert.AreSame(host.Spreadsheet.Session, host.Session);
        Assert.IsTrue(host.TryOpenFilter(table.Id, statusColumnId));
        Assert.IsTrue(host.IsFilterSheetOpen);

        host.CloseFilterSheet();
        Assert.IsFalse(host.IsFilterSheetOpen);
        Assert.IsFalse(host.TryOpenFilter(table.Id, Guid.NewGuid()));
    }
}
