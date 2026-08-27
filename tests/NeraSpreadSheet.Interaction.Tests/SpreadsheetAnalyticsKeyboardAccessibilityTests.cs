using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsKeyboardAccessibilityTests
{
    private static readonly string[] ExpectedActions =
        ["Select", "Move", "Resize", "Delete"];

    [TestMethod]
    public void KeyboardMapperUsesDeterministicMoveResizeAndAccelerationSemantics()
    {
        Assert.AreEqual(
            new SpreadsheetAnalyticsKeyboardIntent(
                SpreadsheetAnalyticsKeyboardAction.Move,
                -1d,
                0d),
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Left));
        Assert.AreEqual(
            new SpreadsheetAnalyticsKeyboardIntent(
                SpreadsheetAnalyticsKeyboardAction.Resize,
                10d,
                0d),
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Right,
                SpreadsheetAnalyticsKeyboardModifiers.Shift |
                SpreadsheetAnalyticsKeyboardModifiers.Control));
        Assert.AreEqual(
            SpreadsheetAnalyticsKeyboardAction.Delete,
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Delete).Action);
        Assert.AreEqual(
            SpreadsheetAnalyticsKeyboardAction.CancelOrClearSelection,
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Escape).Action);
    }

    [TestMethod]
    public void AccessibilityProjectionIsStableOrderedNamedAndSelectionAware()
    {
        var chart = SpreadsheetAnalyticsItemKey.ForChart(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var pivot = SpreadsheetAnalyticsItemKey.ForPivot(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var targets = new[]
        {
            new SpreadsheetAnalyticsInteractionTarget(
                pivot,
                new RectD(20d, 30d, 300d, 180d),
                new RectD(20d, 30d, 300d, 180d),
                new RectD(0d, 0d, 240d, 160d),
                5),
            new SpreadsheetAnalyticsInteractionTarget(
                chart,
                new RectD(5d, 8d, 200d, 120d),
                new RectD(5d, 8d, 200d, 120d),
                new RectD(0d, 0d, 500d, 400d),
                2),
        };

        var nodes = SpreadsheetAnalyticsAccessibilityProjector.Project(
            targets,
            pivot,
            item => item == chart ? "Revenue chart" : null);

        Assert.AreEqual(2, nodes.Count);
        Assert.AreEqual(chart, nodes[0].Item);
        Assert.AreEqual("Revenue chart", nodes[0].Name);
        Assert.AreEqual(SpreadsheetAnalyticsAccessibleRole.Chart, nodes[0].Role);
        Assert.IsFalse(nodes[0].IsSelected);
        Assert.IsFalse(nodes[0].IsPartiallyClipped);
        Assert.AreEqual(
            "analytics-chart-11111111111111111111111111111111",
            nodes[0].AutomationId);

        Assert.AreEqual(pivot, nodes[1].Item);
        Assert.AreEqual("Pivot table", nodes[1].Name);
        Assert.AreEqual(SpreadsheetAnalyticsAccessibleRole.PivotTable, nodes[1].Role);
        Assert.IsTrue(nodes[1].IsSelected);
        Assert.IsTrue(nodes[1].IsPartiallyClipped);
        CollectionAssert.AreEqual(ExpectedActions, nodes[1].Actions.ToArray());
    }
}
