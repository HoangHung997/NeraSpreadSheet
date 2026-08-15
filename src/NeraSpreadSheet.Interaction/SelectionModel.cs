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

    public void SetActiveCell(CellAddress address, bool preserveRanges = false)
    {
        var changed = ActiveCell != address || AnchorCell != address;
        ActiveCell = address;
        AnchorCell = address;

        if (!preserveRanges)
        {
            changed |= _ranges.Count != 1 || _ranges[0] != new CellRange(address, address);
            _ranges.Clear();
            _ranges.Add(new CellRange(address, address));
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
