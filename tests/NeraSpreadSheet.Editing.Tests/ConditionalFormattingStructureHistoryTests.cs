using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ConditionalFormattingStructureHistoryTests
{
    [TestMethod]
    public void InsertUndoRedoRestoresAndRemapsRuleStateExactly()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook, worksheet);
        var styleId = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Color = new ColorRgba(240, 210, 80),
                },
            });
        var ruleId = Guid.NewGuid();
        var originalRange = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(3, 1));
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                ruleId,
                [originalRange],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=A2>0",
                formula2: null,
                styleId,
                priority: 1));

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
            originalRange,
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

    private static void AssertRule(
        Worksheet worksheet,
        Guid expectedId,
        CellRange expectedRange,
        string expectedFormula)
    {
        var rule = worksheet.ConditionalFormattingRules.Single();
        Assert.AreEqual(expectedId, rule.Id);
        Assert.AreEqual(expectedRange, rule.Ranges.Single());
        Assert.AreEqual(expectedFormula, rule.Formula1);
        Assert.AreEqual(1, rule.Priority);
    }
}
