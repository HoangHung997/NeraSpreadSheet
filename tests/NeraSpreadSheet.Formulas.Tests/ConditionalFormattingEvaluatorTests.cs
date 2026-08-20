using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ConditionalFormattingEvaluatorTests
{
    [TestMethod]
    public void CellIsRuleAppliesDifferentialFill()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(default, 12d);
        var fill = new CellFillStyle
        {
            IsVisible = true,
            Color = new ColorRgba(230, 90, 50),
        };
        var styleId = worksheet.DifferentialStyles.Intern(
            new CellStylePatch { Fill = fill });
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=10",
                formula2: null,
                styleId,
                priority: 1));

        var style = ConditionalFormattingEvaluator.ResolveStyle(
            WorksheetSnapshot.Capture(worksheet),
            default,
            CellStyle.Default);

        Assert.AreEqual(fill, style.Fill);
    }

    [TestMethod]
    public void ExpressionRuleTranslatesRelativeReferencePerCell()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), 1d);
        worksheet.SetValue(new CellAddress(2, 0), -1d);
        var patch = new CellStylePatch
        {
            FontColor = new ColorRgba(10, 120, 210),
        };
        var styleId = worksheet.DifferentialStyles.Intern(patch);
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(
                    new CellAddress(1, 1),
                    new CellAddress(2, 1))],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=A2>0",
                formula2: null,
                styleId,
                priority: 1));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        var first = ConditionalFormattingEvaluator.ResolveStyle(
            snapshot,
            new CellAddress(1, 1),
            CellStyle.Default);
        var second = ConditionalFormattingEvaluator.ResolveStyle(
            snapshot,
            new CellAddress(2, 1),
            CellStyle.Default);

        Assert.AreEqual(
            patch.FontColor,
            first.Font.Color);
        Assert.AreEqual(
            CellStyle.Default.Font.Color,
            second.Font.Color);
    }

    [TestMethod]
    public void PriorityAndStopIfTruePreserveHigherPriorityProperties()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(default, 5d);
        var highFill = new CellFillStyle
        {
            IsVisible = true,
            Color = new ColorRgba(240, 200, 40),
        };
        var highStyle = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = highFill,
                FontWeight = 700,
            });
        var lowStyle = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Color = new ColorRgba(20, 20, 20),
                },
                FontColor = new ColorRgba(30, 90, 190),
            });

        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=0",
                formula2: null,
                highStyle,
                priority: 1,
                stopIfTrue: false));
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=0",
                formula2: null,
                lowStyle,
                priority: 2));

        var style = ConditionalFormattingEvaluator.ResolveStyle(
            WorksheetSnapshot.Capture(worksheet),
            default,
            CellStyle.Default);

        Assert.AreEqual(highFill, style.Fill);
        Assert.AreEqual(700, style.Font.Weight);
        Assert.AreEqual(
            new ColorRgba(30, 90, 190),
            style.Font.Color);
    }

    [TestMethod]
    public void StopIfTrueSkipsLowerPriorityRules()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(default, 5d);
        var highStyle = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Color = new ColorRgba(240, 200, 40),
                },
            });
        var lowStyle = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                FontColor = new ColorRgba(30, 90, 190),
            });

        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=1=1",
                formula2: null,
                highStyle,
                priority: 1,
                stopIfTrue: true));
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=1=1",
                formula2: null,
                lowStyle,
                priority: 2));

        var style = ConditionalFormattingEvaluator.ResolveStyle(
            WorksheetSnapshot.Capture(worksheet),
            default,
            CellStyle.Default);

        Assert.AreEqual(
            CellStyle.Default.Font.Color,
            style.Font.Color);
    }
}
