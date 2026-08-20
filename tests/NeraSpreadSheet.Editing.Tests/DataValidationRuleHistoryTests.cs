using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DataValidationRuleHistoryTests
{
    [TestMethod]
    public void AddRemoveUndoRedoPreserveExactRule()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var rule = new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(4, 1))],
            DataValidationType.List,
            null,
            "=\"A,B,C\"",
            showInputMessage: true,
            promptTitle: "Choose",
            prompt: "Select a value.");

        session.Validation.AddRule(rule);
        Assert.AreEqual(1, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.AreEqual("Add data validation", session.History.NextUndoDescription);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(rule, session.ActiveWorksheet.DataValidationRules.Single());

        Assert.IsTrue(session.Validation.RemoveRule(rule.Id));
        Assert.AreEqual(0, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.AreEqual("Remove data validation", session.History.NextUndoDescription);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(rule, session.ActiveWorksheet.DataValidationRules.Single());
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(0, session.ActiveWorksheet.DataValidationRuleCount);
    }

    [TestMethod]
    public void FailedOverlappingAddDoesNotEnterHistory()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Validation.AddRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Whole,
            DataValidationOperator.GreaterThan,
            "0"));
        var historyBefore = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Validation.AddRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                DataValidationType.Decimal,
                DataValidationOperator.LessThan,
                "100")));
        Assert.AreEqual(historyBefore, session.History.UndoCount);
        Assert.AreEqual(1, session.ActiveWorksheet.DataValidationRuleCount);
    }
}
