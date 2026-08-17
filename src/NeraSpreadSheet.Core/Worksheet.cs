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
    private static readonly char[] InvalidNameCharacters = ['[', ']', ':', '*', '?', '/', '\\'];
    private readonly Dictionary<CellAddress, CellData> _cells = [];

    internal Worksheet(string name)
    {
        Name = name;
        Dimensions = new WorksheetDimensions();
        MergedCells = new MergedCellRanges();
    }

    public string Name { get; internal set; }
    public WorksheetDimensions Dimensions { get; }
    public MergedCellRanges MergedCells { get; }
    public long Version { get; private set; }
    public int UsedCellCount => _cells.Count;
    public event EventHandler<CellsChangedEventArgs>? CellsChanged;

    public CellData GetCell(CellAddress address) => _cells.GetValueOrDefault(address, CellData.Empty);

    public object? GetValue(CellAddress address) =>
        GetCell(ResolveMergedAnchor(address)).Value.RawValue;

    public string? GetFormula(CellAddress address) =>
        GetCell(ResolveMergedAnchor(address)).Formula;

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

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > SpreadsheetLimits.MaxWorksheetNameLength)
        {
            throw new ArgumentException(
                $"Worksheet names cannot exceed {SpreadsheetLimits.MaxWorksheetNameLength} characters.",
                nameof(name));
        }
        if (normalized.IndexOfAny(InvalidNameCharacters) >= 0)
        {
            throw new ArgumentException("Worksheet name contains an invalid character.", nameof(name));
        }
        if (normalized.StartsWith('\'') || normalized.EndsWith('\''))
        {
            throw new ArgumentException("Worksheet name cannot start or end with an apostrophe.", nameof(name));
        }

        Name = normalized;
    }

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

    internal WorksheetStructuralState CaptureStructuralState() => new(
        _cells.ToArray(),
        Dimensions.GetRowOverrides().ToArray(),
        Dimensions.GetColumnOverrides().ToArray(),
        MergedCells.Ranges.ToArray());

    internal void ApplyStructuralChange(WorksheetStructuralChange change)
    {
        var transformedCells = CreateStructuralCells(change);
        var transformedDimensions = Dimensions.CreateStructuralOverrides(change);
        var transformedMergedCells = MergedCells.CreateStructuralRanges(change);

        _cells.Clear();
        foreach (var (address, cell) in transformedCells)
        {
            _cells.Add(address, cell);
        }
        Dimensions.ReplaceStructuralOverrides(change, transformedDimensions);
        MergedCells.ReplaceAll(transformedMergedCells);
        PublishStructuralChange(change);
    }

    internal void RestoreStructuralState(
        WorksheetStructuralState state,
        WorksheetStructuralChange signalChange)
    {
        ArgumentNullException.ThrowIfNull(state);

        _cells.Clear();
        foreach (var (address, cell) in state.Cells)
        {
            _cells.Add(address, cell);
        }
        Dimensions.RestoreOverrides(state.RowHeights, state.ColumnWidths, signalChange);
        MergedCells.ReplaceAll(state.MergedCells);
        PublishStructuralChange(signalChange);
    }

    private Dictionary<CellAddress, CellData> CreateStructuralCells(WorksheetStructuralChange change)
    {
        var transformed = new Dictionary<CellAddress, CellData>(_cells.Count);
        foreach (var (address, cell) in _cells)
        {
            if (!change.TryMapAddress(address, out var mappedAddress))
            {
                if (change.Kind == WorksheetStructuralChangeKind.Insert)
                {
                    throw new InvalidOperationException(
                        "Cannot insert because a used cell would move outside the worksheet bounds.");
                }
                continue;
            }
            transformed.Add(mappedAddress, cell);
        }
        return transformed;
    }

    private void PublishStructuralChange(WorksheetStructuralChange change)
    {
        Version++;
        var range = change.Axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(change.Index, 0),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1))
            : new CellRange(
                new CellAddress(0, change.Index),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1));
        CellsChanged?.Invoke(this, new CellsChangedEventArgs(range, Version));
    }
}
