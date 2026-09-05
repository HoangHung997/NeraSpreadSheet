using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Interaction;

public sealed record SelectionSnapshot(
    CellAddress ActiveCell,
    CellAddress AnchorCell,
    IReadOnlyList<CellRange> Ranges,
    long Version)
{
    public bool Contains(CellAddress address) => Ranges.Any(range => range.Contains(address));
}
