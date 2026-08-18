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
        double newSize)
    {
        Axis = axis;
        Index = index;
        OldSize = oldSize;
        NewSize = newSize;
    }

    public WorksheetAxis Axis { get; }

    public int Index { get; }

    public double OldSize { get; }

    public double NewSize { get; }
}

public sealed class WorksheetDimensions
{
    private readonly Dictionary<int, double> _rowHeights = [];
    private readonly Dictionary<int, double> _columnWidths = [];

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

    public double GetRowHeight(int rowIndex)
    {
        ValidateRow(rowIndex);
        return _rowHeights.GetValueOrDefault(rowIndex, DefaultRowHeight);
    }

    public double GetColumnWidth(int columnIndex)
    {
        ValidateColumn(columnIndex);
        return _columnWidths.GetValueOrDefault(columnIndex, DefaultColumnWidth);
    }

    public void SetRowHeight(int rowIndex, double height)
    {
        ValidateRow(rowIndex);
        SetSize(
            _rowHeights,
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
            WorksheetAxis.Column,
            columnIndex,
            width,
            DefaultColumnWidth);
    }

    public IReadOnlyDictionary<int, double> GetRowOverrides() => _rowHeights;

    public IReadOnlyDictionary<int, double> GetColumnOverrides() => _columnWidths;

    internal KeyValuePair<int, double>[] CreateStructuralOverrides(
        WorksheetStructuralChange change)
    {
        var source = change.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        var transformed = new List<KeyValuePair<int, double>>(source.Count);
        foreach (var (index, size) in source)
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
            transformed.Add(new KeyValuePair<int, double>(mappedIndex, size));
        }
        return [.. transformed];
    }

    internal KeyValuePair<int, double>[] CreateAxisMoveOverrides(
        WorksheetAxisMove move)
    {
        var source = move.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        var transformed = new KeyValuePair<int, double>[source.Count];
        var position = 0;
        foreach (var (index, size) in source)
        {
            transformed[position++] = new KeyValuePair<int, double>(
                move.MapIndex(index),
                size);
        }
        Array.Sort(
            transformed,
            static (left, right) => left.Key.CompareTo(right.Key));
        return transformed;
    }

    internal void ReplaceStructuralOverrides(
        WorksheetStructuralChange change,
        IReadOnlyList<KeyValuePair<int, double>> transformed)
    {
        var target = change.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        ReplaceOverrides(target, transformed);
        PublishAxisChange(change.Axis, change.Index);
    }

    internal void ReplaceAxisMoveOverrides(
        WorksheetAxisMove move,
        IReadOnlyList<KeyValuePair<int, double>> transformed)
    {
        var target = move.Axis == WorksheetAxis.Row
            ? _rowHeights
            : _columnWidths;
        ReplaceOverrides(target, transformed);
        PublishAxisChange(move.Axis, move.AffectedStartIndex);
    }

    internal void RestoreOverrides(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths,
        WorksheetStructuralChange signalChange)
    {
        RestoreOverrideDictionaries(rowHeights, columnWidths);
        PublishAxisChange(signalChange.Axis, signalChange.Index);
    }

    internal void RestoreOverrides(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths,
        WorksheetAxisMove signalMove)
    {
        RestoreOverrideDictionaries(rowHeights, columnWidths);
        PublishAxisChange(signalMove.Axis, signalMove.AffectedStartIndex);
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

    private void RestoreOverrideDictionaries(
        IReadOnlyList<KeyValuePair<int, double>> rowHeights,
        IReadOnlyList<KeyValuePair<int, double>> columnWidths)
    {
        ReplaceOverrides(_rowHeights, rowHeights);
        ReplaceOverrides(_columnWidths, columnWidths);
    }

    private void PublishAxisChange(WorksheetAxis axis, int index)
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
                defaultSize));
    }

    private void SetSize(
        Dictionary<int, double> values,
        WorksheetAxis axis,
        int index,
        double newSize,
        double defaultSize)
    {
        Guard.NonNegativeFinite(newSize, nameof(newSize));
        var oldSize = values.GetValueOrDefault(index, defaultSize);

        if (Math.Abs(oldSize - newSize) <= 1e-9)
        {
            return;
        }

        if (Math.Abs(newSize - defaultSize) <= 1e-9)
        {
            values.Remove(index);
        }
        else
        {
            values[index] = newSize;
        }

        Version++;
        Changed?.Invoke(
            this,
            new DimensionChangedEventArgs(
                axis,
                index,
                oldSize,
                newSize));
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
