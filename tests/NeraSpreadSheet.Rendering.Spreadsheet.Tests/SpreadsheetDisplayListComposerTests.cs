using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetDisplayListComposerTests
{
    [TestMethod]
    public void ComposeIncludesTextAndSelectionCommandsForVisibleCell()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Nera");
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(
            rows,
            columns).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(320d, 200d),
                0d));
        var selection = new SelectionModel();
        selection.SetActiveCell(new CellAddress(0, 0));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(sheet),
            layout,
            selection.Capture());

        Assert.IsTrue(
            displayList.Commands
                .OfType<DrawTextCommand>()
                .Any(command => command.Text == "Nera"));
        Assert.IsTrue(
            displayList.Commands
                .OfType<DrawLineCommand>()
                .Count() >= 4);
    }

    [TestMethod]
    public void ConditionalFormattingProducesVisibleFillAndFontCommands()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, 12d);
        var fillColor = new ColorRgba(245, 210, 70);
        var fontColor = new ColorRgba(20, 80, 180);
        var styleId = sheet.DifferentialStyles.Intern(
            new CellStylePatch
            {
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Color = fillColor,
                },
                FontColor = fontColor,
                FontWeight = 700,
            });
        sheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=10",
                formula2: null,
                styleId,
                priority: 1));

        var rows = new SparseAxisMetricIndex(10, 20d);
        var columns = new SparseAxisMetricIndex(10, 80d);
        var layout = new ViewportLayoutEngine(
            rows,
            columns).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(160d, 80d),
                0d));
        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(sheet),
            layout,
            styles: workbook.Styles);

        Assert.IsTrue(
            displayList.Commands
                .OfType<FillRectangleCommand>()
                .Any(command =>
                    command.Bounds ==
                        new RectD(0d, 0d, 80d, 20d) &&
                    command.Color == fillColor));
        var text = displayList.Commands
            .OfType<DrawTextCommand>()
            .Single(command => command.Text == "12");
        Assert.AreEqual(fontColor, text.Style.Color);
        Assert.AreEqual(700, text.Style.FontWeight);
    }
}
