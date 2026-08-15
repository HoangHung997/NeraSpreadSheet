using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetViewportEngineTests
{
    [TestMethod]
    public void ComposePreservesFractionalPixelOffset()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "Nera");
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var frame = engine.Compose(13.25d, 7.75d, 320d, 180d, 0d);

        Assert.AreEqual(13.25d, frame.Layout.ScrollX, 1e-9);
        Assert.AreEqual(7.75d, frame.Layout.ScrollY, 1e-9);
        Assert.IsTrue(frame.DisplayList.Commands.OfType<DrawTextCommand>().Any(command => command.Text == "Nera"));
    }

    [TestMethod]
    public void HitTestAccountsForScrollAndDimensionOverrides()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.Dimensions.SetRowHeight(0, 40d);
        sheet.Dimensions.SetColumnWidth(0, 100d);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var hit = engine.TryHitTest(10d, 10d, 100d, 40d, out var address);

        Assert.IsTrue(hit);
        Assert.AreEqual(new CellAddress(1, 1), address);
    }

    [TestMethod]
    public void ContentExtentReflectsSparseDimensionOverrides()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var before = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook)).GetContentExtent();
        sheet.Dimensions.SetColumnWidth(0, sheet.Dimensions.DefaultColumnWidth + 25d);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var after = engine.GetContentExtent();

        Assert.AreEqual(before.Width + 25d, after.Width, 1e-9);
    }
}
