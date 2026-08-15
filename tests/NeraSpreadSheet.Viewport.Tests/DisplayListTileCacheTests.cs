using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class DisplayListTileCacheTests
{
    [TestMethod]
    public void FractionalScrollWithinTileReusesCachedDisplayList()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "Nera");
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions { ScrollTileSize = 256d, MaxEntries = 4 });

        engine.Compose(10.5d, 12.25d, 500d, 300d, 0d);
        var second = engine.Compose(20.75d, 19.5d, 500d, 300d, 0d);

        Assert.AreEqual(1L, engine.DisplayListCacheMissCount);
        Assert.AreEqual(1L, engine.DisplayListCacheHitCount);
        var translation = second.DisplayList.Commands.OfType<PushTranslationCommand>().First();
        Assert.AreEqual(-20.75d, translation.DeltaX, 1e-9);
        Assert.AreEqual(-19.5d, translation.DeltaY, 1e-9);
        Assert.AreEqual(20.75d, second.Layout.ScrollX, 1e-9);
        Assert.AreEqual(19.5d, second.Layout.ScrollY, 1e-9);
    }

    [TestMethod]
    public void CrossingScrollTileBuildsAnotherDisplayList()
    {
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(new Workbook()),
            new SpreadsheetViewportCacheOptions { ScrollTileSize = 128d, MaxEntries = 4 });

        engine.Compose(10d, 10d, 400d, 250d, 0d);
        engine.Compose(140d, 10d, 400d, 250d, 0d);

        Assert.AreEqual(2L, engine.DisplayListCacheMissCount);
        Assert.AreEqual(0L, engine.DisplayListCacheHitCount);
        Assert.AreEqual(2, engine.DisplayListCacheEntryCount);
    }

    [TestMethod]
    public void WorksheetAndSelectionChangesInvalidateCacheKey()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var engine = new SpreadsheetViewportEngine(session);

        engine.Compose(10d, 10d, 400d, 250d, 0d);
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        engine.Compose(10d, 10d, 400d, 250d, 0d);
        workbook.Worksheets[0].SetValue(default, "Changed");
        engine.Compose(10d, 10d, 400d, 250d, 0d);

        Assert.AreEqual(3L, engine.DisplayListCacheMissCount);
    }

    [TestMethod]
    public void CacheRespectsEntryLimit()
    {
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(new Workbook()),
            new SpreadsheetViewportCacheOptions { ScrollTileSize = 64d, MaxEntries = 2 });

        engine.Compose(1d, 1d, 300d, 200d, 0d);
        engine.Compose(70d, 1d, 300d, 200d, 0d);
        engine.Compose(140d, 1d, 300d, 200d, 0d);

        Assert.AreEqual(2, engine.DisplayListCacheEntryCount);
        Assert.AreEqual(3L, engine.DisplayListCacheMissCount);
    }
}
