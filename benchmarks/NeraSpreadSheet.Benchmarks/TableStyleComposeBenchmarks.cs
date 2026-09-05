using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Benchmarks;

[MemoryDiagnoser]
public class TableStyleComposeBenchmarks
{
    private SpreadsheetViewportEngine _styledEngine = null!;
    private SpreadsheetViewportEngine _unstyledEngine = null!;

    [GlobalSetup]
    public void Setup()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "LargeTable",
            new CellRange(
                default,
                new CellAddress(SpreadsheetLimits.MaxRows - 1, 19)),
            Enumerable.Range(0, 20).Select(index =>
                new SpreadsheetTableColumn(Guid.NewGuid(), $"Column{index + 1}")),
            styleName: "TableStyleMedium2",
            showFirstColumn: true,
            showLastColumn: true,
            showRowStripes: true,
            showColumnStripes: true));
        var cells = new List<KeyValuePair<CellAddress, CellData>>(4_000);
        for (var row = 0; row < 200; row++)
        {
            for (var column = 0; column < 20; column++)
            {
                cells.Add(new KeyValuePair<CellAddress, CellData>(
                    new CellAddress(row, column),
                    new CellData(CellValue.FromNumber((row * 20d) + column))));
            }
        }
        worksheet.SetCells(cells);
        _styledEngine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook),
            new SpreadsheetViewportCacheOptions { Enabled = false });

        var unstyledWorkbook = new Workbook();
        unstyledWorkbook.Worksheets[0].SetCells(cells);
        _unstyledEngine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(unstyledWorkbook),
            new SpreadsheetViewportCacheOptions { Enabled = false });
    }

    [Benchmark(Baseline = true)]
    public int ComposeOneHundredTwentyUnstyledFrames() =>
        ComposeFrames(_unstyledEngine);

    [Benchmark]
    public int ComposeOneHundredTwentyStyledFrames() =>
        ComposeFrames(_styledEngine);

    private static int ComposeFrames(SpreadsheetViewportEngine engine)
    {
        var commands = 0;
        for (var frame = 0; frame < 120; frame++)
        {
            commands += engine.Compose(
                scrollX: frame * 0.75d,
                scrollY: frame * 1.5d,
                viewportWidth: 1200d,
                viewportHeight: 800d,
                overscan: 128d).DisplayList.Count;
        }
        return commands;
    }
}
