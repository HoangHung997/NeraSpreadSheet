using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout;

public readonly record struct AxisIndexRange
{
    public AxisIndexRange(int startIndex, int endIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(endIndex, startIndex);
        StartIndex = startIndex;
        EndIndex = endIndex;
    }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public int Count => checked(EndIndex - StartIndex + 1);
}

public sealed class SparseAxisMetricIndex
{
    private const double EqualityTolerance = 1e-9;
    private readonly Dictionary<int, double> _overrides = [];
    private readonly Dictionary<int, double> _fenwickDeltas = [];
    private AxisIndexRange[] _hiddenRanges = [];
    private HiddenRangeMetric[] _hiddenMetrics = [];

    public SparseAxisMetricIndex(int count, double defaultSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        Count = count;
        DefaultSize = Guard.PositiveFinite(defaultSize, nameof(defaultSize));
    }

    public int Count { get; }

    public double DefaultSize { get; }

    public int OverrideCount => _overrides.Count;

    public int HiddenRangeCount => _hiddenRanges.Length;

    public long Version { get; private set; }

    public double TotalExtent => Math.Max(0d, GetOffset(Count));

    public double GetSize(int index)
    {
        ValidateIndex(index);
        return TryGetHiddenRange(index, out _)
            ? 0d
            : GetRawSize(index);
    }

    public void SetSize(int index, double size)
    {
        ValidateIndex(index);
        Guard.NonNegativeFinite(size, nameof(size));

        var previous = GetRawSize(index);
        if (Math.Abs(previous - size) <= EqualityTolerance)
        {
            return;
        }

        if (Math.Abs(size - DefaultSize) <= EqualityTolerance)
        {
            _overrides.Remove(index);
        }
        else
        {
            _overrides[index] = size;
        }

        AddFenwickDelta(index, size - previous);
        RebuildHiddenMetrics();
        Version++;
    }

    public void SetHiddenRanges(IEnumerable<AxisIndexRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var normalized = NormalizeHiddenRanges(ranges);
        if (_hiddenRanges.SequenceEqual(normalized))
        {
            return;
        }

        _hiddenRanges = normalized;
        RebuildHiddenMetrics();
        Version++;
    }

    public void ClearHiddenRanges()
    {
        if (_hiddenRanges.Length == 0)
        {
            return;
        }

        _hiddenRanges = [];
        _hiddenMetrics = [];
        Version++;
    }

    public bool IsHidden(int index)
    {
        ValidateIndex(index);
        return TryGetHiddenRange(index, out _);
    }

    public double GetOffset(int exclusiveIndex)
    {
        if (exclusiveIndex < 0 || exclusiveIndex > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveIndex));
        }

        return GetRawOffset(exclusiveIndex) -
               GetHiddenExtentBefore(exclusiveIndex);
    }

    public int FindIndexAtOffset(double offset)
    {
        Guard.NonNegativeFinite(offset, nameof(offset));
        var totalExtent = TotalExtent;

        if (totalExtent <= 0d || offset >= totalExtent)
        {
            return Count - 1;
        }

        var low = 0;
        var high = Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var start = GetOffset(middle);
            var end = GetOffset(middle + 1);

            if (offset < start)
            {
                high = middle - 1;
            }
            else if (offset >= end)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return Math.Clamp(low, 0, Count - 1);
    }

    public IReadOnlyList<AxisSlot> GetSlots(
        double viewportOffset,
        double viewportExtent,
        double overscan = 0d)
    {
        Guard.NonNegativeFinite(viewportOffset, nameof(viewportOffset));
        Guard.NonNegativeFinite(viewportExtent, nameof(viewportExtent));
        Guard.NonNegativeFinite(overscan, nameof(overscan));

        if (viewportExtent <= 0d)
        {
            return Array.Empty<AxisSlot>();
        }

        var startOffset = Math.Max(0d, viewportOffset - overscan);
        var endOffset = Math.Min(
            TotalExtent,
            viewportOffset + viewportExtent + overscan);

        if (endOffset <= startOffset)
        {
            return Array.Empty<AxisSlot>();
        }

        var startIndex = FindIndexAtOffset(startOffset);
        var slots = new List<AxisSlot>();

        for (var index = startIndex; index < Count; index++)
        {
            if (TryGetHiddenRange(index, out var hiddenRange))
            {
                index = hiddenRange.EndIndex;
                continue;
            }

            var absoluteStart = GetOffset(index);
            if (absoluteStart >= endOffset)
            {
                break;
            }

            var size = GetRawSize(index);
            if (size > 0d)
            {
                slots.Add(new AxisSlot(
                    index,
                    absoluteStart - viewportOffset,
                    size));
            }
        }

        return slots;
    }

    private AxisIndexRange[] NormalizeHiddenRanges(
        IEnumerable<AxisIndexRange> ranges)
    {
        var ordered = ranges
            .OrderBy(static range => range.StartIndex)
            .ThenBy(static range => range.EndIndex)
            .ToArray();
        foreach (var range in ordered)
        {
            if (range.EndIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ranges),
                    $"Hidden ranges must end before axis index {Count}.");
            }
        }

        if (ordered.Length == 0)
        {
            return [];
        }

        var normalized = new List<AxisIndexRange>(ordered.Length)
        {
            ordered[0],
        };
        for (var index = 1; index < ordered.Length; index++)
        {
            var current = ordered[index];
            var previous = normalized[^1];
            if (current.StartIndex <= previous.EndIndex + 1)
            {
                normalized[^1] = new AxisIndexRange(
                    previous.StartIndex,
                    Math.Max(previous.EndIndex, current.EndIndex));
            }
            else
            {
                normalized.Add(current);
            }
        }

        return normalized.ToArray();
    }

    private double GetRawSize(int index) =>
        _overrides.GetValueOrDefault(index, DefaultSize);

    private double GetRawOffset(int exclusiveIndex) =>
        (DefaultSize * exclusiveIndex) +
        GetPrefixDelta(exclusiveIndex);

    private double GetHiddenExtentBefore(int exclusiveIndex)
    {
        if (exclusiveIndex <= 0 || _hiddenMetrics.Length == 0)
        {
            return 0d;
        }

        var low = 0;
        var high = _hiddenMetrics.Length - 1;
        var match = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_hiddenMetrics[middle].StartIndex < exclusiveIndex)
            {
                match = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (match < 0)
        {
            return 0d;
        }

        var metric = _hiddenMetrics[match];
        var clippedEnd = Math.Min(
            exclusiveIndex,
            metric.EndExclusiveIndex);
        return metric.PrefixHiddenExtent +
               (GetRawOffset(clippedEnd) -
                GetRawOffset(metric.StartIndex));
    }

    private bool TryGetHiddenRange(
        int index,
        out AxisIndexRange range)
    {
        var low = 0;
        var high = _hiddenRanges.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = _hiddenRanges[middle];
            if (index < candidate.StartIndex)
            {
                high = middle - 1;
            }
            else if (index > candidate.EndIndex)
            {
                low = middle + 1;
            }
            else
            {
                range = candidate;
                return true;
            }
        }

        range = default;
        return false;
    }

    private void RebuildHiddenMetrics()
    {
        if (_hiddenRanges.Length == 0)
        {
            _hiddenMetrics = [];
            return;
        }

        var metrics = new HiddenRangeMetric[_hiddenRanges.Length];
        var prefix = 0d;
        for (var index = 0; index < _hiddenRanges.Length; index++)
        {
            var range = _hiddenRanges[index];
            var endExclusive = checked(range.EndIndex + 1);
            metrics[index] = new HiddenRangeMetric(
                range.StartIndex,
                endExclusive,
                prefix);
            prefix += GetRawOffset(endExclusive) -
                      GetRawOffset(range.StartIndex);
        }

        _hiddenMetrics = metrics;
    }

    private void AddFenwickDelta(int zeroBasedIndex, double delta)
    {
        for (var node = zeroBasedIndex + 1;
             node <= Count;
             node += node & -node)
        {
            var updated =
                _fenwickDeltas.GetValueOrDefault(node) + delta;
            if (Math.Abs(updated) <= EqualityTolerance)
            {
                _fenwickDeltas.Remove(node);
            }
            else
            {
                _fenwickDeltas[node] = updated;
            }
        }
    }

    private double GetPrefixDelta(int exclusiveIndex)
    {
        var result = 0d;
        for (var node = exclusiveIndex;
             node > 0;
             node -= node & -node)
        {
            result += _fenwickDeltas.GetValueOrDefault(node);
        }

        return result;
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private readonly record struct HiddenRangeMetric(
        int StartIndex,
        int EndExclusiveIndex,
        double PrefixHiddenExtent);
}
