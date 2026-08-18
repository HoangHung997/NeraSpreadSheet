using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

internal readonly record struct WorksheetAxisStyleMutation
{
    public WorksheetAxisStyleMutation(
        WorksheetAxis axis,
        int startIndex,
        int endIndex,
        CellStylePatch patch)
    {
        if (!Enum.IsDefined(axis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.IsEmpty)
        {
            throw new ArgumentException(
                "An axis style mutation must change at least one property.",
                nameof(patch));
        }

        var axisLength = axis == WorksheetAxis.Row
            ? SpreadsheetLimits.MaxRows
            : SpreadsheetLimits.MaxColumns;
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            startIndex,
            axisLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(endIndex, startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            endIndex,
            axisLength);

        Axis = axis;
        StartIndex = startIndex;
        EndIndex = endIndex;
        Patch = patch;
    }

    public WorksheetAxis Axis { get; }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public CellStylePatch Patch { get; }

    public bool Contains(CellAddress address) =>
        Axis == WorksheetAxis.Row
            ? address.RowIndex >= StartIndex && address.RowIndex <= EndIndex
            : address.ColumnIndex >= StartIndex &&
              address.ColumnIndex <= EndIndex;
}

internal sealed class SetWorksheetStylesOperation : ISpreadsheetEditOperation
{
    private readonly CellStyleCatalog _styles;
    private readonly WorksheetAxisStyleMutation[] _axisMutations;
    private readonly CellRange[] _finiteRanges;
    private readonly Func<CellStyle, CellStyle> _transform;
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
        IEnumerable<CellRange> affectedRanges,
        string description)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        ArgumentNullException.ThrowIfNull(axisMutations);
        ArgumentNullException.ThrowIfNull(finiteRanges);
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
        ArgumentNullException.ThrowIfNull(affectedRanges);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _axisMutations = axisMutations.ToArray();
        _finiteRanges = finiteRanges.ToArray();
        AffectedRanges = affectedRanges.ToArray();
        if (AffectedRanges.Count == 0)
        {
            throw new ArgumentException(
                "At least one affected range is required.",
                nameof(affectedRanges));
        }
        if (_axisMutations.Length == 0 && _finiteRanges.Length == 0)
        {
            throw new ArgumentException(
                "At least one axis mutation or finite range is required.",
                nameof(axisMutations));
        }

        Description = description.Trim();
        AffectedRange = CreateBoundingRange(AffectedRanges.ToArray());
    }

    public string Description { get; }

    public Worksheet Worksheet { get; }

    public CellRange AffectedRange { get; }

    public IReadOnlyList<CellRange> AffectedRanges { get; }

    public void Execute()
    {
        if (_afterAxisState is not null && _afterCells is not null)
        {
            Worksheet.RestoreAxisStyleState(
                _afterAxisState,
                AffectedRange);
            if (_afterCells.Length != 0)
            {
                Worksheet.SetCells(_afterCells);
            }
            return;
        }

        _beforeAxisState = Worksheet.CaptureAxisStyleState();
        var addresses = CollectChangedAddresses();
        _beforeCells = CaptureCells(addresses);
        var updates = CreateCellUpdates(addresses);

        try
        {
            foreach (var mutation in _axisMutations)
            {
                Worksheet.ApplyAxisStyle(
                    mutation.Axis,
                    mutation.StartIndex,
                    mutation.EndIndex,
                    mutation.Patch);
            }
            if (updates.Length != 0)
            {
                Worksheet.SetCells(updates);
            }

            _afterAxisState = Worksheet.CaptureAxisStyleState();
            _afterCells = CaptureCells(addresses);
        }
        catch
        {
            Worksheet.RestoreAxisStyleState(
                _beforeAxisState,
                AffectedRange);
            if (_beforeCells.Length != 0)
            {
                Worksheet.SetCells(_beforeCells);
            }
            throw;
        }
    }

    public void Undo()
    {
        if (_beforeAxisState is null || _beforeCells is null)
        {
            throw new InvalidOperationException(
                "The style operation has not been executed.");
        }

        Worksheet.RestoreAxisStyleState(
            _beforeAxisState,
            AffectedRange);
        if (_beforeCells.Length != 0)
        {
            Worksheet.SetCells(_beforeCells);
        }
    }

    private CellAddress[] CollectChangedAddresses()
    {
        var addresses = new HashSet<CellAddress>();
        if (_axisMutations.Length != 0)
        {
            foreach (var (address, cell) in Worksheet.EnumerateUsedCells())
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
                    addresses.Add(Worksheet.ResolveMergedAnchor(address));
                }
            }
        }
        return addresses.OrderBy(static address => address).ToArray();
    }

    private KeyValuePair<CellAddress, CellData>[] CaptureCells(
        CellAddress[] addresses) =>
        addresses
            .Select(address => new KeyValuePair<CellAddress, CellData>(
                address,
                Worksheet.GetCell(address)))
            .ToArray();

    private KeyValuePair<CellAddress, CellData>[] CreateCellUpdates(
        CellAddress[] addresses)
    {
        var updates = new List<KeyValuePair<CellAddress, CellData>>();
        foreach (var address in addresses)
        {
            var current = Worksheet.GetCell(address);
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
                    Worksheet.GetEffectiveStyle(address, _styles)) ??
                    throw new InvalidOperationException(
                        "Style transform returned null.");
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

    private static CellRange CreateBoundingRange(CellRange[] ranges)
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
