using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

internal readonly record struct WorksheetAxisStyleMutation(
    WorksheetAxis Axis,
    int StartIndex,
    int EndIndex,
    CellStylePatch Patch)
{
    public WorksheetAxisStyleMutation
    {
        if (!Enum.IsDefined(Axis))
        {
            throw new ArgumentOutOfRangeException(nameof(Axis));
        }
        ArgumentNullException.ThrowIfNull(Patch);
        if (Patch.IsEmpty)
        {
            throw new ArgumentException(
                "An axis style mutation must change at least one property.",
                nameof(Patch));
        }
        var axisLength = Axis == WorksheetAxis.Row
            ? SpreadsheetLimits.MaxRows
            : SpreadsheetLimits.MaxColumns;
        if (StartIndex < 0 || StartIndex >= axisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(StartIndex));
        }
        if (EndIndex < StartIndex || EndIndex >= axisLength)
        {
            throw new ArgumentOutOfRangeException(nameof(EndIndex));
        }
    }

    public bool Contains(CellAddress address) =>
        Axis == WorksheetAxis.Row
            ? address.RowIndex >= StartIndex && address.RowIndex <= EndIndex
            : address.ColumnIndex >= StartIndex &&
              address.ColumnIndex <= EndIndex;
}

internal sealed class SetWorksheetStylesOperation : ISpreadsheetEditOperation
{
    private readonly Worksheet _worksheet;
    private readonly CellStyleCatalog _styles;
    private readonly WorksheetAxisStyleMutation[] _axisMutations;
    private readonly CellRange[] _finiteRanges;
    private readonly Func<CellStyle, CellStyle> _transform;
    private readonly CellRange _signalRange;
    private WorksheetAxisStyleState? _beforeAxisState;
    private WorksheetAxisStyleState? _afterAxisState;
    private KeyValuePair<CellAddress, CellData>[]? _beforeCells;
    private KeyValuePair<CellAddress, CellData>[]? _afterCells;

    public SetWorksheetStylesOperation(
        Worksheet worksheet,
        CellStyleCatalog styles,
        IEnumerable<WorksheetAxisStyleMutation> axisMutations,
        IEnumerable<CellRange> finiteRanges,
        Func<CellStyle, CellStyle> transform,
        IEnumerable<CellRange> affectedRanges)
    {
        _worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        ArgumentNullException.ThrowIfNull(axisMutations);
        ArgumentNullException.ThrowIfNull(finiteRanges);
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
        ArgumentNullException.ThrowIfNull(affectedRanges);
        _axisMutations = axisMutations.ToArray();
        _finiteRanges = finiteRanges.ToArray();
        AffectedRanges = affectedRanges.ToArray();
        if (AffectedRanges.Count == 0)
        {
            throw new ArgumentException(
                "At least one affected range is required.",
                nameof(affectedRanges));
        }
        _signalRange = CreateBoundingRange(AffectedRanges);
    }

    public string Description => "Format cells";

    public IReadOnlyList<CellRange> AffectedRanges { get; }

    public void Apply()
    {
        if (_afterAxisState is not null && _afterCells is not null)
        {
            _worksheet.RestoreAxisStyleState(
                _afterAxisState,
                _signalRange);
            _worksheet.SetCells(_afterCells);
            return;
        }

        _beforeAxisState = _worksheet.CaptureAxisStyleState();
        var addresses = CollectChangedAddresses();
        _beforeCells = addresses
            .Select(address => new KeyValuePair<CellAddress, CellData>(
                address,
                _worksheet.GetCell(address)))
            .ToArray();

        try
        {
            foreach (var mutation in _axisMutations)
            {
                _worksheet.ApplyAxisStyle(
                    mutation.Axis,
                    mutation.StartIndex,
                    mutation.EndIndex,
                    mutation.Patch);
            }

            var updates = CreateCellUpdates(addresses);
            _worksheet.SetCells(updates);
            _afterAxisState = _worksheet.CaptureAxisStyleState();
            _afterCells = addresses
                .Select(address => new KeyValuePair<CellAddress, CellData>(
                    address,
                    _worksheet.GetCell(address)))
                .ToArray();
        }
        catch
        {
            _worksheet.RestoreAxisStyleState(
                _beforeAxisState,
                _signalRange);
            _worksheet.SetCells(_beforeCells);
            throw;
        }
    }

    public void Undo()
    {
        if (_beforeAxisState is null || _beforeCells is null)
        {
            throw new InvalidOperationException(
                "The style operation has not been applied.");
        }

        _worksheet.RestoreAxisStyleState(
            _beforeAxisState,
            _signalRange);
        _worksheet.SetCells(_beforeCells);
    }

    private CellAddress[] CollectChangedAddresses()
    {
        var addresses = new HashSet<CellAddress>();
        if (_axisMutations.Length != 0)
        {
            foreach (var (address, cell) in _worksheet.EnumerateUsedCells())
            {
                if (cell.StyleId != CellStyleCatalog.DefaultStyleId &&
                    IsCoveredByAxisMutation(address))
                {
                    addresses.Add(address);
                }
            }
        }

        foreach (var range in _finiteRanges)
        {
            foreach (var address in range.EnumerateCells())
            {
                if (!IsCoveredByAxisMutation(address))
                {
                    addresses.Add(_worksheet.ResolveMergedAnchor(address));
                }
            }
        }
        return addresses.OrderBy(static address => address).ToArray();
    }

    private KeyValuePair<CellAddress, CellData>[] CreateCellUpdates(
        IEnumerable<CellAddress> addresses)
    {
        var updates = new List<KeyValuePair<CellAddress, CellData>>();
        foreach (var address in addresses)
        {
            var current = _worksheet.GetCell(address);
            CellStyle nextStyle;
            if (current.StyleId != CellStyleCatalog.DefaultStyleId &&
                IsCoveredByAxisMutation(address))
            {
                nextStyle = _styles.Get(current.StyleId);
                foreach (var mutation in _axisMutations)
                {
                    if (mutation.Contains(address))
                    {
                        nextStyle = mutation.Patch.Apply(nextStyle);
                    }
                }
            }
            else
            {
                nextStyle = _transform(
                    _worksheet.GetEffectiveStyle(address, _styles));
            }

            var styleId = _styles.Intern(nextStyle);
            if (styleId == current.StyleId)
            {
                continue;
            }
            updates.Add(new KeyValuePair<CellAddress, CellData>(
                address,
                new CellData(
                    current.Value,
                    current.Formula,
                    styleId)));
        }
        return updates.ToArray();
    }

    private bool IsCoveredByAxisMutation(CellAddress address)
    {
        foreach (var mutation in _axisMutations)
        {
            if (mutation.Contains(address))
            {
                return true;
            }
        }
        return false;
    }

    private static CellRange CreateBoundingRange(
        IReadOnlyList<CellRange> ranges)
    {
        var top = ranges.Min(static range => range.Top);
        var left = ranges.Min(static range => range.Left);
        var bottom = ranges.Max(static range => range.Bottom);
        var right = ranges.Max(static range => range.Right);
        return new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right));
    }
}
