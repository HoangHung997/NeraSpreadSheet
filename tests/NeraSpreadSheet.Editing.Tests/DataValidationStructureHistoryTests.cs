using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DataValidationStructureHistoryTests
{
    [TestMethod]
    public void InsertUndoRedoRestoresAndRemapsRuleExactly()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook, worksheet);
        var ruleId = Guid.NewGuid();
        worksheet.AddDataValidationRule(new DataValidationRule(
            ruleId,
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1))],
            DataValidationType.Custom,
            @operator: null,
            "=A2>0"));

        session.Structure.InsertRows(0, 1);
        AssertRule(
            worksheet,
            ruleId,
            new CellRange(
                new CellAddress(2, 1),
                new CellAddress(4, 1)),
            "=A3>0");

        Assert.IsTrue(session.Undo());
        AssertRule(
            worksheet,
            ruleId,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1)),
            "=A2>0");

        Assert.IsTrue(session.Redo());
        AssertRule(
            worksheet,
            ruleId,
            new CellRange(
                new CellAddress(2, 1),
                new CellAddress(4, 1)),
            "=A3>0");
    }

    [TestMethod]
    public void DeleteThatRemovesCompleteTargetDropsRuleAndUndoRestoresIt()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook, worksheet);
        var rule = new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 0),
                new CellAddress(2, 0))],
            DataValidationType.List,
            @operator: null,
            "=\"A,B\"");
        worksheet.AddDataValidationRule(rule);

        session.Structure.DeleteRows(1, 2);
        Assert.AreEqual(0, worksheet.DataValidationRuleCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, worksheet.DataValidationRuleCount);
        Assert.AreEqual(rule.Id, worksheet.DataValidationRules.Single().Id);
    }

    [TestMethod]
    public void InternallyPermutedTargetIsRejectedAtomically()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook, worksheet);
        worksheet.SetValue(new CellAddress(1, 0), "first");
        worksheet.SetValue(new CellAddress(2, 0), "second");
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1))],
            DataValidationType.Custom,
            @operator: null,
            "=A2<>\"\""));
        var before = worksheet.DataValidationRules.Single();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Reorder.MoveRows(
                sourceIndex: 2,
                count: 1,
                destinationBoundary: 1));
        Assert.AreEqual("first", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("second", worksheet.GetValue(new CellAddress(2, 0)));
        var after = worksheet.DataValidationRules.Single();
        Assert.AreEqual(before.Id, after.Id);
        Assert.AreEqual(before.Ranges.Single(), after.Ranges.Single());
        Assert.AreEqual(before.Formula1, after.Formula1);
        Assert.AreEqual(0, session.History.UndoCount);
    }

    private static void AssertRule(
        Worksheet worksheet,
        Guid expectedId,
        CellRange expectedRange,
        string expectedFormula)
    {
        var rule = worksheet.DataValidationRules.Single();
        Assert.AreEqual(expectedId, rule.Id);
        Assert.AreEqual(expectedRange, rule.Ranges.Single());
        Assert.AreEqual(expectedFormula, rule.Formula1);
    }
}
