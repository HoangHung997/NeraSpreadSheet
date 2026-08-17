using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Interaction;

public sealed class SelectionChangedEventArgs : EventArgs
{
    public SelectionChangedEventArgs(SelectionSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public SelectionSnapshot Snapshot { get; }
}

public sealed class SelectionModel
{
    private readonly List<CellRange> _ranges = [new CellRange(default, default)];

    public CellAddress ActiveCell { get; private set; }

    public CellAddress AnchorCell { get; private set; }

    public IReadOnlyList<CellRange> Ranges => _ranges;

    public long Version { get; private set; }

    public event EventHandler<SelectionChangedEventArgs>? Changed;

    public SelectionSnapshot Capture() => new(ActiveCell, AnchorCell, _ranges.ToArray(), Version);

    public bool Contains(CellAddress address) => _ranges.Any(range => range.Contains(address));

    public void SetActiveCell(CellAddress address, bool preserveRanges = false, bool preserveAnchor = false)
    {
        var keepRanges = preserveRanges || preserveAnchor;
        var changed = ActiveCell != address;
        ActiveCell = address;

        if (!preserveAnchor)
        {
            changed |= AnchorCell != address;
            AnchorCell = address;
        }

        if (!keepRanges)
        {
            var singleCellRange = new CellRange(address, address);
            changed |= _ranges.Count != 1 || _ranges[0] != singleCellRange;
            _ranges.Clear();
            _ranges.Add(singleCellRange);
        }

        PublishIfChanged(changed);
    }

    public void Select(CellRange range, bool additive = false)
    {
        var changed = false;
        if (!additive)
        {
            changed = _ranges.Count != 1 || _ranges[0] != range;
            _ranges.Clear();
            _ranges.Add(range);
        }
        else if (!_ranges.Contains(range))
        {
            _ranges.Add(range);
            changed = true;
        }

        var newActive = range.TopLeft;
        changed |= ActiveCell != newActive || AnchorCell != newActive;
        ActiveCell = newActive;
        AnchorCell = newActive;
        PublishIfChanged(changed);
    }

    public void SelectRow(int rowIndex, bool additive = false) =>
        Select(
            new CellRange(
                new CellAddress(rowIndex, 0),
                new CellAddress(rowIndex, SpreadsheetLimits.MaxColumns - 1)),
            additive);

    public void SelectColumn(int columnIndex, bool additive = false) =>
        Select(
            new CellRange(
                new CellAddress(0, columnIndex),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, columnIndex)),
            additive);

    public void SelectAll() =>
        Select(new CellRange(
            default,
            new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1)));

    public void ExtendRowsTo(int rowIndex)
    {
        var active = new CellAddress(rowIndex, 0);
        var range = new CellRange(
            new CellAddress(AnchorCell.RowIndex, 0),
            new CellAddress(rowIndex, SpreadsheetLimits.MaxColumns - 1));
        var changed = ActiveCell != active || _ranges.Count != 1 || _ranges[0] != range;
        ActiveCell = active;
        _ranges.Clear();
        _ranges.Add(range);
        PublishIfChanged(changed);
    }

    public void ExtendColumnsTo(int columnIndex)
    {
        var active = new CellAddress(0, columnIndex);
        var range = new CellRange(
            new CellAddress(0, AnchorCell.ColumnIndex),
            new CellAddress(SpreadsheetLimits.MaxRows - 1, columnIndex));
        var changed = ActiveCell != active || _ranges.Count != 1 || _ranges[0] != range;
        ActiveCell = active;
        _ranges.Clear();
        _ranges.Add(range);
        PublishIfChanged(changed);
    }

    public void ExtendTo(CellAddress address)
    {
        var range = new CellRange(AnchorCell, address);
        var changed = ActiveCell != address || _ranges.Count != 1 || _ranges[0] != range;
        ActiveCell = address;
        _ranges.Clear();
        _ranges.Add(range);
        PublishIfChanged(changed);
    }

    public void AddRange(CellRange range)
    {
        if (_ranges.Contains(range))
        {
            return;
        }

        _ranges.Add(range);
        ActiveCell = range.TopLeft;
        AnchorCell = range.TopLeft;
        PublishIfChanged(true);
    }

    public void ClearAdditionalRanges()
    {
        var primary = new CellRange(ActiveCell, ActiveCell);
        if (_ranges.Count == 1 && _ranges[0] == primary)
        {
            return;
        }

        _ranges.Clear();
        _ranges.Add(primary);
        AnchorCell = ActiveCell;
        PublishIfChanged(true);
    }

    private void PublishIfChanged(bool changed)
    {
        if (!changed)
        {
            return;
        }

        Version++;
        Changed?.Invoke(this, new SelectionChangedEventArgs(Capture()));
    }
}
