using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

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
    public void AxisHitTestsUseSameFreezeTransformAsCellHitTesting()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);

        Assert.IsTrue(engine.TryHitTestRow(10d, 40d, out var frozenRow));
        Assert.IsTrue(engine.TryHitTestRow(30d, 40d, out var scrollingRow));
        Assert.IsTrue(engine.TryHitTestColumn(10d, 100d, out var frozenColumn));
        Assert.IsTrue(engine.TryHitTestColumn(90d, 100d, out var scrollingColumn));

        Assert.AreEqual(0, frozenRow);
        Assert.AreEqual(3, scrollingRow);
        Assert.AreEqual(0, frozenColumn);
        Assert.AreEqual(2, scrollingColumn);
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
    public void FrozenViewportUsesPaneAwareTranslationCache()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(new CellAddress(1, 1), "Nera");
        var session = new SpreadsheetSession(workbook);
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);
        var theme = new SpreadsheetRenderTheme();

        engine.Compose(10d, 5d, 400d, 240d, 0d, theme);
        var second = engine.Compose(20d, 8d, 400d, 240d, 0d, theme);

        Assert.AreEqual(1, engine.DisplayListCacheEntryCount);
        Assert.AreEqual(1L, engine.DisplayListCacheHitCount);
        Assert.AreEqual(1L, engine.DisplayListCacheMissCount);
        Assert.IsTrue(second.DisplayList.Commands.OfType<PushTranslationCommand>().Any());
        Assert.AreEqual(
            2,
            second.DisplayList.Commands.OfType<DrawLineCommand>().Count(command => command.Color == theme.FreezePaneLine));
    }

    [TestMethod]
    public void FreezeConfigurationParticipatesInCacheIdentity()
    {
        var session = new SpreadsheetSession(new Workbook());
        var engine = new SpreadsheetViewportEngine(session);

        session.View.SetFrozenPanes(1, 1);
        engine.Compose(10d, 5d, 400d, 240d, 0d);
        session.View.SetFrozenPanes(2, 1);
        engine.Compose(10d, 5d, 400d, 240d, 0d);

        Assert.AreEqual(2L, engine.DisplayListCacheMissCount);
        Assert.AreEqual(2, engine.DisplayListCacheEntryCount);
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
