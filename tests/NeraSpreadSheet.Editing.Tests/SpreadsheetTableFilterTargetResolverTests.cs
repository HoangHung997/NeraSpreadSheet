using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterTargetResolverTests
{
    [TestMethod]
    public void ActiveCellResolvesHeaderDataAndTotalsColumns()
    {
        var context = CreateContext();

        context.Session.Selection.SetActiveCell(new CellAddress(0, 1));
        Assert.IsTrue(context.Session.TryResolveActiveTableFilterTarget(
            out var headerTarget));
        Assert.AreEqual(context.AmountColumnId, headerTarget.ColumnId);
        Assert.AreEqual("Amount", headerTarget.ColumnName);
        Assert.AreEqual(1, headerTarget.WorksheetColumnIndex);

        context.Session.Selection.SetActiveCell(new CellAddress(2, 0));
        Assert.IsTrue(context.Session.TryResolveActiveTableFilterTarget(
            out var dataTarget));
        Assert.AreEqual(context.StatusColumnId, dataTarget.ColumnId);

        context.Session.Selection.SetActiveCell(new CellAddress(4, 1));
        Assert.IsTrue(context.Session.TryResolveActiveTableFilterTarget(
            out var totalsTarget));
        Assert.AreEqual(context.AmountColumnId, totalsTarget.ColumnId);
    }

    [TestMethod]
    public void AddressOutsideTableDoesNotResolveFilterTarget()
    {
        var context = CreateContext();

        Assert.IsFalse(context.Session.TryResolveTableFilterTarget(
            new CellAddress(7, 4),
            out _));
    }

    private static TestContext CreateContext()
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
                new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ],
            hasTotalsRow: true);
        worksheet.AddTable(table);
        return new TestContext(
            statusColumnId,
            amountColumnId,
            new SpreadsheetSession(workbook));
    }

    private sealed record TestContext(
        Guid StatusColumnId,
        Guid AmountColumnId,
        SpreadsheetSession Session);
}
