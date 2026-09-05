using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Host-facing convenience surface for native Table-filter presenters.
/// The authoritative mutable state remains owned by <see cref="SpreadsheetTableFilterMenu"/>.
/// </summary>
public static class SpreadsheetTableFilterMenuHostExtensions
{
    extension(SpreadsheetTableFilterMenu menu)
    {
        public IReadOnlyList<SpreadsheetTableFilterValueItem> GetVisibleItems() =>
            menu.Capture().Values;

        public void SelectValue(CellValue value, bool selected) =>
            menu.SetSelected(value, selected);

        public bool CanApplyValueSelection =>
            menu.Capture().CanApplyValueSelection;

        public bool ValuesTruncated =>
            menu.Capture().IsTruncated;

        public int DistinctValueCount =>
            menu.Capture().DistinctValueCount;
    }
}
