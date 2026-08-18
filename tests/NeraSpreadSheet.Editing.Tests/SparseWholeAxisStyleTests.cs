using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SparseWholeAxisStyleTests
{
    [TestMethod]
    public void WholeRowFillRemainsSparseAndSupportsUndoRedo()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = session.ActiveWorksheet;
        var fill = new ColorRgba(36, 118, 210);
        session.Selection.SelectRow(6);

        session.Styles.SetFill(fill);

        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(6, SpreadsheetLimits.MaxColumns - 1),
                workbook.Styles).Fill.Color);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.RowStyleSpanCount);
        Assert.IsFalse(worksheet.GetEffectiveStyle(
            new CellAddress(6, 200),
            workbook.Styles).Fill.IsVisible);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(6, 200),
                workbook.Styles).Fill.Color);
    }

    [TestMethod]
    public void WholeColumnFormattingBypassesFiniteMaterializationLimit()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var styles = new SpreadsheetStyleController(
            session,
            maximumMaterializedCells: 1);
        session.Selection.SelectColumn(4);

        styles.ToggleBold();

        Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount);
        Assert.AreEqual(1, session.ActiveWorksheet.ColumnStyleSpanCount);
        Assert.AreEqual(
            700,
            session.ActiveWorksheet.GetEffectiveStyle(
                new CellAddress(SpreadsheetLimits.MaxRows - 1, 4),
                workbook.Styles).Font.Weight);
    }

    [TestMethod]
    public void WholeSheetFormattingUsesOneSparseAxisSpan()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var fill = new ColorRgba(240, 210, 70);
        session.Selection.SelectAll();

        session.Styles.SetFill(fill);

        var worksheet = session.ActiveWorksheet;
        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(0, worksheet.ColumnStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    SpreadsheetLimits.MaxColumns - 1),
                workbook.Styles).Fill.Color);
    }

    [TestMethod]
    public void WholeRowPatchUpdatesDirectCellStyleAndUndoRestoresIt()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = session.ActiveWorksheet;
        var address = new CellAddress(3, 8);
        var originalStyleId = workbook.Styles.Intern(new CellStyle
        {
            Font = new CellFontStyle
            {
                Italic = true,
            },
        });
        worksheet.SetStyle(address, originalStyleId);
        var fill = new ColorRgba(180, 70, 140);
        session.Selection.SelectRow(3);

        session.Styles.SetFill(fill);

        var effective = worksheet.GetEffectiveStyle(address, workbook.Styles);
        Assert.IsTrue(effective.Font.Italic);
        Assert.AreEqual(fill, effective.Fill.Color);
        Assert.AreEqual(1, worksheet.UsedCellCount);
        Assert.AreNotEqual(originalStyleId, worksheet.GetCell(address).StyleId);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(originalStyleId, worksheet.GetCell(address).StyleId);
        Assert.AreEqual(0, worksheet.RowStyleSpanCount);
        Assert.IsTrue(worksheet.GetEffectiveStyle(
            address,
            workbook.Styles).Font.Italic);
        Assert.IsFalse(worksheet.GetEffectiveStyle(
            address,
            workbook.Styles).Fill.IsVisible);
    }

    [TestMethod]
    public void LaterColumnStyleOverridesEarlierRowStyleAtIntersection()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = session.ActiveWorksheet;
        var rowFill = new ColorRgba(210, 60, 40);
        var columnFill = new ColorRgba(40, 90, 210);
        session.Selection.SelectRow(2);
        session.Styles.SetFill(rowFill);
        session.Selection.SelectColumn(5);

        session.Styles.SetFill(columnFill);

        Assert.AreEqual(
            columnFill,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 5),
                workbook.Styles).Fill.Color);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            rowFill,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 5),
                workbook.Styles).Fill.Color);
    }

    [TestMethod]
    public void FiniteRangeStillEnforcesMaterializationLimit()
    {
        var session = new SpreadsheetSession(new Workbook());
        var styles = new SpreadsheetStyleController(
            session,
            maximumMaterializedCells: 3);
        session.Selection.Select(new CellRange(
            default,
            new CellAddress(1, 1)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            styles.SetFill(new ColorRgba(10, 20, 30)));
        Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount);
    }
}
