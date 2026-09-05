using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Benchmarks;

[MemoryDiagnoser]
public class SparseWholeAxisStyleBenchmarks
{
    private readonly ColorRgba _fill = new(35, 125, 205);
    private Workbook _workbook = null!;
    private WorksheetSnapshot _snapshot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _workbook = new Workbook();
        var session = new SpreadsheetSession(_workbook);
        session.Selection.SelectRow(500);
        session.Styles.SetFill(_fill);
        _snapshot = WorksheetSnapshot.Capture(session.ActiveWorksheet);
    }

    [Benchmark]
    public int ApplyWholeRowFillWithoutCellMaterialization()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Selection.SelectRow(500);
        session.Styles.SetFill(_fill);
        return session.ActiveWorksheet.RowStyleSpanCount +
            session.ActiveWorksheet.UsedCellCount;
    }

    [Benchmark]
    public int ResolveOneThousandSnapshotStyles()
    {
        var checksum = 0;
        for (var column = 0; column < 1_000; column++)
        {
            checksum += _snapshot.GetEffectiveStyle(
                new CellAddress(500, column),
                _workbook.Styles).Fill.Color.Blue;
        }
        return checksum;
    }
}
