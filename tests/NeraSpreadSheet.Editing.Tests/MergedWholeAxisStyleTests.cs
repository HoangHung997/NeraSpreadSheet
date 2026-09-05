using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class MergedWholeAxisStyleTests
{
    [TestMethod]
    public void WholeRowFormattingAcrossMergedRangeUpdatesAnchorAndUndo()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = session.ActiveWorksheet;
        var mergedRange = new CellRange(
            new CellAddress(1, 0),
            new CellAddress(2, 1));
        worksheet.MergeCells(mergedRange);
        var fill = new ColorRgba(30, 140, 205);
        session.Selection.SelectRow(2);

        session.Styles.SetFill(fill);

        Assert.AreEqual(1, worksheet.UsedCellCount);
        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                mergedRange.TopLeft,
                workbook.Styles).Fill.Color);
        Assert.IsTrue(worksheet.MergedCells.TryGetContaining(
            new CellAddress(2, 1),
            out var actualMerge));
        Assert.AreEqual(mergedRange, actualMerge);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.AreEqual(0, worksheet.RowStyleSpanCount);
        Assert.IsFalse(worksheet.GetEffectiveStyle(
            mergedRange.TopLeft,
            workbook.Styles).Fill.IsVisible);
        Assert.IsTrue(worksheet.MergedCells.TryGetContaining(
            new CellAddress(2, 1),
            out actualMerge));
        Assert.AreEqual(mergedRange, actualMerge);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                mergedRange.TopLeft,
                workbook.Styles).Fill.Color);
    }

    [TestMethod]
    public void WholeColumnPatchPreservesDirectMergedAnchorStyle()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var worksheet = session.ActiveWorksheet;
        var mergedRange = new CellRange(
            new CellAddress(0, 3),
            new CellAddress(1, 4));
        worksheet.MergeCells(mergedRange);
        var originalStyleId = workbook.Styles.Intern(new CellStyle
        {
            Font = new CellFontStyle
            {
                Italic = true,
            },
        });
        worksheet.SetStyle(mergedRange.TopLeft, originalStyleId);
        var fill = new ColorRgba(185, 75, 150);
        session.Selection.SelectColumn(4);

        session.Styles.SetFill(fill);

        var effective = worksheet.GetEffectiveStyle(
            mergedRange.TopLeft,
            workbook.Styles);
        Assert.IsTrue(effective.Font.Italic);
        Assert.AreEqual(fill, effective.Fill.Color);
        Assert.AreNotEqual(
            originalStyleId,
            worksheet.GetCell(mergedRange.TopLeft).StyleId);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            originalStyleId,
            worksheet.GetCell(mergedRange.TopLeft).StyleId);
        effective = worksheet.GetEffectiveStyle(
            mergedRange.TopLeft,
            workbook.Styles);
        Assert.IsTrue(effective.Font.Italic);
        Assert.IsFalse(effective.Fill.IsVisible);
    }
}
