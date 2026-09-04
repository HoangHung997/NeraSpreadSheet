using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetWorksheetAutoFilterControllerTests
{
    [TestMethod]
    public void ApplyValueFilterUsesProductionUndoRedo()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.WorksheetFilter.SetRange(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1)));

        session.WorksheetFilter.ApplyValueFilter(
            worksheetColumnIndex: 0,
            [CellValue.FromText("Open")]);

        Assert.IsFalse(WorksheetSnapshot.Capture(worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(WorksheetSnapshot.Capture(worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(session.Redo());
        Assert.IsFalse(WorksheetSnapshot.Capture(worksheet)
            .IsRowVisible(2));
    }

    [TestMethod]
    public void CustomFilterPreservesOtherColumns()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.WorksheetFilter.SetRange(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1)));
        session.WorksheetFilter.ApplyValueFilter(
            0,
            [CellValue.FromText("Open")]);

        session.WorksheetFilter.ApplyCustomFilter(
            1,
            new TableFilterCondition(
                TableFilterComparisonOperator.GreaterThan,
                CellValue.FromNumber(15d)));

        Assert.AreEqual(
            2,
            worksheet.AutoFilter!.Columns.Count);
        session.WorksheetFilter.ClearColumnFilter(1);
        Assert.AreEqual(
            1,
            worksheet.AutoFilter!.Columns.Count);
        Assert.AreEqual(
            0,
            worksheet.AutoFilter.Columns[0].ColumnOffset);
    }

    [TestMethod]
    public void ReapplyingSameFilterDoesNotCreateHistory()
    {
        var workbook = CreateWorkbook();
        var session = new SpreadsheetSession(workbook);
        var filter = new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)));

        session.WorksheetFilter.SetAutoFilter(filter);
        var undoCount = session.History.UndoCount;
        session.WorksheetFilter.SetAutoFilter(filter.Copy());

        Assert.AreEqual(undoCount, session.History.UndoCount);
    }

    [TestMethod]
    public void RichFilterAndSortStateUseSingleProductionHistoryEntries()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.WorksheetFilter.SetRange(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1)));
        var before = session.History.UndoCount;

        session.WorksheetFilter.SetColumnFilter(
            1,
            new WorksheetAutoFilterColumn(
                0,
                topBottom: new SpreadsheetTopBottomFilter(top: true, percent: false, value: 1)));

        Assert.AreEqual(before + 1, session.History.UndoCount);
        Assert.AreEqual(1d, worksheet.AutoFilter!.Columns.Single().TopBottom!.Value);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.AutoFilter!.Columns.Count);
        Assert.IsTrue(session.Redo());

        session.WorksheetFilter.SetSortState(new SpreadsheetFilterSortState([
            new SpreadsheetFilterSortCondition(1, descending: true),
        ]));
        Assert.AreEqual(before + 2, session.History.UndoCount);
        Assert.IsTrue(worksheet.AutoFilter!.SortState!.Conditions.Single().Descending);
        Assert.IsTrue(session.Undo());
        Assert.IsNull(worksheet.AutoFilter!.SortState);
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "Open");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        return workbook;
    }
}
