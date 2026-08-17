using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetViewControllerTests
{
    [TestMethod]
    public void FreezeStateIsStoredPerWorksheet()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);

        Assert.IsTrue(session.View.SetFrozenPanes(2, 1));
        Assert.AreEqual(2, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);

        session.ActivateWorksheet(second);
        Assert.AreEqual(0, session.View.FrozenRows);
        Assert.AreEqual(0, session.View.FrozenColumns);
        Assert.IsTrue(session.View.SetFrozenPanes(1, 3));

        session.ActivateWorksheet(first);
        Assert.AreEqual(2, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
    }

    [TestMethod]
    public void FreezeAtActiveCellUsesRowsAboveAndColumnsToLeft()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.SetActiveCell(new CellAddress(4, 3));

        Assert.IsTrue(session.View.FreezeAtActiveCell());

        Assert.AreEqual(4, session.View.FrozenRows);
        Assert.AreEqual(3, session.View.FrozenColumns);
    }

    [TestMethod]
    public void FreezeBoundaryCannotSplitMergedRange()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(new CellAddress(0, 0), new CellAddress(2, 2)));
        var session = new SpreadsheetSession(workbook);

        Assert.ThrowsExactly<InvalidOperationException>(() => session.View.SetFrozenPanes(1, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.View.SetFrozenPanes(0, 1));
    }

    [TestMethod]
    public async Task ViewCommandsFreezeAndUnfreezeCurrentSheet()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.SetActiveCell(new CellAddress(2, 2));

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetViewCommandIds.FreezePanes));
        Assert.IsTrue(session.View.HasFrozenPanes);
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetViewCommandIds.UnfreezePanes));
        Assert.IsFalse(session.View.HasFrozenPanes);
    }
}
