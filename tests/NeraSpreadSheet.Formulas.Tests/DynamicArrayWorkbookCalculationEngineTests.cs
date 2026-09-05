using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DynamicArrayWorkbookCalculationEngineTests
{
    [TestMethod]
    public void RecalculateMaterializesSequenceAndUpdatesDependentFormula()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(new CellAddress(0, 0), "=SEQUENCE(2,2)");
        worksheet.SetFormula(new CellAddress(0, 3), "=SUM(A1:B2)");
        var engine = new DynamicArrayWorkbookCalculationEngine();

        var result = engine.Recalculate(workbook);

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.AreEqual(10d, worksheet.GetValue(new CellAddress(0, 3)));
        Assert.IsTrue(result.UpdatedCellCount >= 5);
        Assert.AreEqual(0, result.ErrorCellCount);
    }

    [TestMethod]
    public void BlockedSpillCommitsErrorAndRecalculatesDependents()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(new CellAddress(0, 0), "=SEQUENCE(1,2)");
        worksheet.SetValue(new CellAddress(0, 1), "blocked");
        worksheet.SetFormula(new CellAddress(0, 3), "=A1");
        var engine = new DynamicArrayWorkbookCalculationEngine();

        var result = engine.Recalculate(workbook);

        Assert.AreEqual("#SPILL!", worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("blocked", worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual("#SPILL!", worksheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.IsTrue(result.ErrorCellCount >= 1);
    }

    [TestMethod]
    public void AffectedRecalculationResizesSpillAndUpdatesRangeDependent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var source = new CellAddress(0, 0);
        worksheet.SetValue(source, 2d);
        worksheet.SetFormula(new CellAddress(0, 1), "=SEQUENCE(A1)");
        worksheet.SetFormula(new CellAddress(0, 3), "=SUM(B1:B3)");
        var engine = new DynamicArrayWorkbookCalculationEngine();
        engine.Recalculate(workbook);
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(0, 3)));

        worksheet.SetValue(source, 3d);
        engine.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(source, source));

        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 1)));
        Assert.AreEqual(6d, worksheet.GetValue(new CellAddress(0, 3)));
        Assert.IsTrue(worksheet.TryGetFormulaSpill(
            new CellAddress(0, 1),
            out var spill));
        Assert.AreEqual(3, spill!.RowCount);
    }

    [TestMethod]
    public void ClearingBlockerAllowsSpillRecoveryOnAffectedRecalculation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var blocker = new CellAddress(0, 1);
        worksheet.SetFormula(new CellAddress(0, 0), "=SEQUENCE(1,2)");
        worksheet.SetValue(blocker, "blocked");
        var engine = new DynamicArrayWorkbookCalculationEngine();
        engine.Recalculate(workbook);
        Assert.AreEqual("#SPILL!", worksheet.GetValue(new CellAddress(0, 0)));

        worksheet.SetValue(blocker, null);
        engine.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(blocker, blocker));

        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual(2d, worksheet.GetValue(blocker));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void ReplacingDynamicFormulaWithScalarClearsChildren()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(0, 0);
        worksheet.SetFormula(owner, "=SEQUENCE(3)");
        var engine = new DynamicArrayWorkbookCalculationEngine();
        engine.Recalculate(workbook);
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 0)));

        worksheet.SetFormula(owner, "=10");
        engine.Recalculate(workbook);

        Assert.AreEqual(10d, worksheet.GetValue(owner));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void TransposeSpillsReferencedRangeAndTracksDependency()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 1d);
        worksheet.SetValue(new CellAddress(0, 1), 2d);
        worksheet.SetValue(new CellAddress(1, 0), 3d);
        worksheet.SetValue(new CellAddress(1, 1), 4d);
        var owner = new CellAddress(0, 3);
        worksheet.SetFormula(owner, "=TRANSPOSE(A1:B2)");
        var engine = new DynamicArrayWorkbookCalculationEngine();

        engine.Recalculate(workbook);

        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(0, 4)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 3)));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 4)));
        var dependencies = engine.DependencyGraph.GetDependencies(
            new FormulaCellKey(worksheet.Name, owner));
        Assert.AreEqual(1, dependencies.Count);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)),
            dependencies[0].Range);
    }
}
