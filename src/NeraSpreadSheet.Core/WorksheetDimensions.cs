using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum WorksheetAxis
{
    Row,
    Column,
}

public sealed class DimensionChangedEventArgs : EventArgs
{
    public DimensionChangedEventArgs(WorksheetAxis axis, int index, double oldSize, double newSize)
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

    public WorksheetDimensions(double defaultRowHeight = 20d, double defaultColumnWidth = 80d)
    {
        DefaultRowHeight = Guard.PositiveFinite(defaultRowHeight, nameof(defaultRowHeight));
        DefaultColumnWidth = Guard.PositiveFinite(defaultColumnWidth, nameof(defaultColumnWidth));
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
        SetSize(_rowHeights, WorksheetAxis.Row, rowIndex, height, DefaultRowHeight);
    }

    public void SetColumnWidth(int columnIndex, double width)
    {
        ValidateColumn(columnIndex);
        SetSize(_columnWidths, WorksheetAxis.Column, columnIndex, width, DefaultColumnWidth);
    }

    public IReadOnlyDictionary<int, double> GetRowOverrides() => _rowHeights;

    public IReadOnlyDictionary<int, double> GetColumnOverrides() => _columnWidths;

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
        Changed?.Invoke(this, new DimensionChangedEventArgs(axis, index, oldSize, newSize));
    }

    private static void ValidateRow(int rowIndex) =>
        Guard.InRange(rowIndex, 0, SpreadsheetLimits.MaxRows - 1, nameof(rowIndex));

    private static void ValidateColumn(int columnIndex) =>
        Guard.InRange(columnIndex, 0, SpreadsheetLimits.MaxColumns - 1, nameof(columnIndex));
}
