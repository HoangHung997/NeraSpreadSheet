using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SortControllerTests
{
    [TestMethod]
    public void SortAscendingMovesEntireRowsAndPreservesStyles()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var boldStyle = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
        });
        sheet.SetValue(new CellAddress(0, 0), 3d);
        sheet.SetValue(new CellAddress(0, 1), "C");
        sheet.SetValue(new CellAddress(1, 0), 1d);
        sheet.SetValue(new CellAddress(1, 1), "A");
        sheet.SetStyle(new CellAddress(1, 1), boldStyle);
        sheet.SetValue(new CellAddress(2, 0), 2d);
        sheet.SetValue(new CellAddress(2, 1), "B");
        var session = new SpreadsheetSession(workbook);
        var range = new CellRange(default, new CellAddress(2, 1));
        session.Selection.Select(range);

        session.Sort.Sort(range, keyColumnOffset: 0, ascending: true);

        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(0, 0)).Value.RawValue);
        Assert.AreEqual("A", sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.AreEqual(boldStyle, sheet.GetCell(new CellAddress(0, 1)).StyleId);
        Assert.AreEqual(2d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual(3d, sheet.GetCell(new CellAddress(2, 0)).Value.RawValue);
    }

    [TestMethod]
    public void SortIsUndoable()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, 2d);
        sheet.SetValue(new CellAddress(1, 0), 1d);
        var session = new SpreadsheetSession(workbook);
        var range = new CellRange(default, new CellAddress(1, 0));

        session.Sort.Sort(range, 0, ascending: true);
        Assert.AreEqual(1d, sheet.GetCell(default).Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2d, sheet.GetCell(default).Value.RawValue);
        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
    }

    [TestMethod]
    public async Task SortCommandsUseActiveColumnAsKey()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "r1");
        sheet.SetValue(new CellAddress(0, 1), 9d);
        sheet.SetValue(new CellAddress(1, 0), "r2");
        sheet.SetValue(new CellAddress(1, 1), 1d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));
        session.Selection.SetActiveCell(new CellAddress(0, 1), preserveAnchor: true);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetSortCommandIds.SortAscending));

        Assert.AreEqual("r2", sheet.GetCell(default).Value.RawValue);
        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
    }

    [TestMethod]
    public void SortRejectsMergedRange()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(default, new CellAddress(0, 1)));
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));

        Assert.IsFalse(session.Sort.CanSortPrimarySelection);
    }
}
