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
        MergedCells = new MergedCellCollection();
    }

    public string Name { get; internal set; }
    public WorksheetDimensions Dimensions { get; }
    public MergedCellCollection MergedCells { get; }
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

    public CellAddress ResolveMergedAnchor(CellAddress address) =>
        MergedCells.TryGetContaining(address, out var range) ? range.TopLeft : address;

    public void SetValue(CellAddress address, object? value)
    {
        address = ResolveMergedAnchor(address);
        var current = GetCell(address);
        SetCell(address, new CellData(CellValue.FromObject(value), styleId: current.StyleId));
    }

    public void SetFormula(CellAddress address, string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        address = ResolveMergedAnchor(address);
        var normalized = formula.StartsWith('=') ? formula : $"={formula}";
        var current = GetCell(address);
        SetCell(address, new CellData(current.Value, normalized, current.StyleId));
    }

    public void SetStyle(CellAddress address, int styleId)
    {
        address = ResolveMergedAnchor(address);
        var current = GetCell(address);
        SetCell(address, new CellData(current.Value, current.Formula, styleId));
    }

    public void Clear(CellAddress address) => SetCell(ResolveMergedAnchor(address), CellData.Empty);

    public void MergeCells(CellRange range, bool clearNonTopLeftCells = true)
    {
        MergedCells.Add(range);

        if (clearNonTopLeftCells)
        {
            var addressesToRemove = _cells.Keys
                .Where(address => address != range.TopLeft && range.Contains(address))
                .ToArray();
            foreach (var address in addressesToRemove)
            {
                _cells.Remove(address);
            }
        }

        Version++;
        CellsChanged?.Invoke(this, new CellsChangedEventArgs(range, Version));
    }

    public bool UnmergeCells(CellRange range)
    {
        if (!MergedCells.Remove(range))
        {
            return false;
        }

        Version++;
        CellsChanged?.Invoke(this, new CellsChangedEventArgs(range, Version));
        return true;
    }

    public bool UnmergeCell(CellAddress address)
    {
        if (!MergedCells.TryGetContaining(address, out var range))
        {
            return false;
        }

        return UnmergeCells(range);
    }

    public void SetCell(CellAddress address, CellData cellData)
    {
        ArgumentNullException.ThrowIfNull(cellData);
        address = ResolveMergedAnchor(address);
        SetCells([new KeyValuePair<CellAddress, CellData>(address, cellData)]);
    }

    public void SetCells(IEnumerable<KeyValuePair<CellAddress, CellData>> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var requested = new Dictionary<CellAddress, CellData>();
        foreach (var pair in changes)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            requested[ResolveMergedAnchor(pair.Key)] = pair.Value;
        }

        if (requested.Count == 0)
        {
            return;
        }

        var changed = false;
        var top = int.MaxValue;
        var left = int.MaxValue;
        var bottom = int.MinValue;
        var right = int.MinValue;

        foreach (var (address, cellData) in requested)
        {
            if (GetCell(address) == cellData)
            {
                continue;
            }

            if (cellData.IsEmpty)
            {
                _cells.Remove(address);
            }
            else
            {
                _cells[address] = cellData;
            }

            changed = true;
            top = Math.Min(top, address.RowIndex);
            left = Math.Min(left, address.ColumnIndex);
            bottom = Math.Max(bottom, address.RowIndex);
            right = Math.Max(right, address.ColumnIndex);
        }

        if (!changed)
        {
            return;
        }

        Version++;
        var range = new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
        CellsChanged?.Invoke(this, new CellsChangedEventArgs(range, Version));
    }
}
