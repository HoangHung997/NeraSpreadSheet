using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetSnapshotFormulaSpillTests
{
    [TestMethod]
    public void SnapshotCapturesSpillIdentityAndChildOwnership()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(1, 1);
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                2,
                2,
                static (row, column) =>
                    CellValue.FromNumber((row * 2d) + column + 1d)));

        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.AreEqual(1, snapshot.FormulaSpillCount);
        Assert.AreEqual(1, snapshot.FormulaSpills.Count);
        Assert.IsTrue(snapshot.TryGetFormulaSpill(owner, out var spill));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 2)),
            spill!.Range);
        Assert.IsTrue(snapshot.TryGetFormulaSpillOwner(
            new CellAddress(2, 2),
            out var resolvedOwner));
        Assert.AreEqual(owner, resolvedOwner);
        Assert.IsTrue(snapshot.IsFormulaSpillChild(
            new CellAddress(1, 2)));
        Assert.IsFalse(snapshot.IsFormulaSpillChild(owner));
    }

    [TestMethod]
    public void SnapshotSpillMetadataRemainsImmutableAfterReplacement()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(0, 0);
        worksheet.SetFormula(owner, "=SEQUENCE(2)");
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                2,
                1,
                static (row, _) => CellValue.FromNumber(row + 1d)));
        var before = WorksheetSnapshot.Capture(worksheet);

        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                3,
                1,
                static (row, _) => CellValue.FromNumber(row + 10d)));
        var after = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(before.TryGetFormulaSpill(owner, out var beforeSpill));
        Assert.AreEqual(2, beforeSpill!.RowCount);
        Assert.AreEqual(1d, before.GetCell(owner).Value.RawValue);
        Assert.IsTrue(after.TryGetFormulaSpill(owner, out var afterSpill));
        Assert.AreEqual(3, afterSpill!.RowCount);
        Assert.AreEqual(10d, after.GetCell(owner).Value.RawValue);
    }

    [TestMethod]
    public void SpillErrorSnapshotDoesNotExposeStaleOwnership()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(0, 0);
        worksheet.SetFormula(owner, "=SEQUENCE(2)");
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                2,
                1,
                static (row, _) => CellValue.FromNumber(row + 1d)));
        worksheet.SetFormulaSpillError(owner);

        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.AreEqual(0, snapshot.FormulaSpillCount);
        Assert.AreEqual("#SPILL!", snapshot.GetCell(owner).Value.RawValue);
        Assert.IsFalse(snapshot.TryGetFormulaSpillOwner(owner, out _));
    }
}
