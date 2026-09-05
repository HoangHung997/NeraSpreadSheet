using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ConditionalAggregateSpreadsheetSessionTests
{
    [TestMethod]
    public void SourceEditRecalculatesOnlyAffectedConditionalAggregateDependents()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "North");
        worksheet.SetValue(new CellAddress(1, 0), "South");
        worksheet.SetValue(new CellAddress(2, 0), "North");
        worksheet.SetValue(new CellAddress(0, 1), 10d);
        worksheet.SetValue(new CellAddress(1, 1), 20d);
        worksheet.SetValue(new CellAddress(2, 1), 30d);
        var formulaAddress = new CellAddress(0, 3);
        worksheet.SetFormula(
            formulaAddress,
            "=SUMIF(A1:A3,\"North\",B1:B3)");
        var unrelated = new CellAddress(0, 5);
        worksheet.SetFormula(unrelated, "=1+1");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        Assert.AreEqual(40d, worksheet.GetValue(formulaAddress));
        Assert.AreEqual(2d, worksheet.GetValue(unrelated));

        session.SetValue(new CellAddress(1, 0), "North");

        Assert.AreEqual(60d, worksheet.GetValue(formulaAddress));
        Assert.AreEqual(2d, worksheet.GetValue(unrelated));
        var dependencies = session.Calculation.DependencyGraph.GetDependencies(
            new NeraSpreadSheet.Formulas.FormulaCellKey(
                worksheet.Name,
                formulaAddress));
        Assert.AreEqual(2, dependencies.Count);
    }

    [TestMethod]
    public void CriteriaCellEditRecalculatesConditionalAggregate()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 10d);
        worksheet.SetValue(new CellAddress(1, 0), 20d);
        worksheet.SetValue(new CellAddress(2, 0), 30d);
        var criterion = new CellAddress(0, 2);
        worksheet.SetValue(criterion, ">10");
        var formula = new CellAddress(0, 3);
        worksheet.SetFormula(formula, "=COUNTIF(A1:A3,C1)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        Assert.AreEqual(2d, worksheet.GetValue(formula));

        session.SetValue(criterion, ">=10");

        Assert.AreEqual(3d, worksheet.GetValue(formula));
    }

    [TestMethod]
    public void UndoAndRedoSourceEditRestoreConditionalAggregateResult()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A");
        worksheet.SetValue(new CellAddress(1, 0), "B");
        worksheet.SetValue(new CellAddress(0, 1), 10d);
        worksheet.SetValue(new CellAddress(1, 1), 20d);
        var formula = new CellAddress(0, 3);
        worksheet.SetFormula(formula, "=SUMIF(A1:A2,\"A\",B1:B2)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        Assert.AreEqual(10d, worksheet.GetValue(formula));

        session.SetValue(new CellAddress(1, 0), "A");
        Assert.AreEqual(30d, worksheet.GetValue(formula));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(10d, worksheet.GetValue(formula));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(30d, worksheet.GetValue(formula));
    }

    [TestMethod]
    public void ConditionalAggregateCanDriveDynamicArrayShape()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 10d);
        worksheet.SetValue(new CellAddress(1, 0), 20d);
        worksheet.SetValue(new CellAddress(2, 0), 30d);
        var owner = new CellAddress(0, 2);
        worksheet.SetFormula(
            owner,
            "=SEQUENCE(COUNTIF(A1:A3,\">10\"))");
        var session = new SpreadsheetSession(workbook);

        session.Recalculate();

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 2)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 2)));
    }

    [TestMethod]
    public void MismatchedRangesCommitValueErrorWithoutPartialMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(0, 1), 10d);
        var formula = new CellAddress(0, 3);
        worksheet.SetFormula(formula, "=SUMIF(A1:A2,\"A\",B1:B3)");
        var session = new SpreadsheetSession(workbook);

        var result = session.Recalculate();

        Assert.AreEqual("#VALUE!", worksheet.GetValue(formula));
        Assert.AreEqual("=SUMIF(A1:A2,\"A\",B1:B3)", worksheet.GetFormula(formula));
        Assert.IsTrue(result.ErrorCellCount >= 1);
    }
}
