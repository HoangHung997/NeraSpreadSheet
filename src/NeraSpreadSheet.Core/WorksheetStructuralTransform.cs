namespace NeraSpreadSheet.Core;

public enum WorksheetStructuralChangeKind
{
    Insert,
    Delete,
}

public readonly record struct WorksheetStructuralChange
{
    public WorksheetStructuralChange(
        WorksheetAxis axis,
        WorksheetStructuralChangeKind kind,
        int index,
        int count)
    {
        var axisLength = axis == WorksheetAxis.Row
            ? SpreadsheetLimits.MaxRows
            : SpreadsheetLimits.MaxColumns;
        if (index < 0 || index >= axisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, axisLength - index);

        Axis = axis;
        Kind = kind;
        Index = index;
        Count = count;
        AxisLength = axisLength;
    }

    public WorksheetAxis Axis { get; }
    public WorksheetStructuralChangeKind Kind { get; }
    public int Index { get; }
    public int Count { get; }
    public int AxisLength { get; }
    public int EndIndex => checked(Index + Count - 1);

    public int MapBoundary(int boundary)
    {
        if (boundary < 0 || boundary >= AxisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(boundary));
        }

        if (Kind == WorksheetStructuralChangeKind.Insert)
        {
            return Index < boundary
                ? Math.Min(AxisLength - 1, checked(boundary + Count))
                : boundary;
        }

        if (boundary <= Index)
        {
            return boundary;
        }

        var deletedBeforeBoundary = Math.Min(Count, boundary - Index);
        return boundary - deletedBeforeBoundary;
    }

    public bool TryMapIndex(int sourceIndex, out int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= AxisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        }

        if (Kind == WorksheetStructuralChangeKind.Insert)
        {
            if (sourceIndex < Index)
            {
                targetIndex = sourceIndex;
                return true;
            }

            var shifted = (long)sourceIndex + Count;
            if (shifted >= AxisLength)
            {
                targetIndex = default;
                return false;
            }
            targetIndex = (int)shifted;
            return true;
        }

        if (sourceIndex >= Index && sourceIndex <= EndIndex)
        {
            targetIndex = default;
            return false;
        }
        targetIndex = sourceIndex > EndIndex ? sourceIndex - Count : sourceIndex;
        return true;
    }

    public bool TryMapAddress(CellAddress source, out CellAddress target)
    {
        var sourceIndex = Axis == WorksheetAxis.Row ? source.RowIndex : source.ColumnIndex;
        if (!TryMapIndex(sourceIndex, out var mappedIndex))
        {
            target = default;
            return false;
        }

        target = Axis == WorksheetAxis.Row
            ? new CellAddress(mappedIndex, source.ColumnIndex)
            : new CellAddress(source.RowIndex, mappedIndex);
        return true;
    }

    public bool TryMapRange(CellRange source, out CellRange target)
    {
        var start = Axis == WorksheetAxis.Row ? source.Top : source.Left;
        var end = Axis == WorksheetAxis.Row ? source.Bottom : source.Right;
        if (!TryMapInterval(start, end, out var mappedStart, out var mappedEnd))
        {
            target = default;
            return false;
        }

        target = Axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(mappedStart, source.Left),
                new CellAddress(mappedEnd, source.Right))
            : new CellRange(
                new CellAddress(source.Top, mappedStart),
                new CellAddress(source.Bottom, mappedEnd));
        return true;
    }

    public bool TryMapInterval(int start, int end, out int mappedStart, out int mappedEnd)
    {
        if (start < 0 || end < start || end >= AxisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (Kind == WorksheetStructuralChangeKind.Insert)
        {
            if (Index <= start)
            {
                var shiftedStart = (long)start + Count;
                var shiftedEnd = (long)end + Count;
                if (shiftedEnd >= AxisLength)
                {
                    mappedStart = default;
                    mappedEnd = default;
                    return false;
                }
                mappedStart = (int)shiftedStart;
                mappedEnd = (int)shiftedEnd;
                return true;
            }

            if (Index <= end)
            {
                var expandedEnd = (long)end + Count;
                if (expandedEnd >= AxisLength)
                {
                    mappedStart = default;
                    mappedEnd = default;
                    return false;
                }
                mappedStart = start;
                mappedEnd = (int)expandedEnd;
                return true;
            }

            mappedStart = start;
            mappedEnd = end;
            return true;
        }

        var deleteStart = Index;
        var deleteEnd = EndIndex;
        if (deleteEnd < start)
        {
            mappedStart = start - Count;
            mappedEnd = end - Count;
            return true;
        }
        if (deleteStart > end)
        {
            mappedStart = start;
            mappedEnd = end;
            return true;
        }

        var hasLowSegment = start < deleteStart;
        var hasHighSegment = end > deleteEnd;
        if (!hasLowSegment && !hasHighSegment)
        {
            mappedStart = default;
            mappedEnd = default;
            return false;
        }

        mappedStart = hasLowSegment ? start : deleteStart;
        mappedEnd = hasHighSegment ? end - Count : deleteStart - 1;
        return mappedStart <= mappedEnd;
    }
}
