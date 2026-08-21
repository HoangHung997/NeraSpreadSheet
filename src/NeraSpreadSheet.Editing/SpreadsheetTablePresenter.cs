using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetTableManagerColumnSnapshot(
    Guid Id,
    string Name,
    int WorksheetColumnIndex,
    bool HasCalculatedFormula,
    bool HasTotalsFormula,
    bool HasTotalsLabel,
    bool IsFiltered);

public sealed record SpreadsheetTableManagerItemSnapshot(
    Guid Id,
    string Name,
    CellRange Range,
    bool HasHeaders,
    bool HasTotalsRow,
    string? StyleName,
    bool HasActiveFilter,
    IReadOnlyList<SpreadsheetTableManagerColumnSnapshot> Columns);

public sealed record SpreadsheetTableManagerSnapshot(
    string WorksheetName,
    IReadOnlyList<SpreadsheetTableManagerItemSnapshot> Tables);

public sealed record SpreadsheetTableFilterValueItem(
    CellValue Value,
    string DisplayText,
    int Count,
    bool IsSelected);

public sealed record SpreadsheetTableFilterMenuSnapshot(
    Guid TableId,
    Guid ColumnId,
    string TableName,
    string ColumnName,
    string SearchText,
    int SourceRowCount,
    int ScannedRowCount,
    int DistinctValueCount,
    bool IsRowScanTruncated,
    bool IsDistinctValueTruncated,
    bool HasActiveFilter,
    bool HasCustomFilter,
    bool AreAllVisibleValuesSelected,
    bool AreNoVisibleValuesSelected,
    bool CanApplyValueSelection,
    IReadOnlyList<SpreadsheetTableFilterValueItem> Values)
{
    public bool IsTruncated =>
        IsRowScanTruncated || IsDistinctValueTruncated;
}

public sealed class SpreadsheetTableFilterMenu
{
    private readonly SpreadsheetTablePresenterController _owner;
    private readonly Dictionary<CellValue, int> _counts;
    private readonly HashSet<CellValue> _selected;
    private string _searchText = string.Empty;

    internal SpreadsheetTableFilterMenu(
        SpreadsheetTablePresenterController owner,
        SpreadsheetTable table,
        SpreadsheetTableColumn column,
        Dictionary<CellValue, int> counts,
        TableFilterColumn? currentFilter,
        int sourceRowCount,
        int scannedRowCount,
        bool isRowScanTruncated,
        bool isDistinctValueTruncated)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(column);
        _counts = counts ?? throw new ArgumentNullException(nameof(counts));
        TableId = table.Id;
        ColumnId = column.Id;
        TableName = table.Name;
        ColumnName = column.Name;
        SourceRowCount = sourceRowCount;
        ScannedRowCount = scannedRowCount;
        IsRowScanTruncated = isRowScanTruncated;
        IsDistinctValueTruncated = isDistinctValueTruncated;
        HasActiveFilter = currentFilter is not null;
        HasCustomFilter = currentFilter?.FirstCondition is not null;

        _selected = currentFilter is null || HasCustomFilter
            ? _counts.Keys.ToHashSet()
            : currentFilter.Values.ToHashSet();
        if (currentFilter?.IncludeBlank == true)
        {
            _selected.Add(CellValue.Blank);
        }
    }

    public Guid TableId { get; }

    public Guid ColumnId { get; }

    public string TableName { get; }

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

    public event EventHandler? Changed;

    public SpreadsheetTableFilterMenuSnapshot Capture()
    {
        var visible = GetVisibleValues();
        var selectedVisibleCount = visible.Count(item =>
            _selected.Contains(item.Value));
        return new SpreadsheetTableFilterMenuSnapshot(
            TableId,
            ColumnId,
            TableName,
            ColumnName,
            _searchText,
            SourceRowCount,
            ScannedRowCount,
            _counts.Count,
            IsRowScanTruncated,
            IsDistinctValueTruncated,
            HasActiveFilter,
            HasCustomFilter,
            visible.Count > 0 && selectedVisibleCount == visible.Count,
            selectedVisibleCount == 0,
            _selected.Count > 0,
            visible.Select(item => new SpreadsheetTableFilterValueItem(
                    item.Value,
                    item.DisplayText,
                    item.Count,
                    _selected.Contains(item.Value)))
                .ToArray());
    }

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
                "The value is not part of this filter menu.",
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
        foreach (var item in GetVisibleValues())
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
        foreach (var item in GetVisibleValues())
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
            TableId,
            ColumnId,
            firstCondition,
            secondCondition,
            combineWithAnd);

    public void ClearColumnFilter() =>
        _owner.ClearColumnFilter(TableId, ColumnId);

    public void ClearAllTableFilters() =>
        _owner.ClearAllTableFilters(TableId);

    internal CellValue[] GetSelectedValues() =>
        _selected
            .OrderBy(static value => value.Kind)
            .ThenBy(static value => FormatValue(value), StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal bool RepresentsEveryEnumeratedValue =>
        _selected.Count == _counts.Count &&
        _counts.Keys.All(_selected.Contains);

    private List<ValueCount> GetVisibleValues()
    {
        var result = _counts
            .Select(static pair => new ValueCount(
                pair.Key,
                FormatValue(pair.Key),
                pair.Value))
            .Where(item =>
                _searchText.Length == 0 ||
                item.DisplayText.Contains(
                    _searchText,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.Value.IsBlank ? 0 : 1)
            .ThenBy(static item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Value.Kind)
            .ToList();
        return result;
    }

    private static string FormatValue(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => "(Blank)",
            CellValueKind.DateTime => ((DateTime)value.RawValue!).ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            CellValueKind.Boolean => (bool)value.RawValue! ? "TRUE" : "FALSE",
            _ => value.ToString(),
        };

    private readonly record struct ValueCount(
        CellValue Value,
        string DisplayText,
        int Count);
}

public sealed class SpreadsheetTablePresenterController
{
    public const int DefaultMaximumRows = 100_000;
    public const int DefaultMaximumDistinctValues = 10_000;

    private readonly SpreadsheetSession _session;

    public SpreadsheetTablePresenterController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public SpreadsheetTableManagerSnapshot GetManagerSnapshot()
    {
        var worksheet = _session.ActiveWorksheet;
        var tables = worksheet.Tables
            .Select(table => new SpreadsheetTableManagerItemSnapshot(
                table.Id,
                table.Name,
                table.Range,
                table.HasHeaders,
                table.HasTotalsRow,
                table.StyleName,
                table.AutoFilter is { Columns.Count: > 0 },
                table.Columns.Select((column, index) =>
                    new SpreadsheetTableManagerColumnSnapshot(
                        column.Id,
                        column.Name,
                        table.Range.Left + index,
                        column.CalculatedColumnFormula is not null,
                        column.TotalsRowFormula is not null,
                        column.TotalsRowLabel is not null,
                        table.AutoFilter?.Columns.Any(filter =>
                            filter.ColumnId == column.Id) == true))
                    .ToArray()))
            .ToArray();
        return new SpreadsheetTableManagerSnapshot(
            worksheet.Name,
            tables);
    }

    public SpreadsheetTableFilterMenu OpenFilterMenu(
        Guid tableId,
        Guid columnId,
        int maximumRows = DefaultMaximumRows,
        int maximumDistinctValues = DefaultMaximumDistinctValues)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumDistinctValues);
        var table = GetTable(tableId);
        if (!table.TryGetColumn(columnId, out var column) ||
            column is null)
        {
            throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        }

        var counts = new Dictionary<CellValue, int>();
        var sourceRowCount = table.DataRange?.RowCount ?? 0;
        var scannedRowCount = 0;
        var distinctTruncated = false;
        if (table.DataRange is { } dataRange)
        {
            var snapshot = WorksheetSnapshot.Capture(
                _session.ActiveWorksheet);
            var worksheetColumn = table.Range.Left +
                                  table.GetColumnIndex(columnId);
            var rowLimit = Math.Min(
                dataRange.RowCount,
                maximumRows);
            for (var offset = 0; offset < rowLimit; offset++)
            {
                var value = snapshot.GetCell(new CellAddress(
                    dataRange.Top + offset,
                    worksheetColumn)).Value;
                scannedRowCount++;
                if (counts.TryGetValue(value, out var count))
                {
                    counts[value] = count + 1;
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

        var currentFilter = table.AutoFilter?.Columns
            .FirstOrDefault(candidate =>
                candidate.ColumnId == columnId)
            ?.Copy();
        return new SpreadsheetTableFilterMenu(
            this,
            table,
            column,
            counts,
            currentFilter,
            sourceRowCount,
            scannedRowCount,
            sourceRowCount > maximumRows,
            distinctTruncated);
    }

    public void ApplyValueSelection(
        SpreadsheetTableFilterMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        EnsureMenuBelongsToActiveTable(menu);
        var selected = menu.GetSelectedValues();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one value must be selected before applying a value filter.");
        }

        TableFilterColumn? replacement = null;
        if (menu.IsTruncated || !menu.RepresentsEveryEnumeratedValue)
        {
            var includeBlank = selected.Any(static value =>
                value.IsBlank);
            replacement = new TableFilterColumn(
                menu.ColumnId,
                selected,
                includeBlank);
        }

        ReplaceColumnFilter(
            menu.TableId,
            menu.ColumnId,
            replacement);
    }

    public void ApplyCustomFilter(
        Guid tableId,
        Guid columnId,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        var table = GetTable(tableId);
        if (!table.TryGetColumn(columnId, out _))
        {
            throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        }

        ReplaceColumnFilter(
            tableId,
            columnId,
            new TableFilterColumn(
                columnId,
                firstCondition: firstCondition,
                secondCondition: secondCondition,
                combineWithAnd: combineWithAnd));
    }

    public void ClearColumnFilter(Guid tableId, Guid columnId)
    {
        var table = GetTable(tableId);
        if (!table.TryGetColumn(columnId, out _))
        {
            throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        }
        ReplaceColumnFilter(tableId, columnId, replacement: null);
    }

    public void ClearAllTableFilters(Guid tableId)
    {
        GetTable(tableId);
        _session.Tables.ClearAutoFilter(tableId);
    }

    private void ReplaceColumnFilter(
        Guid tableId,
        Guid columnId,
        TableFilterColumn? replacement)
    {
        var table = GetTable(tableId);
        var columns = table.AutoFilter?.Columns
            .Where(candidate => candidate.ColumnId != columnId)
            .Select(static candidate => candidate.Copy())
            .ToList() ?? [];
        if (replacement is not null)
        {
            columns.Add(replacement.Copy());
        }
        _session.Tables.SetAutoFilter(
            tableId,
            columns.Count == 0
                ? null
                : new TableAutoFilter(columns));
    }

    private SpreadsheetTable GetTable(Guid tableId)
    {
        if (_session.ActiveWorksheet.TryGetTable(
                tableId,
                out var table) &&
            table is not null)
        {
            return table;
        }

        throw new KeyNotFoundException(
            $"Table '{tableId}' was not found on the active worksheet.");
    }

    private void EnsureMenuBelongsToActiveTable(
        SpreadsheetTableFilterMenu menu)
    {
        var table = GetTable(menu.TableId);
        if (!table.TryGetColumn(menu.ColumnId, out _))
        {
            throw new InvalidOperationException(
                "The filter menu no longer targets a column in the active table.");
        }
    }
}
