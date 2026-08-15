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
        var cached = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions
            {
                Enabled = true,
                ScrollTileSize = 256d,
                MaxEntries = 4,
            });
        var uncached = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions { Enabled = false });

        cached.Compose(10d, 10d, 1000d, 700d);
        uncached.Compose(10d, 10d, 1000d, 700d);
        RunFrames(cached, 2);
        RunFrames(uncached, 2);

        cached.ClearDisplayListCache();
        cached.Compose(10d, 10d, 1000d, 700d);
        var uncachedBytes = MeasureAllocatedBytes(() => RunFrames(uncached, 32));
        var cachedBytes = MeasureAllocatedBytes(() => RunFrames(cached, 32));

        Assert.IsTrue(
            cachedBytes < uncachedBytes,
            $"Expected cached scrolling to allocate less memory. Cached={cachedBytes}, uncached={uncachedBytes}.");
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
