using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsOverlayDisplayListComposerTests
{
    [TestMethod]
    public void OverlayUsesViewportTranslationAndSelectionHandles()
    {
        var worksheet = new Workbook().Worksheets[0];
        PopulateSource(worksheet);
        var chart = new SpreadsheetChartDefinition(
            Guid.NewGuid(),
            "Chart1",
            SpreadsheetChartType.Column,
            SourceRange(),
            "Sales");
        var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var placement = new SpreadsheetAnalyticsPlacement(
            item,
            new RectD(50d, 60d, 300d, 180d),
            0);
        var layout = Layout(10d, 20d, 500d, 320d);

        var displayList = SpreadsheetAnalyticsOverlayDisplayListComposer.Compose(
            worksheet,
            [chart],
            [],
            [placement],
            layout,
            item);

        var translation = displayList.Commands
            .OfType<PushTranslationCommand>()
            .Single();
        Assert.AreEqual(40d, translation.DeltaX);
        Assert.AreEqual(40d, translation.DeltaY);
        Assert.AreEqual(
            1,
            displayList.Commands.OfType<DrawDisplayListCommand>().Count());
        Assert.AreEqual(
            8,
            displayList.Commands.OfType<FillRectangleCommand>().Count());
        Assert.AreEqual(
            4,
            displayList.Commands
                .OfType<DrawLineCommand>()
                .Count(command => command.StrokeWidth ==
                                  SpreadsheetAnalyticsOverlayDisplayListComposer.SelectionStrokeWidth));
    }

    [TestMethod]
    public void OverlayDuplicatesContentOnlyForVisibleFreezeFragments()
    {
        var worksheet = new Workbook().Worksheets[0];
        PopulateSource(worksheet);
        var pivot = new SpreadsheetPivotDefinition(
            Guid.NewGuid(),
            "Pivot1",
            SourceRange(),
            0,
            1);
        var placement = new SpreadsheetAnalyticsPlacement(
            SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id),
            new RectD(80d, 30d, 100d, 100d),
            0);
        var layout = Layout(
            40d,
            20d,
            300d,
            200d,
            100d,
            50d);

        var displayList = SpreadsheetAnalyticsOverlayDisplayListComposer.Compose(
            worksheet,
            [],
            [pivot],
            [placement],
            layout);

        Assert.AreEqual(
            4,
            displayList.Commands.OfType<DrawDisplayListCommand>().Count());
        Assert.AreEqual(
            4,
            displayList.Commands.OfType<PushClipCommand>().Count());
        Assert.AreEqual(
            4,
            displayList.Commands.OfType<PushTranslationCommand>().Count());
    }

    [TestMethod]
    public void MissingDefinitionIsIgnoredWithoutLeakingRenderState()
    {
        var worksheet = new Workbook().Worksheets[0];
        var placement = new SpreadsheetAnalyticsPlacement(
            SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid()),
            new RectD(10d, 10d, 240d, 160d),
            0);

        var displayList = SpreadsheetAnalyticsOverlayDisplayListComposer.Compose(
            worksheet,
            [],
            [],
            [placement],
            Layout(0d, 0d, 300d, 200d));

        Assert.AreEqual(0, displayList.Commands.Count);
    }

    private static void PopulateSource(Worksheet sheet)
    {
        sheet.SetValue(new CellAddress(0, 0), "Region");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(1, 0), "North");
        sheet.SetValue(new CellAddress(1, 1), 10d);
        sheet.SetValue(new CellAddress(2, 0), "South");
        sheet.SetValue(new CellAddress(2, 1), 5d);
        sheet.SetValue(new CellAddress(3, 0), "North");
        sheet.SetValue(new CellAddress(3, 1), 7d);
    }

    private static CellRange SourceRange() =>
        new(new CellAddress(0, 0), new CellAddress(3, 1));

    private static ViewportLayout Layout(
        double scrollX,
        double scrollY,
        double width,
        double height,
        double frozenWidth = 0d,
        double frozenHeight = 0d) =>
        new(
            scrollX,
            scrollY,
            new SizeD(width, height),
            2000d,
            2000d,
            frozenWidth,
            frozenHeight,
            [],
            []);
}
