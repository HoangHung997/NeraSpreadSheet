using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureDeleteTests
{
    [TestMethod]
    public void DeleteReferencedRowProducesRefErrorWithoutBreakingRecalculation()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.Rename("Sheet1");
        var summary = workbook.AddWorksheet("Summary");
        sheet.SetValue(new CellAddress(4, 0), 12d);
        sheet.SetFormula(default, "=A5");
        summary.SetFormula(default, "=Sheet1!A5");
        var session = new SpreadsheetSession(workbook, sheet);

        session.Structure.DeleteRows(4, 1);

        Assert.AreEqual("=#REF!", sheet.GetFormula(default));
        Assert.AreEqual("=#REF!", summary.GetFormula(default));
        Assert.AreEqual(CellValueKind.Error, sheet.GetCell(default).Value.Kind);
        Assert.AreEqual(CellValueKind.Error, summary.GetCell(default).Value.Kind);
    }

    [TestMethod]
    public void DeletePartialRangeShrinksRangeAndUndoRestoresOriginalFormula()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.Rename("Sheet1");
        var summary = workbook.AddWorksheet("Summary");
        for (var row = 4; row <= 9; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), row + 1d);
        }
        summary.SetFormula(default, "=SUM(Sheet1!A5:A10)");
        var session = new SpreadsheetSession(workbook, sheet);

        session.Structure.DeleteRows(4, 2);

        Assert.AreEqual("=SUM(Sheet1!A5:A8)", summary.GetFormula(default));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("=SUM(Sheet1!A5:A10)", summary.GetFormula(default));
        Assert.AreEqual(5d, sheet.GetValue(new CellAddress(4, 0)));
        Assert.AreEqual(10d, sheet.GetValue(new CellAddress(9, 0)));
    }

    [TestMethod]
    public void DeleteColumnMapsFrozenBoundaryAndSelectionThenUndoRestoresThem()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(3, 4));
        session.View.SetFrozenPanes(2, 4);

        session.Structure.DeleteColumns(1, 2);

        Assert.AreEqual(new CellAddress(3, 2), session.Selection.ActiveCell);
        Assert.AreEqual(2, session.View.FrozenColumns);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(new CellAddress(3, 4), session.Selection.ActiveCell);
        Assert.AreEqual(4, session.View.FrozenColumns);
    }
}
