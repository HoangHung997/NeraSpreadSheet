using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Benchmarks;

[MemoryDiagnoser]
public class ViewportComposeBenchmarks
{
    private SpreadsheetViewportEngine _cached = null!;
    private SpreadsheetViewportEngine _uncached = null!;

    [GlobalSetup]
    public void Setup()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var changes = new List<KeyValuePair<CellAddress, CellData>>(4_000);
        for (var row = 0; row < 200; row++)
        {
            for (var column = 0; column < 20; column++)
            {
                changes.Add(new KeyValuePair<CellAddress, CellData>(
                    new CellAddress(row, column),
                    new CellData(CellValue.FromNumber((row * 20d) + column))));
            }
        }
        sheet.SetCells(changes);

        _cached = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions
            {
                Enabled = true,
                ScrollTileSize = 256d,
                MaxEntries = 8,
            });
        _uncached = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions { Enabled = false });
    }

    [Benchmark(Baseline = true)]
    public int Compose120FramesWithoutDisplayListCache()
    {
        return Compose120Frames(_uncached, clearCacheFirst: false);
    }

    [Benchmark]
    public int Compose120FramesWithDisplayListCache()
    {
        return Compose120Frames(_cached, clearCacheFirst: true);
    }

    private static int Compose120Frames(SpreadsheetViewportEngine engine, bool clearCacheFirst)
    {
        if (clearCacheFirst)
        {
            engine.ClearDisplayListCache();
        }

        var commandCount = 0;
        for (var frame = 0; frame < 120; frame++)
        {
            var offset = 10d + frame;
            commandCount += engine.Compose(offset, offset * 0.5d, 1200d, 800d).DisplayList.Count;
        }
        return commandCount;
    }
}
