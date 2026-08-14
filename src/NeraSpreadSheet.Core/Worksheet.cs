namespace NeraSpreadSheet.Core;

public sealed class CellsChangedEventArgs : EventArgs
{
    public CellsChangedEventArgs(CellRange range, long worksheetVersion)
    {
        Range = range;
        WorksheetVersion = worksheetVersion;
    }

    public CellRange Range { get; }

    public long WorksheetVersion { get; }
}

public sealed class Worksheet
{
    private readonly Dictionary<CellAddress, CellData> _cells = [];

    internal Worksheet(string name)
    {
        Name = name;
        Dimensions = new WorksheetDimensions();
    }

    public string Name { get; internal set; }

    public WorksheetDimensions Dimensions { get; }

    public long Version { get; private set; }

    public int UsedCellCount => _cells.Count;

    public event EventHandler<CellsChangedEventArgs>? CellsChanged;

    public CellData GetCell(CellAddress address) => _cells.GetValueOrDefault(address, CellData.Empty);

    public bool TryGetCell(CellAddress address, out CellData cellData)
    {
        if (_cells.TryGetValue(address, out var stored))
        {
            cellData = stored;
            return true;
        }

        cellData = CellData.Empty;
        return false;
    }

    public IEnumerable<KeyValuePair<CellAddress, CellData>> EnumerateUsedCells() => _cells;

    public void SetValue(CellAddress address, object? value)
    {
        var current = GetCell(address);
        SetCell(address, new CellData(CellValue.FromObject(value), styleId: current.StyleId));
    }

    public void SetFormula(CellAddress address, string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        var normalized = formula.StartsWith("=", StringComparison.Ordinal) ? formula : $"={formula}";
        var current = GetCell(address);
        SetCell(address, new CellData(current.Value, normalized, current.StyleId));
    }

    public void SetStyle(CellAddress address, int styleId)
    {
        var current = GetCell(address);
        SetCell(address, new CellData(current.Value, current.Formula, styleId));
    }

    public void Clear(CellAddress address) => SetCell(address, CellData.Empty);

    public void SetCell(CellAddress address, CellData cellData)
    {
        ArgumentNullException.ThrowIfNull(cellData);
        var current = GetCell(address);

        if (current == cellData)
        {
            return;
        }

        if (cellData.IsEmpty)
        {
            _cells.Remove(address);
        }
        else
        {
            _cells[address] = cellData;
        }

        Version++;
        CellsChanged?.Invoke(this, new CellsChangedEventArgs(new CellRange(address, address), Version));
    }
}
