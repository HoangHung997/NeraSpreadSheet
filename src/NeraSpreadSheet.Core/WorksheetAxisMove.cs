namespace NeraSpreadSheet.Core;

public readonly record struct WorksheetAxisInterval
{
    public WorksheetAxisInterval(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        Start = start;
        End = end;
    }

    public int Start { get; }

    public int End { get; }

    public int Length => checked(End - Start + 1);
}

public readonly record struct WorksheetAxisMove
{
    public WorksheetAxisMove(
        WorksheetAxis axis,
        int sourceIndex,
        int count,
        int destinationBoundary)
    {
        if (!Enum.IsDefined(axis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }

        var axisLength = axis == WorksheetAxis.Row
            ? SpreadsheetLimits.MaxRows
            : SpreadsheetLimits.MaxColumns;
        if (sourceIndex < 0 || sourceIndex >= axisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            count,
            axisLength - sourceIndex);
        if (destinationBoundary < 0 || destinationBoundary > axisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationBoundary));
        }

        Axis = axis;
        SourceIndex = sourceIndex;
        Count = count;
        DestinationBoundary = destinationBoundary;
        AxisLength = axisLength;
    }

    public WorksheetAxis Axis { get; }

    public int SourceIndex { get; }

    public int Count { get; }

    public int DestinationBoundary { get; }

    public int AxisLength { get; }

    public int SourceEndIndex => checked(SourceIndex + Count - 1);

    public bool IsNoOp =>
        DestinationBoundary >= SourceIndex &&
        DestinationBoundary <= SourceEndIndex + 1;

    public int InsertionIndex => IsNoOp
        ? SourceIndex
        : DestinationBoundary < SourceIndex
            ? DestinationBoundary
            : DestinationBoundary - Count;

    public int AffectedStartIndex => Math.Min(SourceIndex, InsertionIndex);

    public int AffectedEndIndex => Math.Max(
        SourceEndIndex,
        checked(InsertionIndex + Count - 1));

    public int MapIndex(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= AxisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        }
        if (IsNoOp)
        {
            return sourceIndex;
        }

        if (DestinationBoundary < SourceIndex)
        {
            if (sourceIndex >= SourceIndex && sourceIndex <= SourceEndIndex)
            {
                return DestinationBoundary + (sourceIndex - SourceIndex);
            }
            if (sourceIndex >= DestinationBoundary && sourceIndex < SourceIndex)
            {
                return sourceIndex + Count;
            }
            return sourceIndex;
        }

        if (sourceIndex >= SourceIndex && sourceIndex <= SourceEndIndex)
        {
            return InsertionIndex + (sourceIndex - SourceIndex);
        }
        if (sourceIndex > SourceEndIndex && sourceIndex < DestinationBoundary)
        {
            return sourceIndex - Count;
        }
        return sourceIndex;
    }

    public CellAddress MapAddress(CellAddress source) =>
        Axis == WorksheetAxis.Row
            ? new CellAddress(MapIndex(source.RowIndex), source.ColumnIndex)
            : new CellAddress(source.RowIndex, MapIndex(source.ColumnIndex));

    public WorksheetAxisInterval[] MapInterval(int start, int end)
    {
        if (start < 0 || end < start || end >= AxisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (IsNoOp)
        {
            return [new WorksheetAxisInterval(start, end)];
        }

        var mapped = new List<WorksheetAxisInterval>(4);
        if (DestinationBoundary < SourceIndex)
        {
            AddMappedSegment(mapped, start, end, 0, DestinationBoundary - 1, 0);
            AddMappedSegment(
                mapped,
                start,
                end,
                DestinationBoundary,
                SourceIndex - 1,
                Count);
            AddMappedSegment(
                mapped,
                start,
                end,
                SourceIndex,
                SourceEndIndex,
                DestinationBoundary - SourceIndex);
            AddMappedSegment(
                mapped,
                start,
                end,
                SourceEndIndex + 1,
                AxisLength - 1,
                0);
        }
        else
        {
            AddMappedSegment(mapped, start, end, 0, SourceIndex - 1, 0);
            AddMappedSegment(
                mapped,
                start,
                end,
                SourceIndex,
                SourceEndIndex,
                InsertionIndex - SourceIndex);
            AddMappedSegment(
                mapped,
                start,
                end,
                SourceEndIndex + 1,
                DestinationBoundary - 1,
                -Count);
            AddMappedSegment(
                mapped,
                start,
                end,
                DestinationBoundary,
                AxisLength - 1,
                0);
        }

        mapped.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        if (mapped.Count <= 1)
        {
            return [.. mapped];
        }

        var merged = new List<WorksheetAxisInterval>(mapped.Count);
        var current = mapped[0];
        for (var index = 1; index < mapped.Count; index++)
        {
            var next = mapped[index];
            if (next.Start <= current.End + 1)
            {
                current = new WorksheetAxisInterval(
                    current.Start,
                    Math.Max(current.End, next.End));
                continue;
            }

            merged.Add(current);
            current = next;
        }
        merged.Add(current);
        return [.. merged];
    }

    public bool TryMapContiguousInterval(
        int start,
        int end,
        out int mappedStart,
        out int mappedEnd)
    {
        var intervals = MapInterval(start, end);
        if (intervals.Length != 1)
        {
            mappedStart = default;
            mappedEnd = default;
            return false;
        }

        mappedStart = intervals[0].Start;
        mappedEnd = intervals[0].End;
        return true;
    }

    public bool TryMapContiguousRange(CellRange source, out CellRange target)
    {
        var start = Axis == WorksheetAxis.Row ? source.Top : source.Left;
        var end = Axis == WorksheetAxis.Row ? source.Bottom : source.Right;
        if (!TryMapContiguousInterval(start, end, out var mappedStart, out var mappedEnd))
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

    private static void AddMappedSegment(
        List<WorksheetAxisInterval> result,
        int requestedStart,
        int requestedEnd,
        int segmentStart,
        int segmentEnd,
        int delta)
    {
        if (segmentStart > segmentEnd)
        {
            return;
        }

        var intersectionStart = Math.Max(requestedStart, segmentStart);
        var intersectionEnd = Math.Min(requestedEnd, segmentEnd);
        if (intersectionStart > intersectionEnd)
        {
            return;
        }

        result.Add(new WorksheetAxisInterval(
            intersectionStart + delta,
            intersectionEnd + delta));
    }
}
