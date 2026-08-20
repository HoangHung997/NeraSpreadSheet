using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class ConditionalFormattingStructuralSafetyTests
{
    [TestMethod]
    public void StructuralInsertRewritesAbsoluteAndRelativeRuleReferences()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styleId = worksheet.DifferentialStyles.Intern(
            CreateFillPatch());
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(
                    new CellAddress(1, 1),
                    new CellAddress(3, 1))],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=$A$10+A5>0",
                formula2: null,
                styleId,
                priority: 1));

        worksheet.ApplyStructuralChange(
            new WorksheetStructuralChange(
                WorksheetAxis.Row,
                WorksheetStructuralChangeKind.Insert,
                index: 4,
                count: 1));

        var rule = worksheet.ConditionalFormattingRules.Single();
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1)),
            rule.Ranges.Single());
        Assert.AreEqual("=$A$11+A6>0", rule.Formula1);
    }

    [TestMethod]
    public void AxisMoveRejectsContiguousButNonUniformRuleImageAtomically()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), "one");
        worksheet.SetValue(new CellAddress(3, 0), "three");
        var styleId = worksheet.DifferentialStyles.Intern(
            CreateFillPatch());
        var originalRange = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(4, 1));
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [originalRange],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=A2<>\"\"",
                formula2: null,
                styleId,
                priority: 1));
        var beforeVersion = worksheet.Version;
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 3,
            count: 2,
            destinationBoundary: 1);

        Assert.IsTrue(move.TryMapContiguousRange(
            originalRange,
            out _));
        Assert.IsFalse(move.TryMapUniformRange(
            originalRange,
            out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.ApplyAxisMove(move));

        Assert.AreEqual(
            "one",
            worksheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual(
            "three",
            worksheet.GetCell(new CellAddress(3, 0)).Value.RawValue);
        Assert.AreEqual(
            originalRange,
            worksheet.ConditionalFormattingRules.Single().Ranges.Single());
        Assert.AreEqual(beforeVersion, worksheet.Version);
    }

    private static CellStylePatch CreateFillPatch() => new()
    {
        Fill = new CellFillStyle
        {
            IsVisible = true,
            Color = new ColorRgba(220, 80, 60),
        },
    };
}
