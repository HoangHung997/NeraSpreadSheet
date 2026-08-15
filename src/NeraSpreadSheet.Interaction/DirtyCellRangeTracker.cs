using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Interaction;

public sealed class DirtyCellRangeTracker
{
    private readonly List<CellRange> _ranges = [];

    public int Count => _ranges.Count;

    public bool IsEmpty => _ranges.Count == 0;

    public void Add(CellAddress address) => Add(new CellRange(address, address));

    public void Add(CellRange range)
    {
        var merged = range;
        for (var index = _ranges.Count - 1; index >= 0; index--)
        {
            var existing = _ranges[index];
            if (!Touches(existing, merged))
            {
                continue;
            }

            merged = Union(existing, merged);
            _ranges.RemoveAt(index);
        }

        _ranges.Add(merged);
    }

    public IReadOnlyList<CellRange> Peek() => _ranges.ToArray();

    public IReadOnlyList<CellRange> Drain()
    {
        var result = _ranges.ToArray();
        _ranges.Clear();
        return result;
    }

    public void Clear() => _ranges.Clear();

    private static bool Touches(CellRange first, CellRange second) =>
        second.Left <= first.Right + 1L && second.Right + 1L >= first.Left &&
        second.Top <= first.Bottom + 1L && second.Bottom + 1L >= first.Top;

    private static CellRange Union(CellRange first, CellRange second) => new(
        new CellAddress(Math.Min(first.Top, second.Top), Math.Min(first.Left, second.Left)),
        new CellAddress(Math.Max(first.Bottom, second.Bottom), Math.Max(first.Right, second.Right)));
}
