using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetFormulaSpillTests
{
    [TestMethod]
    public void ApplyMaterializesSparseCellsAndTracksOwnership()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(1, 1);
        worksheet.SetFormula(owner, "=SEQUENCE(2,3)");
        var values = FormulaArrayValue.Create(
            2,
            3,
            static (row, column) =>
                CellValue.FromNumber((row * 3d) + column + 1d));

        var result = worksheet.TryApplyFormulaSpill(owner, values);

        Assert.IsTrue(result.IsApplied);
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(6d, worksheet.GetValue(new CellAddress(2, 3)));
        Assert.IsTrue(worksheet.TryGetFormulaSpillOwner(
            new CellAddress(2, 2),
            out var resolvedOwner));
        Assert.AreEqual(owner, resolvedOwner);
        Assert.IsTrue(worksheet.IsFormulaSpillChild(
            new CellAddress(1, 2)));
        Assert.IsFalse(worksheet.IsFormulaSpillChild(owner));
    }

    [TestMethod]
    public void ReplacingSpillClearsObsoleteChildrenAndPreservesStyles()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(0, 0);
        var styledChild = new CellAddress(1, 1);
        var styleId = workbook.Styles.Intern(new CellStyle
        {
            Alignment = new CellAlignmentStyle
            {
                WrapText = true,
            },
        });
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        worksheet.SetStyle(styledChild, styleId);
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                2,
                2,
                static (row, column) =>
                    CellValue.FromNumber((row * 2d) + column + 1d)));

        var replacement = worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                1,
                2,
                static (_, column) =>
                    CellValue.FromNumber(10d + column)));

        Assert.IsTrue(replacement.IsApplied);
        Assert.AreEqual(10d, worksheet.GetValue(owner));
        Assert.AreEqual(11d, worksheet.GetValue(new CellAddress(0, 1)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(styledChild));
        Assert.AreEqual(styleId, worksheet.GetCell(styledChild).StyleId);
    }

    [TestMethod]
    public void CollisionPreflightLeavesWorksheetUnchanged()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(0, 0);
        var blocker = new CellAddress(0, 1);
        worksheet.SetFormula(owner, "=SEQUENCE(1,3)");
        worksheet.SetValue(blocker, "occupied");
        var beforeOwner = worksheet.GetCell(owner);

        var result = worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                1,
                3,
                static (_, column) =>
                    CellValue.FromNumber(column + 1d)));

        Assert.AreEqual(FormulaSpillApplyStatus.Blocked, result.Status);
        Assert.AreEqual(blocker, result.BlockingAddress);
        Assert.AreEqual(beforeOwner, worksheet.GetCell(owner));
        Assert.AreEqual("occupied", worksheet.GetValue(blocker));
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void DirectChildEditInvalidatesOwnershipAndClearsUnchangedSiblings()
    {
        var worksheet = new Workbook().Worksheets[0];
        var owner = new CellAddress(0, 0);
        worksheet.SetFormula(owner, "=SEQUENCE(3)");
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                3,
                1,
                static (row, _) => CellValue.FromNumber(row + 1d)));

        worksheet.SetValue(new CellAddress(1, 0), 99d);

        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(99d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));
        Assert.IsFalse(worksheet.TryGetFormulaSpillOwner(
            new CellAddress(1, 0),
            out _));
    }

    [TestMethod]
    public void StyleOnlyChildChangeKeepsSpillOwnership()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(0, 0);
        var child = new CellAddress(0, 1);
        worksheet.SetFormula(owner, "=SEQUENCE(1,2)");
        worksheet.TryApplyFormulaSpill(
            owner,
            FormulaArrayValue.Create(
                1,
                2,
                static (_, column) => CellValue.FromNumber(column + 1d)));
        var styleId = workbook.Styles.Intern(new CellStyle
        {
            Font = new CellFontStyle
            {
                Weight = 700,
            },
        });

        worksheet.SetStyle(child, styleId);

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.IsTrue(worksheet.IsFormulaSpillChild(child));
        Assert.AreEqual(2d, worksheet.GetValue(child));
        Assert.AreEqual(styleId, worksheet.GetCell(child).StyleId);
    }

    [TestMethod]
    public void SpillErrorClearsChildrenAndRetainsOwnerFormula()
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

        Assert.AreEqual("#SPILL!", worksheet.GetValue(owner));
        Assert.AreEqual("=SEQUENCE(2)", worksheet.GetFormula(owner));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void MergeTableAndBoundsBlockSpills()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(new CellAddress(0, 0), "=SEQUENCE(2,2)");
        worksheet.MergeCells(new CellRange(
            new CellAddress(1, 1),
            new CellAddress(1, 2)));
        var merged = worksheet.TryApplyFormulaSpill(
            new CellAddress(0, 0),
            FormulaArrayValue.Create(
                2,
                2,
                static (_, _) => CellValue.FromNumber(1d)));
        Assert.AreEqual(FormulaSpillApplyStatus.Blocked, merged.Status);

        worksheet.UnmergeCell(new CellAddress(1, 1));
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Values",
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Value")]));
        var table = worksheet.TryApplyFormulaSpill(
            new CellAddress(0, 0),
            FormulaArrayValue.Create(
                2,
                2,
                static (_, _) => CellValue.FromNumber(1d)));
        Assert.AreEqual(FormulaSpillApplyStatus.Blocked, table.Status);

        var edge = new CellAddress(
            SpreadsheetLimits.MaxRows - 1,
            SpreadsheetLimits.MaxColumns - 1);
        worksheet.SetFormula(edge, "=SEQUENCE(2)");
        var bounds = worksheet.TryApplyFormulaSpill(
            edge,
            FormulaArrayValue.Create(
                2,
                1,
                static (_, _) => CellValue.FromNumber(1d)));
        Assert.AreEqual(FormulaSpillApplyStatus.OutOfBounds, bounds.Status);
    }
}
