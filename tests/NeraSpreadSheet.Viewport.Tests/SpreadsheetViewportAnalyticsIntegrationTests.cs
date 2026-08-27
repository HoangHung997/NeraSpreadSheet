using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetViewportAnalyticsIntegrationTests
{
    [TestMethod]
    public void ComposeIncludesAnalyticsOverlayWithoutPollutingCellDisplayListCache()
    {
        var session = CreateSessionWithSource();
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column,
            "Revenue");
        var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var engine = new SpreadsheetViewportEngine(session);

        var first = engine.Compose(0d, 0d, 640d, 420d, 0d);
        Assert.IsTrue(EnumerateCommands(first.DisplayList)
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Revenue"));
        var misses = engine.DisplayListCacheMissCount;
        var hits = engine.DisplayListCacheHitCount;
        var beforeTarget = engine.GetAnalyticsInteractionTargets(first.Layout)
            .Single();

        Assert.IsTrue(session.AnalyticsPlacements.MoveBy(item, 31.5d, 17.25d));
        var second = engine.Compose(0d, 0d, 640d, 420d, 0d);
        var afterTarget = engine.GetAnalyticsInteractionTargets(second.Layout)
            .Single();

        Assert.AreEqual(misses, engine.DisplayListCacheMissCount);
        Assert.IsTrue(engine.DisplayListCacheHitCount > hits);
        Assert.AreEqual(
            beforeTarget.ViewportBounds.X + 31.5d,
            afterTarget.ViewportBounds.X,
            1e-9);
        Assert.AreEqual(
            beforeTarget.ViewportBounds.Y + 17.25d,
            afterTarget.ViewportBounds.Y,
            1e-9);
        Assert.IsTrue(EnumerateCommands(second.DisplayList)
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Revenue"));
    }

    [TestMethod]
    public void InteractionPreviewChangesRenderedTargetsWithoutMutatingPlacement()
    {
        var session = CreateSessionWithSource();
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line,
            "Trend");
        var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var engine = new SpreadsheetViewportEngine(session);
        var frame = engine.Compose(0d, 0d, 640d, 420d, 0d);
        var before = session.AnalyticsPlacements.GetPlacement(item);
        var target = engine.GetAnalyticsInteractionTargets(frame.Layout).Single();
        var pointer = new PointD(
            target.ViewportBounds.Left + 40d,
            target.ViewportBounds.Top + 40d);

        Assert.IsTrue(session.AnalyticsInteraction.TryBeginTransform(
            pointer,
            [target]));
        Assert.IsTrue(session.AnalyticsInteraction.UpdateTransform(
            new PointD(pointer.X + 22.75d, pointer.Y + 13.5d)));

        var previewFrame = engine.Compose(0d, 0d, 640d, 420d, 0d);
        var previewTarget = engine.GetAnalyticsInteractionTargets(previewFrame.Layout)
            .Single();

        Assert.AreEqual(before, session.AnalyticsPlacements.GetPlacement(item));
        Assert.AreEqual(
            before.DocumentBounds.X + 22.75d,
            previewTarget.DocumentBounds.X,
            1e-9);
        Assert.AreEqual(
            before.DocumentBounds.Y + 13.5d,
            previewTarget.DocumentBounds.Y,
            1e-9);
        Assert.IsTrue(EnumerateCommands(previewFrame.DisplayList)
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Trend"));
    }

    [TestMethod]
    public void ActiveWorksheetChangeClearsAnalyticsInteractionSelection()
    {
        var workbook = new Workbook();
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        var selected = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        Assert.IsTrue(session.AnalyticsInteraction.Select(selected));

        session.ActivateWorksheet(second);

        Assert.IsNull(session.AnalyticsInteraction.SelectedItem);
        Assert.IsFalse(session.AnalyticsInteraction.IsTransforming);
    }

    private static SpreadsheetSession CreateSessionWithSource()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Quarter");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(1, 0), "Q1");
        sheet.SetValue(new CellAddress(1, 1), 12d);
        sheet.SetValue(new CellAddress(2, 0), "Q2");
        sheet.SetValue(new CellAddress(2, 1), 18d);
        session.Selection.Select(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(2, 1)));
        return session;
    }

    private static IEnumerable<RenderCommand> EnumerateCommands(
        DisplayList displayList)
    {
        foreach (var command in displayList.Commands)
        {
            yield return command;
            if (command is not DrawDisplayListCommand nested)
            {
                continue;
            }

            foreach (var child in EnumerateCommands(nested.DisplayList))
            {
                yield return child;
            }
        }
    }
}
