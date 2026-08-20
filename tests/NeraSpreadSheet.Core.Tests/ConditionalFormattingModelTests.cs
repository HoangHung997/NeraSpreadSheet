using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class ConditionalFormattingModelTests
{
    [TestMethod]
    public void DifferentialCatalogDeduplicatesAndRuleMutationInvalidatesRange()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var patch = new CellStylePatch
        {
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Color = new ColorRgba(240, 210, 80),
            },
        };
        var firstStyleId =
            worksheet.DifferentialStyles.Intern(patch);
        var secondStyleId =
            worksheet.DifferentialStyles.Intern(patch);
        Assert.AreEqual(firstStyleId, secondStyleId);

        CellsChangedEventArgs? signal = null;
        worksheet.CellsChanged += (_, args) => signal = args;
        var range = new CellRange(
            new CellAddress(2, 3),
            new CellAddress(7, 5));
        var rule = new ConditionalFormattingRule(
            Guid.NewGuid(),
            [range],
            ConditionalFormattingRuleType.CellIs,
            ConditionalFormattingOperator.GreaterThan,
            "=10",
            formula2: null,
            firstStyleId,
            priority: 1,
            stopIfTrue: true);

        worksheet.AddConditionalFormattingRule(rule);

        Assert.AreEqual(1, worksheet.ConditionalFormattingRuleCount);
        Assert.IsNotNull(signal);
        Assert.AreEqual(range, signal.Range);
        Assert.IsTrue(
            worksheet.RemoveConditionalFormattingRule(rule.Id));
        Assert.AreEqual(0, worksheet.ConditionalFormattingRuleCount);
    }

    [TestMethod]
    public void StructuralInsertMapsRuleRangeAndRelativeAnchorFormula()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styleId = worksheet.DifferentialStyles.Intern(
            CreateFillPatch());
        var rule = new ConditionalFormattingRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1))],
            ConditionalFormattingRuleType.Expression,
            ConditionalFormattingOperator.Equal,
            "=A2>0",
            formula2: null,
            styleId,
            priority: 1);

        worksheet.AddConditionalFormattingRule(rule);
        worksheet.ApplyStructuralChange(
            new WorksheetStructuralChange(
                WorksheetAxis.Row,
                WorksheetStructuralChangeKind.Insert,
                index: 0,
                count: 1));

        var mapped = worksheet.ConditionalFormattingRules.Single();
        Assert.AreEqual(
            new CellRange(
                new CellAddress(2, 1),
                new CellAddress(4, 1)),
            mapped.Ranges.Single());
        Assert.AreEqual("=A3>0", mapped.Formula1);
    }

    [TestMethod]
    public void AxisMoveRejectsDiscontiguousRuleBeforeMutatingWorksheet()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(new CellAddress(2, 0), "sentinel");
        var styleId = worksheet.DifferentialStyles.Intern(
            CreateFillPatch());
        var rule = new ConditionalFormattingRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 0),
                new CellAddress(3, 0))],
            ConditionalFormattingRuleType.Expression,
            ConditionalFormattingOperator.Equal,
            "=A2<>\"\"",
            formula2: null,
            styleId,
            priority: 1);
        worksheet.AddConditionalFormattingRule(rule);
        var beforeVersion = worksheet.Version;

        var threw = false;
        try
        {
            worksheet.ApplyAxisMove(
                new WorksheetAxisMove(
                    WorksheetAxis.Row,
                    sourceIndex: 2,
                    count: 1,
                    destinationBoundary: 6));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
        Assert.AreEqual(
            "sentinel",
            worksheet.GetCell(new CellAddress(2, 0)).Value.RawValue);
        Assert.AreEqual(
            rule.Ranges.Single(),
            worksheet.ConditionalFormattingRules.Single().Ranges.Single());
        Assert.AreEqual(beforeVersion, worksheet.Version);
    }

    [TestMethod]
    public void SnapshotOwnsImmutableConditionalFormattingState()
    {
        var worksheet = new Workbook().Worksheets[0];
        var firstPatch = CreateFillPatch();
        var styleId = worksheet.DifferentialStyles.Intern(firstPatch);
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(2, 2))],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=0",
                formula2: null,
                styleId,
                priority: 1));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        worksheet.ClearConditionalFormattingRules();
        worksheet.DifferentialStyles.Intern(new CellStylePatch
        {
            FontColor = new ColorRgba(20, 30, 40),
        });

        Assert.AreEqual(1, snapshot.ConditionalFormattingRuleCount);
        Assert.AreEqual(firstPatch, snapshot.GetDifferentialStyle(0));
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
