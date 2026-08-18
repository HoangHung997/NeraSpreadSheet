namespace NeraSpreadSheet.Core;

internal readonly record struct WorksheetAxisStyleOperation
{
    public WorksheetAxisStyleOperation(
        long sequence,
        CellStylePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sequence, 0L);
        if (patch.IsEmpty)
        {
            throw new ArgumentException(
                "An axis style operation must change at least one property.",
                nameof(patch));
        }

        Sequence = sequence;
        Patch = patch;
    }

    public long Sequence { get; }

    public CellStylePatch Patch { get; }
}

internal sealed class WorksheetAxisStyleSpan
{
    public WorksheetAxisStyleSpan(
        int startIndex,
        int endIndex,
        IEnumerable<WorksheetAxisStyleOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(endIndex, startIndex);

        StartIndex = startIndex;
        EndIndex = endIndex;
        Operations = operations.ToArray();
        if (Operations.Length == 0)
        {
            throw new ArgumentException(
                "A style span must contain at least one operation.",
                nameof(operations));
        }
        for (var index = 1; index < Operations.Length; index++)
        {
            if (Operations[index - 1].Sequence >= Operations[index].Sequence)
            {
                throw new ArgumentException(
                    "Style operations must be strictly ordered by sequence.",
                    nameof(operations));
            }
        }
    }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public WorksheetAxisStyleOperation[] Operations { get; }

    public WorksheetAxisStyleSpan Clone() =>
        new(StartIndex, EndIndex, Operations);

    public WorksheetAxisStyleSpan WithBounds(int startIndex, int endIndex) =>
        new(startIndex, endIndex, Operations);

    public WorksheetAxisStyleSpan Append(
        int startIndex,
        int endIndex,
        WorksheetAxisStyleOperation operation)
    {
        if (Operations[^1].Sequence >= operation.Sequence)
        {
            throw new ArgumentException(
                "A new operation must have a greater sequence.",
                nameof(operation));
        }

        var operations = new WorksheetAxisStyleOperation[
            Operations.Length + 1];
        Operations.CopyTo(operations, 0);
        operations[^1] = operation;
        return new WorksheetAxisStyleSpan(
            startIndex,
            endIndex,
            operations);
    }
}

internal sealed class WorksheetAxisStyleState
{
    public WorksheetAxisStyleState(
        IEnumerable<WorksheetAxisStyleSpan> rowSpans,
        IEnumerable<WorksheetAxisStyleSpan> columnSpans,
        long nextSequence)
    {
        ArgumentNullException.ThrowIfNull(rowSpans);
        ArgumentNullException.ThrowIfNull(columnSpans);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            nextSequence,
            0L);

        RowSpans = rowSpans.Select(static span => span.Clone()).ToArray();
        ColumnSpans = columnSpans.Select(static span => span.Clone()).ToArray();
        NextSequence = nextSequence;
    }

    public WorksheetAxisStyleSpan[] RowSpans { get; }

    public WorksheetAxisStyleSpan[] ColumnSpans { get; }

    public long NextSequence { get; }
}

internal sealed class WorksheetAxisStyleMap
{
    private readonly int _axisLength;
    private List<WorksheetAxisStyleSpan> _spans = [];

    public WorksheetAxisStyleMap(int axisLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(axisLength);
        _axisLength = axisLength;
    }

    public int SpanCount => _spans.Count;

    public WorksheetAxisStyleOperation[] GetOperations(int index)
    {
        ValidateIndex(index, nameof(index));
        var low = 0;
        var high = _spans.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var span = _spans[middle];
            if (index < span.StartIndex)
            {
                high = middle - 1;
            }
            else if (index > span.EndIndex)
            {
                low = middle + 1;
            }
            else
            {
                return span.Operations;
            }
        }
        return [];
    }

    public WorksheetAxisStyleSpan[] Capture() =>
        _spans.Select(static span => span.Clone()).ToArray();

    public void Restore(IEnumerable<WorksheetAxisStyleSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        _spans = Normalize(spans.Select(static span => span.Clone()));
        ValidateSpans(_spans);
    }

    public void Apply(
        int startIndex,
        int endIndex,
        WorksheetAxisStyleOperation operation)
    {
        ValidateRange(startIndex, endIndex);
        var next = new List<WorksheetAxisStyleSpan>(_spans.Count + 3);
        var cursor = startIndex;
        var targetCompleted = false;

        foreach (var span in _spans)
        {
            if (span.EndIndex < startIndex)
            {
                next.Add(span);
                continue;
            }
            if (span.StartIndex > endIndex)
            {
                if (!targetCompleted && cursor <= endIndex)
                {
                    next.Add(CreateOperationSpan(
                        cursor,
                        endIndex,
                        operation));
                    targetCompleted = true;
                }
                next.Add(span);
                continue;
            }

            if (span.StartIndex < startIndex)
            {
                next.Add(span.WithBounds(
                    span.StartIndex,
                    startIndex - 1));
            }

            if (cursor < span.StartIndex)
            {
                next.Add(CreateOperationSpan(
                    cursor,
                    Math.Min(endIndex, span.StartIndex - 1),
                    operation));
            }

            var overlapStart = Math.Max(startIndex, span.StartIndex);
            var overlapEnd = Math.Min(endIndex, span.EndIndex);
            next.Add(span.Append(
                overlapStart,
                overlapEnd,
                operation));
            cursor = overlapEnd + 1;

            if (span.EndIndex > endIndex)
            {
                next.Add(span.WithBounds(
                    endIndex + 1,
                    span.EndIndex));
                targetCompleted = true;
            }
        }

        if (!targetCompleted && cursor <= endIndex)
        {
            next.Add(CreateOperationSpan(cursor, endIndex, operation));
        }

        _spans = Normalize(next);
        ValidateSpans(_spans);
    }

    public void ApplyStructuralChange(WorksheetStructuralChange change)
    {
        if (change.AxisLength != _axisLength)
        {
            throw new ArgumentException(
                "The structural change axis length does not match the style map.",
                nameof(change));
        }

        var next = new List<WorksheetAxisStyleSpan>(_spans.Count + 2);
        if (change.Kind == WorksheetStructuralChangeKind.Insert)
        {
            foreach (var span in _spans)
            {
                if (span.EndIndex < change.Index)
                {
                    next.Add(span);
                    continue;
                }

                if (span.StartIndex < change.Index)
                {
                    next.Add(span.WithBounds(
                        span.StartIndex,
                        change.Index - 1));
                }

                var shiftedSourceStart = Math.Max(
                    span.StartIndex,
                    change.Index);
                var shiftedStart = checked(
                    shiftedSourceStart + change.Count);
                if (shiftedStart >= _axisLength)
                {
                    continue;
                }

                var shiftedEnd = Math.Min(
                    _axisLength - 1,
                    checked(span.EndIndex + change.Count));
                if (shiftedEnd >= shiftedStart)
                {
                    next.Add(span.WithBounds(shiftedStart, shiftedEnd));
                }
            }
        }
        else
        {
            var deleteEnd = checked(change.Index + change.Count - 1);
            foreach (var span in _spans)
            {
                if (span.EndIndex < change.Index)
                {
                    next.Add(span);
                }
                else if (span.StartIndex > deleteEnd)
                {
                    next.Add(span.WithBounds(
                        span.StartIndex - change.Count,
                        span.EndIndex - change.Count));
                }
                else
                {
                    if (span.StartIndex < change.Index)
                    {
                        next.Add(span.WithBounds(
                            span.StartIndex,
                            change.Index - 1));
                    }
                    if (span.EndIndex > deleteEnd)
                    {
                        next.Add(span.WithBounds(
                            change.Index,
                            span.EndIndex - change.Count));
                    }
                }
            }
        }

        _spans = Normalize(next);
        ValidateSpans(_spans);
    }

    public void ApplyAxisMove(WorksheetAxisMove move)
    {
        if (move.AxisLength != _axisLength)
        {
            throw new ArgumentException(
                "The axis move length does not match the style map.",
                nameof(move));
        }
        if (move.IsNoOp || _spans.Count == 0)
        {
            return;
        }

        var next = new List<WorksheetAxisStyleSpan>(_spans.Count + 2);
        foreach (var span in _spans)
        {
            foreach (var interval in move.MapInterval(
                span.StartIndex,
                span.EndIndex))
            {
                next.Add(new WorksheetAxisStyleSpan(
                    interval.Start,
                    interval.End,
                    span.Operations));
            }
        }

        _spans = Normalize(next);
        ValidateSpans(_spans);
    }

    private static WorksheetAxisStyleSpan CreateOperationSpan(
        int startIndex,
        int endIndex,
        WorksheetAxisStyleOperation operation) =>
        new(startIndex, endIndex, [operation]);

    private static List<WorksheetAxisStyleSpan> Normalize(
        IEnumerable<WorksheetAxisStyleSpan> source)
    {
        var ordered = source
            .OrderBy(static span => span.StartIndex)
            .ThenBy(static span => span.EndIndex)
            .ToArray();
        var normalized = new List<WorksheetAxisStyleSpan>(ordered.Length);
        foreach (var span in ordered)
        {
            if (normalized.Count > 0)
            {
                var previous = normalized[^1];
                if (previous.EndIndex + 1 == span.StartIndex &&
                    previous.Operations.SequenceEqual(span.Operations))
                {
                    normalized[^1] = previous.WithBounds(
                        previous.StartIndex,
                        span.EndIndex);
                    continue;
                }
            }
            normalized.Add(span);
        }
        return normalized;
    }

    private void ValidateRange(int startIndex, int endIndex)
    {
        ValidateIndex(startIndex, nameof(startIndex));
        ValidateIndex(endIndex, nameof(endIndex));
        ArgumentOutOfRangeException.ThrowIfLessThan(endIndex, startIndex);
    }

    private void ValidateIndex(int index, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            index,
            _axisLength,
            parameterName);
    }

    private void ValidateSpans(List<WorksheetAxisStyleSpan> spans)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            ValidateRange(span.StartIndex, span.EndIndex);
            if (index > 0 && spans[index - 1].EndIndex >= span.StartIndex)
            {
                throw new InvalidOperationException(
                    "Axis style spans must not overlap.");
            }
        }
    }
}
