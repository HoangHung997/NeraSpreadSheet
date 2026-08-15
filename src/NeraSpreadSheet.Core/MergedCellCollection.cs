namespace NeraSpreadSheet.Core;

public sealed class MergedCellCollection
{
    private readonly List<CellRange> _ranges = [];

    public int Count => _ranges.Count;

    public IReadOnlyList<CellRange> Ranges => _ranges;

    public bool TryGetContaining(CellAddress address, out CellRange range)
    {
        foreach (var candidate in _ranges)
        {
            if (candidate.Contains(address))
            {
                range = candidate;
                return true;
            }
        }

        range = default;
        return false;
    }

    public bool Intersects(CellRange range)
    {
        foreach (var existing in _ranges)
        {
            if (Overlaps(existing, range))
            {
                return true;
            }
        }

        return false;
    }

    internal void Add(CellRange range)
    {
        if (range.RowCount == 1 && range.ColumnCount == 1)
        {
            throw new ArgumentException("A merged range must contain more than one cell.", nameof(range));
        }

        if (Intersects(range))
        {
            throw new InvalidOperationException("Merged cell ranges cannot overlap.");
        }

        _ranges.Add(range);
    }

    internal bool Remove(CellRange range) => _ranges.Remove(range);

    private static bool Overlaps(CellRange first, CellRange second) =>
        first.Left <= second.Right &&
        first.Right >= second.Left &&
        first.Top <= second.Bottom &&
        first.Bottom >= second.Top;
}
