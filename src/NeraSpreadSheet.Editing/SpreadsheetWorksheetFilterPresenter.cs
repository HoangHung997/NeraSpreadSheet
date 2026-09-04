using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public readonly record struct SpreadsheetWorksheetFilterTarget(
    CellRange FilterRange,
    int ColumnOffset,
    int WorksheetColumnIndex,
    CellAddress HeaderCell,
    bool IsFiltered);

public sealed record SpreadsheetWorksheetFilterValuePage(
    CellRange FilterRange,
    int ColumnOffset,
    int WorksheetColumnIndex,
    string ColumnName,
    string SearchText,
    int Offset,
    int PageSize,
    int TotalVisibleValueCount,
    bool HasPreviousPage,
    bool HasNextPage,
    bool IsSourceTruncated,
    IReadOnlyList<SpreadsheetAutoFilterMenuKind> MenuKinds,
    IReadOnlyList<SpreadsheetTableFilterValueItem> Values);

public sealed class SpreadsheetWorksheetFilterMenu
{
    public const int MaximumPageSize =
        SpreadsheetTableFilterMenu.MaximumPageSize;

    private readonly SpreadsheetWorksheetFilterPresenterController _owner;
    private readonly Dictionary<CellValue, int> _counts;
    private readonly HashSet<CellValue> _selected;
    private string _searchText = string.Empty;

    internal SpreadsheetWorksheetFilterMenu(
        SpreadsheetWorksheetFilterPresenterController owner,
        WorksheetAutoFilter autoFilter,
        int columnOffset,
        string columnName,
        Dictionary<CellValue, int> counts,
        WorksheetAutoFilterColumn? currentFilter,
        int sourceRowCount,
        int scannedRowCount,
        bool isRowScanTruncated,
        bool isDistinctValueTruncated)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ArgumentNullException.ThrowIfNull(autoFilter);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        _counts = counts ?? throw new ArgumentNullException(nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(columnOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            columnOffset,
            autoFilter.Range.ColumnCount);

        FilterRange = autoFilter.Range;
        ColumnOffset = columnOffset;
        WorksheetColumnIndex = checked(autoFilter.Range.Left + columnOffset);
        HeaderCell = new CellAddress(
            autoFilter.Range.Top,
            WorksheetColumnIndex);
        ColumnName = columnName;
        SourceRowCount = sourceRowCount;
        ScannedRowCount = scannedRowCount;
        IsRowScanTruncated = isRowScanTruncated;
        IsDistinctValueTruncated = isDistinctValueTruncated;
        HasActiveFilter = currentFilter is not null;
        HasCustomFilter = currentFilter is not null &&
            (currentFilter.FirstCondition is not null ||
             currentFilter.DateGroups.Count > 0 ||
             currentFilter.TopBottom is not null ||
             currentFilter.DynamicFilter is not null ||
             currentFilter.ColorFilter is not null ||
             currentFilter.IconFilter is not null);

        _selected = currentFilter is null || HasCustomFilter
            ? _counts.Keys.ToHashSet()
            : currentFilter.Values.ToHashSet();
        if (currentFilter?.IncludeBlank == true)
        {
            _selected.Add(CellValue.Blank);
        }
    }

    public CellRange FilterRange { get; }

    public int ColumnOffset { get; }

    public int WorksheetColumnIndex { get; }

    public CellAddress HeaderCell { get; }

    public string ColumnName { get; }

    public int SourceRowCount { get; }

    public int ScannedRowCount { get; }

    public bool IsRowScanTruncated { get; }

    public bool IsDistinctValueTruncated { get; }

    public bool IsTruncated =>
        IsRowScanTruncated || IsDistinctValueTruncated;

    public bool HasActiveFilter { get; }

    public bool HasCustomFilter { get; }

    public string SearchText => _searchText;

    public bool CanApplyValueSelection => _selected.Count > 0;

    public event EventHandler? Changed;

    public SpreadsheetWorksheetFilterValuePage CapturePage(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            pageSize,
            MaximumPageSize);
        cancellationToken.ThrowIfCancellationRequested();

        var visible = GetVisibleValues(cancellationToken);
        var page = visible
            .Skip(offset)
            .Take(pageSize)
            .Select(item =>
                new SpreadsheetTableFilterValueItem(
                    item.Value,
                    item.DisplayText,
                    item.Count,
                    _selected.Contains(item.Value)))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new SpreadsheetWorksheetFilterValuePage(
            FilterRange,
            ColumnOffset,
            WorksheetColumnIndex,
            ColumnName,
            _searchText,
            offset,
            pageSize,
            visible.Count,
            offset > 0,
            checked(offset + page.Length) < visible.Count,
            IsTruncated,
            SpreadsheetAutoFilterRichProjection.GetMenuKinds(_counts.Keys),
            page);
    }

    public SpreadsheetAutoFilterDatePage CaptureDatePage(
        long generation,
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        SpreadsheetAutoFilterRichProjection.CaptureDatePage(
            _counts,
            generation,
            parent,
            offset,
            pageSize,
            cancellationToken);

    public void SetSearchText(string? searchText)
    {
        var normalized = searchText?.Trim() ?? string.Empty;
        if (string.Equals(
                _searchText,
                normalized,
                StringComparison.Ordinal))
        {
            return;
        }

        _searchText = normalized;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSelected(CellValue value, bool selected)
    {
        if (!_counts.ContainsKey(value))
        {
            throw new ArgumentException(
                "The value is not part of this worksheet filter menu.",
                nameof(value));
        }

        var changed = selected
            ? _selected.Add(value)
            : _selected.Remove(value);
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectAllVisible()
    {
        var changed = false;
        foreach (var item in GetVisibleValues(CancellationToken.None))
        {
            changed |= _selected.Add(item.Value);
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearVisibleSelection()
    {
        var changed = false;
        foreach (var item in GetVisibleValues(CancellationToken.None))
        {
            changed |= _selected.Remove(item.Value);
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyValueSelection() =>
        _owner.ApplyValueSelection(this);

    public void ApplyCustomFilter(
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true) =>
        _owner.ApplyCustomFilter(
            WorksheetColumnIndex,
            firstCondition,
            secondCondition,
            combineWithAnd);

    public void ApplyRichFilter(SpreadsheetAutoFilterRichCriterion criterion) =>
        _owner.ApplyRichFilter(WorksheetColumnIndex, criterion);

    public void ClearColumnFilter() =>
        _owner.ClearColumnFilter(WorksheetColumnIndex);

    internal CellValue[] GetSelectedValues() =>
        _selected
            .OrderBy(static value => value.Kind)
            .ThenBy(
                static value => FormatValue(value),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal bool RepresentsEveryEnumeratedValue =>
        _selected.Count == _counts.Count &&
        _counts.Keys.All(_selected.Contains);

    private List<ValueCount> GetVisibleValues(
        CancellationToken cancellationToken)
    {
        var result = new List<ValueCount>(_counts.Count);
        var index = 0;
        foreach (var pair in _counts)
        {
            if ((index++ & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = new ValueCount(
                pair.Key,
                FormatValue(pair.Key),
                pair.Value);
            if (_searchText.Length == 0 ||
                item.DisplayText.Contains(
                    _searchText,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(item);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        result.Sort(static (left, right) =>
        {
            var blank = (left.Value.IsBlank ? 0 : 1)
                .CompareTo(right.Value.IsBlank ? 0 : 1);
            if (blank != 0)
            {
                return blank;
            }
            var text = StringComparer.OrdinalIgnoreCase.Compare(
                left.DisplayText,
                right.DisplayText);
            return text != 0
                ? text
                : left.Value.Kind.CompareTo(right.Value.Kind);
        });
        return result;
    }

    private static string FormatValue(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => "(Blank)",
            CellValueKind.DateTime => ((DateTime)value.RawValue!).ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            CellValueKind.Boolean =>
                (bool)value.RawValue! ? "TRUE" : "FALSE",
            _ => value.ToString(),
        };

    private readonly record struct ValueCount(
        CellValue Value,
        string DisplayText,
        int Count);
}

public sealed class SpreadsheetWorksheetFilterPresenterController
{
    public const int DefaultMaximumRows =
        SpreadsheetTablePresenterController.DefaultMaximumRows;
    public const int DefaultMaximumDistinctValues =
        SpreadsheetTablePresenterController.DefaultMaximumDistinctValues;

    private readonly SpreadsheetSession _session;

    public SpreadsheetWorksheetFilterPresenterController(
        SpreadsheetSession session)
    {
        _session = session ??
            throw new ArgumentNullException(nameof(session));
    }

    public SpreadsheetWorksheetFilterMenu OpenFilterMenu(
        int worksheetColumnIndex,
        int maximumRows = DefaultMaximumRows,
        int maximumDistinctValues = DefaultMaximumDistinctValues)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worksheetColumnIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumDistinctValues);

        var filter = RequireCurrent();
        var columnOffset =
            worksheetColumnIndex - filter.Range.Left;
        if (columnOffset < 0 ||
            columnOffset >= filter.Range.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worksheetColumnIndex),
                worksheetColumnIndex,
                "The column must belong to the worksheet AutoFilter range.");
        }

        var counts = new Dictionary<CellValue, int>();
        var sourceRowCount = filter.DataRange?.RowCount ?? 0;
        var scannedRowCount = 0;
        var distinctTruncated = false;
        if (filter.DataRange is { } dataRange)
        {
            var snapshot = WorksheetSnapshot.Capture(
                _session.ActiveWorksheet);
            var rowLimit = Math.Min(
                dataRange.RowCount,
                maximumRows);
            for (var offset = 0; offset < rowLimit; offset++)
            {
                var value = snapshot.GetCell(new CellAddress(
                    checked(dataRange.Top + offset),
                    worksheetColumnIndex)).Value;
                scannedRowCount++;
                if (counts.TryGetValue(value, out var count))
                {
                    counts[value] = checked(count + 1);
                }
                else if (counts.Count < maximumDistinctValues)
                {
                    counts.Add(value, 1);
                }
                else
                {
                    distinctTruncated = true;
                }
            }
        }

        var currentFilter = filter.Columns
            .FirstOrDefault(candidate =>
                candidate.ColumnOffset == columnOffset)
            ?.Copy();
        var header = _session.ActiveWorksheet.GetCell(
            new CellAddress(
                filter.Range.Top,
                worksheetColumnIndex)).Value;
        var columnName = header.IsBlank
            ? new CellAddress(
                filter.Range.Top,
                worksheetColumnIndex).ToA1()
            : header.ToString();
        return new SpreadsheetWorksheetFilterMenu(
            this,
            filter,
            columnOffset,
            columnName,
            counts,
            currentFilter,
            sourceRowCount,
            scannedRowCount,
            sourceRowCount > maximumRows,
            distinctTruncated);
    }

    public void ApplyValueSelection(
        SpreadsheetWorksheetFilterMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        EnsureMenuBelongsToCurrentFilter(menu);
        var selected = menu.GetSelectedValues();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one value must be selected before applying a value filter.");
        }

        if (!menu.IsTruncated &&
            menu.RepresentsEveryEnumeratedValue)
        {
            _session.WorksheetFilter.ClearColumnFilter(
                menu.WorksheetColumnIndex);
            return;
        }

        _session.WorksheetFilter.ApplyValueFilter(
            menu.WorksheetColumnIndex,
            selected,
            selected.Any(static value => value.IsBlank));
    }

    public void ApplyCustomFilter(
        int worksheetColumnIndex,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        _session.WorksheetFilter.ApplyCustomFilter(
            worksheetColumnIndex,
            firstCondition,
            secondCondition,
            combineWithAnd);
    }

    public void ApplyRichFilter(
        int worksheetColumnIndex,
        SpreadsheetAutoFilterRichCriterion criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        var current = RequireCurrent();
        var columnOffset = worksheetColumnIndex - current.Range.Left;
        if (columnOffset < 0 || columnOffset >= current.Range.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(worksheetColumnIndex));
        }
        var columns = current.Columns
            .Where(column => column.ColumnOffset != columnOffset)
            .Select(static column => column.Copy())
            .Append(criterion.CreateWorksheetColumn(columnOffset));
        _session.WorksheetFilter.SetAutoFilter(current.WithColumns(columns));
    }

    public void ClearColumnFilter(int worksheetColumnIndex) =>
        _session.WorksheetFilter.ClearColumnFilter(
            worksheetColumnIndex);

    private WorksheetAutoFilter RequireCurrent() =>
        _session.ActiveWorksheet.AutoFilter ??
        throw new InvalidOperationException(
            "The active worksheet does not have a direct AutoFilter range.");

    private void EnsureMenuBelongsToCurrentFilter(
        SpreadsheetWorksheetFilterMenu menu)
    {
        var current = RequireCurrent();
        if (current.Range != menu.FilterRange ||
            menu.ColumnOffset < 0 ||
            menu.ColumnOffset >= current.Range.ColumnCount ||
            current.Range.Left + menu.ColumnOffset !=
            menu.WorksheetColumnIndex)
        {
            throw new InvalidOperationException(
                "The worksheet AutoFilter changed after the menu was created.");
        }
    }
}

public static class SpreadsheetWorksheetFilterTargetResolver
{
    public static bool TryResolveActiveWorksheetFilterTarget(
        this SpreadsheetSession session,
        out SpreadsheetWorksheetFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.TryResolveWorksheetFilterTarget(
            session.Selection.ActiveCell,
            out target);
    }

    public static bool TryResolveWorksheetFilterTarget(
        this SpreadsheetSession session,
        CellAddress address,
        out SpreadsheetWorksheetFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        var filter = session.ActiveWorksheet.AutoFilter;
        if (filter is null ||
            !filter.Range.Contains(address))
        {
            target = default;
            return false;
        }

        var columnOffset =
            address.ColumnIndex - filter.Range.Left;
        var isFiltered = filter.Columns.Any(column =>
            column.ColumnOffset == columnOffset);
        target = new SpreadsheetWorksheetFilterTarget(
            filter.Range,
            columnOffset,
            address.ColumnIndex,
            new CellAddress(
                filter.Range.Top,
                address.ColumnIndex),
            isFiltered);
        return true;
    }
}
