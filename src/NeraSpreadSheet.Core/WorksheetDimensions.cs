using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum WorksheetAxis
{
    Row,
    Column,
}

public sealed class DimensionChangedEventArgs : EventArgs
{
    public DimensionChangedEventArgs(
        WorksheetAxis axis,
        int index,
        double oldSize,
        double newSize,
        int count = 1)
    {
        Axis = axis;
        Index = index;
        OldSize = oldSize;
        NewSize = newSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Count = count;
    }

    public WorksheetAxis Axis { get; }

    public int Index { get; }

    public double OldSize { get; }

    public double NewSize { get; }

    /// <summary>Gets the number of consecutive affected axis entries.</summary>
    public int Count { get; }
}

internal sealed record WorksheetAxisDimensionState(
    KeyValuePair<int, double>[] Overrides,
    WorksheetAxisInterval[] HiddenRanges);

public sealed class WorksheetDimensions
{
    private const double EqualityTolerance = 1e-9;
    private readonly Dictionary<int, double> _rowHeights = [];
    private readonly Dictionary<int, double> _columnWidths = [];
    private WorksheetAxisInterval[] _hiddenRows = [];
    private WorksheetAxisInterval[] _hiddenColumns = [];

    public WorksheetDimensions(
        double defaultRowHeight = 20d,
        double defaultColumnWidth = 80d)
    {
        DefaultRowHeight = Guard.PositiveFinite(
            defaultRowHeight,
            nameof(defaultRowHeight));
        DefaultColumnWidth = Guard.PositiveFinite(
            defaultColumnWidth,
            nameof(defaultColumnWidth));
    }

    public double DefaultRowHeight { get; }

    public double DefaultColumnWidth { get; }

    public long Version { get; private set; }

    public event EventHandler<DimensionChangedEventArgs>? Changed;

    /// <summary>Gets the effective row height, or zero when the row is hidden.</summary>
    public double GetRowHeight(int rowIndex)
    {
        ValidateRow(rowIndex);
        return IsHidden(_hiddenRows, rowIndex)
            ? 0d
            : GetRawSize(_rowHeights, rowIndex, DefaultRowHeight);
    }

    /// <summary>Gets the effective column width, or zero when the column is hidden.</summary>
    public double GetColumnWidth(int columnIndex)
    {
        ValidateColumn(columnIndex);
        return IsHidden(_hiddenColumns, columnIndex)
            ? 0d
            : GetRawSize(_columnWidths, columnIndex, DefaultColumnWidth);
    }

    /// <summary>Gets the row height retained for use after the row is unhidden.</summary>
    public double GetUnhiddenRowHeight(int rowIndex)
    {
        ValidateRow(rowIndex);
        return GetRawSize(_rowHeights, rowIndex, DefaultRowHeight);
    }

    /// <summary>Gets the column width retained for use after the column is unhidden.</summary>
    public double GetUnhiddenColumnWidth(int columnIndex)
    {
        ValidateColumn(columnIndex);
        return GetRawSize(_columnWidths, columnIndex, DefaultColumnWidth);
    }

    /// <summary>Returns whether the specified row is manually hidden.</summary>
    public bool IsRowHidden(int rowIndex)
    {
        ValidateRow(rowIndex);
        return IsHidden(_hiddenRows, rowIndex);
    }

    /// <summary>Returns whether the specified column is manually hidden.</summary>
    public bool IsColumnHidden(int columnIndex)
    {
        ValidateColumn(columnIndex);
        return IsHidden(_hiddenColumns, columnIndex);
    }

    /// <summary>Finds the normalized hidden interval containing a row.</summary>
    public bool TryGetHiddenRowRange(
        int rowIndex,
        out WorksheetAxisInterval range)
    {
        ValidateRow(rowIndex);
        return TryGetHiddenRange(_hiddenRows, rowIndex, out range);
    }

    /// <summary>Finds the normalized hidden interval containing a column.</summary>
    public bool TryGetHiddenColumnRange(
        int columnIndex,
        out WorksheetAxisInterval range)
    {
        ValidateColumn(columnIndex);
        return TryGetHiddenRange(_hiddenColumns, columnIndex, out range);
    }

    /// <summary>Returns whether a row range intersects any manually hidden row.</summary>
    public bool HasHiddenRows(int rowIndex, int count = 1)
    {
        ValidateRange(rowIndex, count, SpreadsheetLimits.MaxRows, nameof(rowIndex));
        return IntersectsHiddenRange(
            _hiddenRows,
            rowIndex,
            checked(rowIndex + count - 1));
    }

    /// <summary>Returns whether a column range intersects any manually hidden column.</summary>
    public bool HasHiddenColumns(int columnIndex, int count = 1)
    {
        ValidateRange(columnIndex, count, SpreadsheetLimits.MaxColumns, nameof(columnIndex));
        return IntersectsHiddenRange(
            _hiddenColumns,
            columnIndex,
            checked(columnIndex + count - 1));
    }

    public void SetRowHeight(int rowIndex, double height)
    {
        ValidateRow(rowIndex);
        SetSize(
            _rowHeights,
            ref _hiddenRows,
            WorksheetAxis.Row,
            rowIndex,
            height,
            DefaultRowHeight);
    }

    public void SetColumnWidth(int columnIndex, double width)
    {
        ValidateColumn(columnIndex);
        SetSize(
            _columnWidths,
            ref _hiddenColumns,
            WorksheetAxis.Column,
            columnIndex,
            width,
            DefaultColumnWidth);
    }

    /// <summary>Hides a sparse consecutive row range without materializing its entries.</summary>
    public void HideRows(int rowIndex, int count = 1)
    {
        ValidateRange(rowIndex, count, SpreadsheetLimits.MaxRows, nameof(rowIndex));
        SetRangeVisibility(
            ref _hiddenRows,
            WorksheetAxis.Row,
            rowIndex,
            count,
            hidden: true,
            DefaultRowHeight);
    }

    /// <summary>Unhides a consecutive row range and restores retained row heights.</summary>
    public void UnhideRows(int rowIndex, int count = 1)
    {
        ValidateRange(rowIndex, count, SpreadsheetLimits.MaxRows, nameof(rowIndex));
        SetRangeVisibility(
            ref _hiddenRows,
            WorksheetAxis.Row,
            rowIndex,
            count,
            hidden: false,
            DefaultRowHeight);
    }

    /// <summary>Hides a sparse consecutive column range without materializing its entries.</summary>
    public void HideColumns(int columnIndex, int count = 1)
    {
        ValidateRange(columnIndex, count, SpreadsheetLimits.MaxColumns, nameof(columnIndex));
        SetRangeVisibility(
            ref _hiddenColumns,
            WorksheetAxis.Column,
            columnIndex,
            count,
            hidden: true,
            DefaultColumnWidth);
    }

    /// <summary>Unhides a consecutive column range and restores retained column widths.</summary>
    public void UnhideColumns(int columnIndex, int count = 1)
    {
        ValidateRange(columnIndex, count, SpreadsheetLimits.MaxColumns, nameof(columnIndex));
        SetRangeVisibility(
            ref _hiddenColumns,
            WorksheetAxis.Column,
            columnIndex,
            count,
            hidden: false,
            DefaultColumnWidth);
    }

    public IReadOnlyDictionary<int, double> GetRowOverrides() => _rowHeights;

    public IReadOnlyDictionary<int, double> GetColumnOverrides() => _columnWidths;

    /// <summary>Gets the normalized sparse intervals of manually hidden rows.</summary>
    public IReadOnlyList<WorksheetAxisInterval> GetHiddenRowRanges() => _hiddenRows;

    /// <summary>Gets the normalized sparse intervals of manually hidden columns.</summary>
    public IReadOnlyList<WorksheetAxisInterval> GetHiddenColumnRanges() => _hiddenColumns;

    internal WorksheetAxisDimensionState CreateStructuralState(
        WorksheetStructuralChange change)
    {
        var sourceOverrides = change.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        var sourceHidden = change.Axis == WorksheetAxis.Row
            ? _hiddenRows
            : _hiddenColumns;
        var transformedOverrides = new List<KeyValuePair<int, double>>(sourceOverrides.Count);
        foreach (var (index, size) in sourceOverrides)
        {
            if (!change.TryMapIndex(index, out var mappedIndex))
            {
                if (change.Kind == WorksheetStructuralChangeKind.Insert)
                {
                    throw new InvalidOperationException(
                        "Cannot insert because a dimension override would move outside the worksheet bounds.");
                }
                continue;
            }
            transformedOverrides.Add(new KeyValuePair<int, double>(mappedIndex, size));
        }

        var transformedHidden = new List<WorksheetAxisInterval>(sourceHidden.Length);
        foreach (var range in sourceHidden)
        {
            if (change.TryMapInterval(range.Start, range.End, out var start, out var end))
            {
                transformedHidden.Add(new WorksheetAxisInterval(start, end));
            }
            else if (change.Kind == WorksheetStructuralChangeKind.Insert)
            {
                throw new InvalidOperationException(
                    "Cannot insert because a hidden dimension range would move outside the worksheet bounds.");
            }
        }
        return new WorksheetAxisDimensionState(
            [.. transformedOverrides],
            NormalizeHiddenRanges(transformedHidden));
    }

    internal WorksheetAxisDimensionState CreateAxisMoveState(
        WorksheetAxisMove move)
    {
        var sourceOverrides = move.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        var sourceHidden = move.Axis == WorksheetAxis.Row
            ? _hiddenRows
            : _hiddenColumns;
        var transformedOverrides = sourceOverrides
            .Select(pair => new KeyValuePair<int, double>(
                move.MapIndex(pair.Key),
                pair.Value))
            .OrderBy(static pair => pair.Key)
            .ToArray();
        var transformedHidden = sourceHidden
            .SelectMany(range => move.MapInterval(range.Start, range.End))
            .Select(static range => new WorksheetAxisInterval(range.Start, range.End));
        return new WorksheetAxisDimensionState(
            transformedOverrides,
            NormalizeHiddenRanges(transformedHidden));
    }

    internal void ReplaceStructuralState(
        WorksheetStructuralChange change,
        WorksheetAxisDimensionState transformed)
    {
        ArgumentNullException.ThrowIfNull(transformed);
        ReplaceAxisState(change.Axis, transformed);
        PublishAxisChange(change.Axis, change.Index, change.Count);
    }

    internal void ReplaceAxisMoveState(
        WorksheetAxisMove move,
        WorksheetAxisDimensionState transformed)
    {
        ArgumentNullException.ThrowIfNull(transformed);
        ReplaceAxisState(move.Axis, transformed);
        PublishAxisChange(
            move.Axis,
            move.AffectedStartIndex,
            checked(move.AffectedEndIndex - move.AffectedStartIndex + 1));
    }

    internal void RestoreState(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths,
        IReadOnlyList<WorksheetAxisInterval> hiddenRows,
        IReadOnlyList<WorksheetAxisInterval> hiddenColumns,
        WorksheetStructuralChange signalChange)
    {
        RestoreState(rowHeights, columnWidths, hiddenRows, hiddenColumns);
        PublishAxisChange(signalChange.Axis, signalChange.Index, signalChange.Count);
    }

    internal void RestoreState(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths,
        IReadOnlyList<WorksheetAxisInterval> hiddenRows,
        IReadOnlyList<WorksheetAxisInterval> hiddenColumns,
        WorksheetAxisMove signalMove)
    {
        RestoreState(rowHeights, columnWidths, hiddenRows, hiddenColumns);
        PublishAxisChange(
            signalMove.Axis,
            signalMove.AffectedStartIndex,
            checked(signalMove.AffectedEndIndex - signalMove.AffectedStartIndex + 1));
    }

    internal void RestoreHiddenRanges(
        WorksheetAxis axis,
        IReadOnlyList<WorksheetAxisInterval> ranges,
        int signalIndex,
        int signalCount)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var normalized = NormalizeHiddenRanges(ranges);
        if (axis == WorksheetAxis.Row)
        {
            _hiddenRows = normalized;
        }
        else
        {
            _hiddenColumns = normalized;
        }
        PublishAxisChange(axis, signalIndex, signalCount);
    }

    private static double GetRawSize(
        IReadOnlyDictionary<int, double> values,
        int index,
        double defaultSize) =>
        values.GetValueOrDefault(index, defaultSize);

    private void SetSize(
        Dictionary<int, double> values,
        ref WorksheetAxisInterval[] hiddenRanges,
        WorksheetAxis axis,
        int index,
        double newSize,
        double defaultSize)
    {
        Guard.NonNegativeFinite(newSize, nameof(newSize));
        var wasHidden = IsHidden(hiddenRanges, index);
        var oldSize = wasHidden
            ? 0d
            : GetRawSize(values, index, defaultSize);
        if (newSize <= EqualityTolerance)
        {
            if (wasHidden)
            {
                return;
            }
            hiddenRanges = AddHiddenRange(hiddenRanges, index, index);
            PublishSizeChange(axis, index, oldSize, 0d);
            return;
        }

        if (Math.Abs(newSize - defaultSize) <= EqualityTolerance)
        {
            values.Remove(index);
        }
        else
        {
            values[index] = newSize;
        }
        if (wasHidden)
        {
            hiddenRanges = RemoveHiddenRange(hiddenRanges, index, index);
        }
        if (!wasHidden && Math.Abs(oldSize - newSize) <= EqualityTolerance)
        {
            return;
        }
        PublishSizeChange(axis, index, oldSize, newSize);
    }

    private void SetRangeVisibility(
        ref WorksheetAxisInterval[] ranges,
        WorksheetAxis axis,
        int startIndex,
        int count,
        bool hidden,
        double defaultSize)
    {
        var endIndex = checked(startIndex + count - 1);
        var next = hidden
            ? AddHiddenRange(ranges, startIndex, endIndex)
            : RemoveHiddenRange(ranges, startIndex, endIndex);
        if (ranges.SequenceEqual(next))
        {
            return;
        }
        ranges = next;
        Version++;
        Changed?.Invoke(
            this,
            new DimensionChangedEventArgs(
                axis,
                startIndex,
                hidden ? defaultSize : 0d,
                hidden ? 0d : defaultSize,
                count));
    }

    private void ReplaceAxisState(
        WorksheetAxis axis,
        WorksheetAxisDimensionState state)
    {
        var target = axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        ReplaceOverrides(target, state.Overrides);
        if (axis == WorksheetAxis.Row)
        {
            _hiddenRows = [.. state.HiddenRanges];
        }
        else
        {
            _hiddenColumns = [.. state.HiddenRanges];
        }
    }

    private void RestoreState(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths,
        IReadOnlyList<WorksheetAxisInterval> hiddenRows,
        IReadOnlyList<WorksheetAxisInterval> hiddenColumns)
    {
        ReplaceOverrides(_rowHeights, rowHeights);
        ReplaceOverrides(_columnWidths, columnWidths);
        _hiddenRows = NormalizeHiddenRanges(hiddenRows);
        _hiddenColumns = NormalizeHiddenRanges(hiddenColumns);
    }

    private static void ReplaceOverrides(
        Dictionary<int, double> target,
        IReadOnlyList<KeyValuePair<int, double>> transformed)
    {
        target.Clear();
        foreach (var (index, size) in transformed)
        {
            target.Add(index, size);
        }
    }

    private void PublishAxisChange(WorksheetAxis axis, int index, int count)
    {
        Version++;
        var defaultSize = axis == WorksheetAxis.Row
            ? DefaultRowHeight
            : DefaultColumnWidth;
        Changed?.Invoke(
            this,
            new DimensionChangedEventArgs(
                axis,
                index,
                defaultSize,
                defaultSize,
                count));
    }

    private void PublishSizeChange(
        WorksheetAxis axis,
        int index,
        double oldSize,
        double newSize)
    {
        Version++;
        Changed?.Invoke(
            this,
            new DimensionChangedEventArgs(
                axis,
                index,
                oldSize,
                newSize));
    }

    private static WorksheetAxisInterval[] AddHiddenRange(
        IEnumerable<WorksheetAxisInterval> ranges,
        int start,
        int end) =>
        NormalizeHiddenRanges(ranges.Append(new WorksheetAxisInterval(start, end)));

    private static WorksheetAxisInterval[] RemoveHiddenRange(
        IEnumerable<WorksheetAxisInterval> ranges,
        int start,
        int end)
    {
        var result = new List<WorksheetAxisInterval>();
        foreach (var range in ranges)
        {
            if (range.End < start || range.Start > end)
            {
                result.Add(range);
                continue;
            }
            if (range.Start < start)
            {
                result.Add(new WorksheetAxisInterval(range.Start, start - 1));
            }
            if (range.End > end)
            {
                result.Add(new WorksheetAxisInterval(end + 1, range.End));
            }
        }
        return [.. result];
    }

    private static WorksheetAxisInterval[] NormalizeHiddenRanges(
        IEnumerable<WorksheetAxisInterval> ranges)
    {
        var ordered = ranges
            .OrderBy(static range => range.Start)
            .ThenBy(static range => range.End)
            .ToArray();
        if (ordered.Length <= 1)
        {
            return ordered;
        }
        var result = new List<WorksheetAxisInterval>(ordered.Length);
        var current = ordered[0];
        for (var index = 1; index < ordered.Length; index++)
        {
            var next = ordered[index];
            if (next.Start <= current.End + 1)
            {
                current = new WorksheetAxisInterval(
                    current.Start,
                    Math.Max(current.End, next.End));
            }
            else
            {
                result.Add(current);
                current = next;
            }
        }
        result.Add(current);
        return [.. result];
    }

    private static bool IsHidden(
        IReadOnlyList<WorksheetAxisInterval> ranges,
        int index) =>
        TryGetHiddenRange(ranges, index, out _);

    private static bool TryGetHiddenRange(
        IReadOnlyList<WorksheetAxisInterval> ranges,
        int index,
        out WorksheetAxisInterval hiddenRange)
    {
        var low = 0;
        var high = ranges.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];
            if (index < range.Start)
            {
                high = middle - 1;
            }
            else if (index > range.End)
            {
                low = middle + 1;
            }
            else
            {
                hiddenRange = range;
                return true;
            }
        }
        hiddenRange = default;
        return false;
    }

    private static bool IntersectsHiddenRange(
        IReadOnlyList<WorksheetAxisInterval> ranges,
        int start,
        int end)
    {
        foreach (var range in ranges)
        {
            if (range.Start > end)
            {
                return false;
            }
            if (range.End >= start)
            {
                return true;
            }
        }
        return false;
    }

    private static void ValidateRange(
        int startIndex,
        int count,
        int axisLength,
        string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            startIndex,
            axisLength,
            parameterName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            count,
            axisLength - startIndex);
    }

    private static void ValidateRow(int rowIndex) =>
        Guard.InRange(
            rowIndex,
            0,
            SpreadsheetLimits.MaxRows - 1,
            nameof(rowIndex));

    private static void ValidateColumn(int columnIndex) =>
        Guard.InRange(
            columnIndex,
            0,
            SpreadsheetLimits.MaxColumns - 1,
            nameof(columnIndex));
}
