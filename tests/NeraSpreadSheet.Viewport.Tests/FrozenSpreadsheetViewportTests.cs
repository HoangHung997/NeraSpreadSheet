using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class FrozenSpreadsheetViewportTests
{
    [TestMethod]
    public void HitTestKeepsFrozenPaneCoordinatesOutOfScrollTransform()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);

        Assert.IsTrue(engine.TryHitTest(10d, 10d, 100d, 40d, out var frozen));
        Assert.IsTrue(engine.TryHitTest(90d, 30d, 100d, 40d, out var scrolling));

        Assert.AreEqual(new CellAddress(0, 0), frozen);
        Assert.AreEqual(new CellAddress(3, 2), scrolling);
    }

    [TestMethod]
    public void CellBoundsKeepFrozenCellFixedAndScrollableCellFractional()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);

        Assert.IsTrue(engine.TryGetCellBounds(new CellAddress(0, 0), 13.25d, 7.75d, out var frozen));
        Assert.IsTrue(engine.TryGetCellBounds(new CellAddress(1, 1), 13.25d, 7.75d, out var scrolling));

        Assert.AreEqual(0d, frozen.X, 1e-9);
        Assert.AreEqual(0d, frozen.Y, 1e-9);
        Assert.AreEqual(66.75d, scrolling.X, 1e-9);
        Assert.AreEqual(12.25d, scrolling.Y, 1e-9);
    }

    [TestMethod]
    public void FrozenViewportBypassesWholeFrameTranslationCache()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(new CellAddress(1, 1), "Nera");
        var session = new SpreadsheetSession(workbook);
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);

        engine.Compose(10d, 5d, 400d, 240d, 0d);
        engine.Compose(20d, 8d, 400d, 240d, 0d);

        Assert.AreEqual(0, engine.DisplayListCacheEntryCount);
        Assert.AreEqual(0L, engine.DisplayListCacheHitCount);
        Assert.AreEqual(0L, engine.DisplayListCacheMissCount);
    }

    [TestMethod]
    public void ComposeMarksFrozenGeometryAndPreservesContinuousScroll()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);

        var frame = engine.Compose(13.25d, 7.75d, 320d, 200d, 0d);

        Assert.AreEqual(80d, frame.Layout.FrozenWidth, 1e-9);
        Assert.AreEqual(20d, frame.Layout.FrozenHeight, 1e-9);
        Assert.AreEqual(13.25d, frame.Layout.ScrollX, 1e-9);
        Assert.AreEqual(7.75d, frame.Layout.ScrollY, 1e-9);
        Assert.IsTrue(frame.DisplayList.Commands.OfType<PushClipCommand>().Count() >= 2);
    }
}
