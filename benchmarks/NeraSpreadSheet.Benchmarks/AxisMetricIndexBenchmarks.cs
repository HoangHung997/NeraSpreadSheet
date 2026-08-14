using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Benchmarks;

[MemoryDiagnoser]
public class AxisMetricIndexBenchmarks
{
    private SparseAxisMetricIndex _rows = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rows = new SparseAxisMetricIndex(1_048_576, 20d);

        for (var row = 0; row < 1_048_576; row += 997)
        {
            _rows.SetSize(row, row % 2 == 0 ? 0d : 36d);
        }
    }

    [Benchmark]
    public int FindRowNearMiddle() => _rows.FindIndexAtOffset(10_000_000.25d);

    [Benchmark]
    public IReadOnlyList<AxisSlot> GetVisibleRows() => _rows.GetSlots(10_000_000.25d, 2_160d, 128d);
}
