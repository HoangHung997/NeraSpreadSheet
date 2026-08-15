using System.Collections.ObjectModel;

namespace NeraSpreadSheet.Core;

public sealed class WorksheetSnapshot
{
    private readonly IReadOnlyDictionary<CellAddress, CellData> _cells;
    private readonly CellRange[] _mergedCells;

    private WorksheetSnapshot(
        string name,
        long version,
        IReadOnlyDictionary<CellAddress, CellData> cells,
        double defaultRowHeight,
        double defaultColumnWidth,
        IReadOnlyDictionary<int, double> rowHeights,
        IReadOnlyDictionary<int, double> columnWidths,
        CellRange[] mergedCells)
    {
        Name = name;
        Version = version;
        _cells = cells;
        DefaultRowHeight = defaultRowHeight;
        DefaultColumnWidth = defaultColumnWidth;
        RowHeights = rowHeights;
        ColumnWidths = columnWidths;
        _mergedCells = mergedCells;
    }

    public string Name { get; }
    public long Version { get; }
    public int UsedCellCount => _cells.Count;
    public double DefaultRowHeight { get; }
    public double DefaultColumnWidth { get; }
    public IReadOnlyDictionary<int, double> RowHeights { get; }
    public IReadOnlyDictionary<int, double> ColumnWidths { get; }
    public IReadOnlyList<CellRange> MergedCells => _mergedCells;

    public CellData GetCell(CellAddress address) => _cells.GetValueOrDefault(address, CellData.Empty);

    public IEnumerable<KeyValuePair<CellAddress, CellData>> EnumerateUsedCells() => _cells;

    public bool TryGetMergedRange(CellAddress address, out CellRange range)
    {
        foreach (var candidate in _mergedCells)
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

    public static WorksheetSnapshot Capture(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var cells = new ReadOnlyDictionary<CellAddress, CellData>(
            worksheet.EnumerateUsedCells().ToDictionary(pair => pair.Key, pair => pair.Value));
        var rows = new ReadOnlyDictionary<int, double>(
            worksheet.Dimensions.GetRowOverrides().ToDictionary(pair => pair.Key, pair => pair.Value));
        var columns = new ReadOnlyDictionary<int, double>(
            worksheet.Dimensions.GetColumnOverrides().ToDictionary(pair => pair.Key, pair => pair.Value));

        return new WorksheetSnapshot(
            worksheet.Name,
            worksheet.Version,
            cells,
            worksheet.Dimensions.DefaultRowHeight,
            worksheet.Dimensions.DefaultColumnWidth,
            rows,
            columns,
            [.. worksheet.MergedCells.Ranges]);
    }
}
