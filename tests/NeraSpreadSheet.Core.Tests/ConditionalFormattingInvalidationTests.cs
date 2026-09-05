using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class ConditionalFormattingInvalidationTests
{
    [TestMethod]
    public void SourceCellMutationInvalidatesConditionalTargetRanges()
    {
        var worksheet = new Workbook().Worksheets[0];
        var targetRange = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(3, 1));
        var styleId = worksheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Color = new ColorRgba(240, 210, 80),
                },
            });
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [targetRange],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=$A$1>0",
                formula2: null,
                styleId,
                priority: 1));

        CellsChangedEventArgs? signal = null;
        worksheet.CellsChanged += (_, args) => signal = args;
        worksheet.SetValue(default, 1d);

        Assert.IsNotNull(signal);
        Assert.AreEqual(
            new CellRange(
                default,
                new CellAddress(3, 1)),
            signal.Range);
    }
}
