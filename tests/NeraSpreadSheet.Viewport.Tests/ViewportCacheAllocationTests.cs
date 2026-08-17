using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class ViewportCacheAllocationTests
{
    [TestMethod]
    public void CachedFractionalScrollAllocatesLessThanUncachedComposition()
    {
        var workbook = CreatePopulatedWorkbook();
        var cached = CreateEngine(workbook, cacheEnabled: true);
        var uncached = CreateEngine(workbook, cacheEnabled: false);

        WarmUp(cached, uncached);

        var uncachedBytes = MeasureAllocatedBytes(() => RunFrames(uncached, 32));
        var cachedBytes = MeasureAllocatedBytes(() => RunFrames(cached, 32));

        Assert.IsTrue(
            cachedBytes < uncachedBytes,
            $"Expected cached scrolling to allocate less memory. Cached={cachedBytes}, uncached={uncachedBytes}.");
    }

    [TestMethod]
    public void CachedFrozenFractionalScrollAllocatesLessThanFreshPaneComposition()
    {
        var workbook = CreatePopulatedWorkbook();
        var cachedSession = new SpreadsheetSession(workbook);
        var uncachedSession = new SpreadsheetSession(workbook);
        cachedSession.View.SetFrozenPanes(2, 2);
        uncachedSession.View.SetFrozenPanes(2, 2);
        var cached = new SpreadsheetViewportEngine(
            cachedSession,
            new SpreadsheetViewportCacheOptions
            {
                Enabled = true,
                ScrollTileSize = 256d,
                MaxEntries = 4,
            });
        var uncached = new SpreadsheetViewportEngine(
            uncachedSession,
            new SpreadsheetViewportCacheOptions { Enabled = false });

        WarmUp(cached, uncached);

        var uncachedBytes = MeasureAllocatedBytes(() => RunFrames(uncached, 32));
        var cachedBytes = MeasureAllocatedBytes(() => RunFrames(cached, 32));

        Assert.IsTrue(
            cachedBytes < uncachedBytes,
            $"Expected pane-aware cached frozen scrolling to allocate less memory. Cached={cachedBytes}, uncached={uncachedBytes}.");
        Assert.IsTrue(cached.DisplayListCacheHitCount > 0L);
    }

    private static SpreadsheetViewportEngine CreateEngine(Workbook workbook, bool cacheEnabled) =>
        new(
            new SpreadsheetSession(workbook),
            cacheEnabled
                ? new SpreadsheetViewportCacheOptions
                {
                    Enabled = true,
                    ScrollTileSize = 256d,
                    MaxEntries = 4,
                }
                : new SpreadsheetViewportCacheOptions { Enabled = false });

    private static void WarmUp(SpreadsheetViewportEngine cached, SpreadsheetViewportEngine uncached)
    {
        cached.Compose(10d, 10d, 1000d, 700d);
        uncached.Compose(10d, 10d, 1000d, 700d);
        RunFrames(cached, 2);
        RunFrames(uncached, 2);
        cached.ClearDisplayListCache();
        cached.Compose(10d, 10d, 1000d, 700d);
    }

    private static Workbook CreatePopulatedWorkbook()
    {
        var workbook = new Workbook();
        var changes = new List<KeyValuePair<CellAddress, CellData>>(1_800);
        for (var row = 0; row < 90; row++)
        {
            for (var column = 0; column < 20; column++)
            {
                changes.Add(new KeyValuePair<CellAddress, CellData>(
                    new CellAddress(row, column),
                    new CellData(CellValue.FromNumber((row * 20d) + column))));
            }
        }
        workbook.Worksheets[0].SetCells(changes);
        return workbook;
    }

    private static void RunFrames(SpreadsheetViewportEngine engine, int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            var scrollX = 20d + frame;
            var scrollY = 10d + (frame * 0.5d);
            engine.Compose(scrollX, scrollY, 1000d, 700d);
        }
    }

    private static long MeasureAllocatedBytes(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
