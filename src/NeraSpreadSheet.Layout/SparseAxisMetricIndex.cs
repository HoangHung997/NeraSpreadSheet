using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout;

public sealed class SparseAxisMetricIndex
{
    private const double EqualityTolerance = 1e-9;
    private readonly Dictionary<int, double> _overrides = [];
    private readonly Dictionary<int, double> _fenwickDeltas = [];

    public SparseAxisMetricIndex(int count, double defaultSize)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Count = count;
        DefaultSize = Guard.PositiveFinite(defaultSize, nameof(defaultSize));
    }

    public int Count { get; }

    public double DefaultSize { get; }

    public int OverrideCount => _overrides.Count;

    public long Version { get; private set; }

    public double TotalExtent => GetOffset(Count);

    public double GetSize(int index)
    {
        ValidateIndex(index);
        return _overrides.GetValueOrDefault(index, DefaultSize);
    }

    public void SetSize(int index, double size)
    {
        ValidateIndex(index);
        Guard.NonNegativeFinite(size, nameof(size));

        var previous = GetSize(index);
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
        Version++;
    }

    public double GetOffset(int exclusiveIndex)
    {
        if (exclusiveIndex < 0 || exclusiveIndex > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveIndex));
        }

        return (DefaultSize * exclusiveIndex) + GetPrefixDelta(exclusiveIndex);
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
        var endOffset = Math.Min(TotalExtent, viewportOffset + viewportExtent + overscan);

        if (endOffset <= startOffset)
        {
            return Array.Empty<AxisSlot>();
        }

        var startIndex = FindIndexAtOffset(startOffset);
        var slots = new List<AxisSlot>();

        for (var index = startIndex; index < Count; index++)
        {
            var absoluteStart = GetOffset(index);
            if (absoluteStart >= endOffset)
            {
                break;
            }

            var size = GetSize(index);
            if (size > 0d)
            {
                slots.Add(new AxisSlot(index, absoluteStart - viewportOffset, size));
            }
        }

        return slots;
    }

    private void AddFenwickDelta(int zeroBasedIndex, double delta)
    {
        for (var node = zeroBasedIndex + 1; node <= Count; node += node & -node)
        {
            var updated = _fenwickDeltas.GetValueOrDefault(node) + delta;
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
        for (var node = exclusiveIndex; node > 0; node -= node & -node)
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
}
