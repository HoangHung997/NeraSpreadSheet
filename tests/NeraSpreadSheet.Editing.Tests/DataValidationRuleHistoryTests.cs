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
        AssertRuleEquivalent(
            rule,
            session.ActiveWorksheet.DataValidationRules.Single());

        Assert.IsTrue(session.Validation.RemoveRule(rule.Id));
        Assert.AreEqual(0, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.AreEqual("Remove data validation", session.History.NextUndoDescription);
        Assert.IsTrue(session.Undo());
        AssertRuleEquivalent(
            rule,
            session.ActiveWorksheet.DataValidationRules.Single());
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

    private static void AssertRuleEquivalent(
        DataValidationRule expected,
        DataValidationRule actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(expected.Type, actual.Type);
        Assert.AreEqual(expected.Operator, actual.Operator);
        Assert.AreEqual(expected.Formula1, actual.Formula1);
        Assert.AreEqual(expected.Formula2, actual.Formula2);
        Assert.AreEqual(expected.AllowBlank, actual.AllowBlank);
        Assert.AreEqual(expected.ShowInputMessage, actual.ShowInputMessage);
        Assert.AreEqual(expected.PromptTitle, actual.PromptTitle);
        Assert.AreEqual(expected.Prompt, actual.Prompt);
        Assert.AreEqual(expected.ShowErrorMessage, actual.ShowErrorMessage);
        Assert.AreEqual(expected.ErrorStyle, actual.ErrorStyle);
        Assert.AreEqual(expected.ErrorTitle, actual.ErrorTitle);
        Assert.AreEqual(expected.Error, actual.Error);
        Assert.AreEqual(expected.ShowDropDown, actual.ShowDropDown);
        CollectionAssert.AreEqual(
            expected.Ranges.ToArray(),
            actual.Ranges.ToArray());
    }
}
