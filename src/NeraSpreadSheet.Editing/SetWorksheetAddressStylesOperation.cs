using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// One undoable finite-selection style operation whose result may depend on cell position.
/// Used for selection-edge semantics such as Outline/Inside borders.
/// </summary>
internal sealed class SetWorksheetAddressStylesOperation : ISpreadsheetEditOperation
{
    private readonly CellStyleCatalog _styles;
    private readonly CellRange[] _ranges;
    private readonly Func<CellAddress, CellStyle, CellStyle> _transform;
    private KeyValuePair<CellAddress, CellData>[]? _before;
    private KeyValuePair<CellAddress, CellData>[]? _after;

    public SetWorksheetAddressStylesOperation(
        Worksheet worksheet,
        CellStyleCatalog styles,
        IEnumerable<CellRange> ranges,
        Func<CellAddress, CellStyle, CellStyle> transform,
        string description)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges = ranges.ToArray();
        if (_ranges.Length == 0)
        {
            throw new ArgumentException("At least one affected range is required.", nameof(ranges));
        }
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description.Trim();
        AffectedRanges = _ranges;
        AffectedRange = new CellRange(
            new CellAddress(_ranges.Min(static range => range.Top), _ranges.Min(static range => range.Left)),
            new CellAddress(_ranges.Max(static range => range.Bottom), _ranges.Max(static range => range.Right)));
    }

    public string Description { get; }
    public Worksheet Worksheet { get; }
    public CellRange AffectedRange { get; }
    public IReadOnlyList<CellRange> AffectedRanges { get; }
    public bool AffectsCalculation => false;

    public void Execute()
    {
        if (_after is not null)
        {
            Worksheet.SetCells(_after);
            return;
        }

        var addresses = CollectAddresses();
        _before = Capture(addresses);
        var updates = new List<KeyValuePair<CellAddress, CellData>>(addresses.Length);
        foreach (var address in addresses)
        {
            var current = Worksheet.GetCell(address);
            var effective = Worksheet.GetEffectiveStyle(address, _styles);
            var next = _transform(address, effective) ??
                throw new InvalidOperationException("Style transform returned null.");
            var styleId = _styles.Intern(next);
            if (styleId != current.StyleId)
            {
                updates.Add(new KeyValuePair<CellAddress, CellData>(
                    address,
                    new CellData(current.Value, current.Formula, styleId)));
            }
        }
        if (updates.Count != 0)
        {
            Worksheet.SetCells(updates);
        }
        _after = Capture(addresses);
    }

    public void Undo()
    {
        if (_before is null)
        {
            throw new InvalidOperationException("The style operation has not been executed.");
        }
        Worksheet.SetCells(_before);
    }

    private CellAddress[] CollectAddresses()
    {
        var result = new HashSet<CellAddress>();
        foreach (var range in _ranges)
        {
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    result.Add(Worksheet.ResolveMergedAnchor(new CellAddress(row, column)));
                }
            }
        }
        return result
            .OrderBy(static address => address.RowIndex)
            .ThenBy(static address => address.ColumnIndex)
            .ToArray();
    }

    private KeyValuePair<CellAddress, CellData>[] Capture(CellAddress[] addresses) =>
        addresses.Select(address => new KeyValuePair<CellAddress, CellData>(
            address,
            Worksheet.GetCell(address))).ToArray();
}