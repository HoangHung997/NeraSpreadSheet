using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureFailureTests
{
    [TestMethod]
    public void InsertOverflowLeavesWorkbookSelectionAndFreezeUnchanged()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(SpreadsheetLimits.MaxRows - 1, 0), "edge");
        sheet.SetFormula(default, "=A1");
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(3, 2));
        session.View.SetFrozenPanes(2, 1);
        var beforeSelection = session.Selection.Capture();
        var beforeFormula = sheet.GetFormula(default);
        var beforeVersion = sheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Structure.InsertRows(SpreadsheetLimits.MaxRows - 2, 1));

        Assert.AreEqual("edge", sheet.GetValue(new CellAddress(SpreadsheetLimits.MaxRows - 1, 0)));
        Assert.AreEqual(beforeFormula, sheet.GetFormula(default));
        Assert.AreEqual(beforeVersion, sheet.Version);
        Assert.AreEqual(beforeSelection.ActiveCell, session.Selection.ActiveCell);
        CollectionAssert.AreEqual(beforeSelection.Ranges.ToArray(), session.Selection.Ranges.ToArray());
        Assert.AreEqual(2, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
        Assert.IsFalse(session.Undo());
    }
}
