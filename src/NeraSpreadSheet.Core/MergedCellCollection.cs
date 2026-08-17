namespace NeraSpreadSheet.Core;

public sealed class MergedCellRanges
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

    internal CellRange[] CreateStructuralRanges(WorksheetStructuralChange change)
    {
        var transformed = new List<CellRange>(_ranges.Count);
        foreach (var range in _ranges)
        {
            if (!change.TryMapRange(range, out var mapped))
            {
                if (change.Kind == WorksheetStructuralChangeKind.Insert)
                {
                    throw new InvalidOperationException(
                        "Cannot insert because a merged range would move outside the worksheet bounds.");
                }
                continue;
            }

            if (mapped.RowCount == 1 && mapped.ColumnCount == 1)
            {
                continue;
            }
            transformed.Add(mapped);
        }
        return [.. transformed];
    }

    internal void ReplaceAll(IReadOnlyList<CellRange> ranges)
    {
        _ranges.Clear();
        foreach (var range in ranges)
        {
            Add(range);
        }
    }

    private static bool Overlaps(CellRange first, CellRange second) =>
        first.Left <= second.Right &&
        first.Right >= second.Left &&
        first.Top <= second.Bottom &&
        first.Bottom >= second.Top;
}
