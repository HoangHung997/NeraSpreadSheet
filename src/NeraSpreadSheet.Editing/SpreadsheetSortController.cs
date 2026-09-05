using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetSortController
{
    public const long DefaultMaximumMaterializedCells = 1_000_000;
    private readonly SpreadsheetSession _session;
    private readonly long _maximumMaterializedCells;

    public SpreadsheetSortController(SpreadsheetSession session, long maximumMaterializedCells = DefaultMaximumMaterializedCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedCells);
        _maximumMaterializedCells = maximumMaterializedCells;
    }

    public bool CanSortPrimarySelection
    {
        get
        {
            if (_session.Selection.Ranges.Count != 1)
            {
                return false;
            }
            var range = _session.Selection.Ranges[0];
            return range.RowCount > 1 &&
                !_session.ActiveWorksheet.MergedCells.Intersects(range) &&
                !IntersectsFormulaSpill(range) &&
                (long)range.RowCount * range.ColumnCount <= _maximumMaterializedCells;
        }
    }

    public bool SortPrimarySelection(bool ascending, bool hasHeader = false)
    {
        if (!CanSortPrimarySelection)
        {
            return false;
        }
        var range = _session.Selection.Ranges[0];
        var activeColumn = _session.Selection.ActiveCell.ColumnIndex;
        var keyOffset = activeColumn >= range.Left && activeColumn <= range.Right
            ? activeColumn - range.Left
            : 0;
        Sort(range, keyOffset, ascending, hasHeader);
        return true;
    }

    public void Sort(CellRange range, int keyColumnOffset, bool ascending, bool hasHeader = false)
    {
        if (keyColumnOffset < 0 || keyColumnOffset >= range.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(keyColumnOffset));
        }
        RejectFormulaSpillIntersection(range);
        if (range.RowCount <= (hasHeader ? 2 : 1))
        {
            return;
        }
        var materialized = checked((long)range.RowCount * range.ColumnCount);
        if (materialized > _maximumMaterializedCells)
        {
            throw new InvalidOperationException("The selected range is too large to materialize for sorting.");
        }
        if (_session.ActiveWorksheet.MergedCells.Intersects(range))
        {
            throw new InvalidOperationException("Sorting a range that intersects merged cells is not supported.");
        }
        var firstDataRow = range.Top + (hasHeader ? 1 : 0);
        var rows = new List<SortRow>();
        for (var row = firstDataRow; row <= range.Bottom; row++)
        {
            var cells = new CellData[range.ColumnCount];
            for (var columnOffset = 0; columnOffset < range.ColumnCount; columnOffset++)
            {
                cells[columnOffset] = _session.ActiveWorksheet.GetCell(new CellAddress(row, range.Left + columnOffset));
            }
            rows.Add(new SortRow(row, cells));
        }

        rows.Sort((left, right) =>
        {
            var comparison = CompareCellValues(left.Cells[keyColumnOffset].Value, right.Cells[keyColumnOffset].Value);
            if (!ascending)
            {
                comparison = -comparison;
            }
            return comparison != 0 ? comparison : left.OriginalRow.CompareTo(right.OriginalRow);
        });

        var updates = new List<KeyValuePair<CellAddress, CellData>>(rows.Count * range.ColumnCount);
        for (var rowOffset = 0; rowOffset < rows.Count; rowOffset++)
        {
            var targetRow = firstDataRow + rowOffset;
            var source = rows[rowOffset].Cells;
            for (var columnOffset = 0; columnOffset < range.ColumnCount; columnOffset++)
            {
                updates.Add(new KeyValuePair<CellAddress, CellData>(
                    new CellAddress(targetRow, range.Left + columnOffset),
                    source[columnOffset]));
            }
        }

        _session.Execute(new SetCellsOperation(
            _session.ActiveWorksheet,
            updates,
            ascending ? "Sort ascending" : "Sort descending"));
        _session.Selection.Select(range);
    }

    /// <summary>Sorts the current Table data range and stores its ordered AutoFilter sort state atomically.</summary>
    public bool SortTable(Guid tableId, SpreadsheetFilterSortState sortState)
    {
        ArgumentNullException.ThrowIfNull(sortState);
        if (!_session.ActiveWorksheet.TryGetTable(tableId, out var table) || table is null)
        {
            throw new KeyNotFoundException($"Table '{tableId}' was not found.");
        }
        var dataRange = table.DataRange;
        if (dataRange is null)
        {
            return false;
        }
        ValidateSortState(sortState, table.Range.ColumnCount);
        var beforeFilter = table.AutoFilter?.Copy();
        var afterFilter = new TableAutoFilter(
            beforeFilter?.Columns ?? [],
            sortState);
        return ExecuteAutoFilterSort(
            dataRange.Value,
            sortState,
            table.Id,
            beforeFilter,
            afterFilter,
            worksheetFilterBefore: null,
            worksheetFilterAfter: null,
            "Sort Table AutoFilter");
    }

    /// <summary>Sorts the current direct worksheet AutoFilter data range and stores its sort state atomically.</summary>
    public bool SortWorksheet(SpreadsheetFilterSortState sortState)
    {
        ArgumentNullException.ThrowIfNull(sortState);
        var filter = _session.ActiveWorksheet.AutoFilter ??
            throw new InvalidOperationException("The active worksheet does not have a direct AutoFilter range.");
        if (filter.DataRange is not { } dataRange)
        {
            return false;
        }
        ValidateSortState(sortState, filter.Range.ColumnCount);
        return ExecuteAutoFilterSort(
            dataRange,
            sortState,
            tableId: null,
            tableFilterBefore: null,
            tableFilterAfter: null,
            filter,
            filter.WithSortState(sortState),
            "Sort worksheet AutoFilter");
    }

    /// <summary>Sorts the owner represented by a current filter target.</summary>
    public bool SortAutoFilter(
        SpreadsheetAutoFilterTarget target,
        SpreadsheetFilterSortState sortState) =>
        target.OwnerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table => SortTable(
                target.TableId ?? throw new ArgumentException(
                    "The Table target is missing its stable identity.", nameof(target)),
                sortState),
            SpreadsheetAutoFilterOwnerKind.Worksheet => SortWorksheet(sortState),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

    /// <summary>Reapplies the current sort state after resolving the owner's latest structural position.</summary>
    public bool ReapplyAutoFilter(SpreadsheetAutoFilterTarget target)
    {
        var state = target.OwnerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table => GetCurrentTableSortState(
                target.TableId ?? throw new ArgumentException(
                    "The Table target is missing its stable identity.", nameof(target))),
            SpreadsheetAutoFilterOwnerKind.Worksheet =>
                _session.ActiveWorksheet.AutoFilter?.SortState,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        return state is not null && SortAutoFilter(target, state);
    }

    /// <summary>Clears only AutoFilter sort metadata in one Undo/Redo operation.</summary>
    public bool ClearAutoFilterSort(SpreadsheetAutoFilterTarget target)
    {
        switch (target.OwnerKind)
        {
            case SpreadsheetAutoFilterOwnerKind.Table:
            {
                var tableId = target.TableId ?? throw new ArgumentException(
                    "The Table target is missing its stable identity.", nameof(target));
                if (!_session.ActiveWorksheet.TryGetTable(tableId, out var table) || table is null)
                {
                    throw new KeyNotFoundException($"Table '{tableId}' was not found.");
                }
                if (table.AutoFilter?.SortState is null)
                {
                    return false;
                }
                _session.Tables.SetSortState(tableId, null);
                return true;
            }
            case SpreadsheetAutoFilterOwnerKind.Worksheet:
                if (_session.ActiveWorksheet.AutoFilter?.SortState is null)
                {
                    return false;
                }
                _session.WorksheetFilter.SetSortState(null);
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }

    private SpreadsheetFilterSortState? GetCurrentTableSortState(Guid tableId)
    {
        if (!_session.ActiveWorksheet.TryGetTable(tableId, out var table) || table is null)
        {
            throw new KeyNotFoundException($"Table '{tableId}' was not found.");
        }
        return table.AutoFilter?.SortState;
    }

    private bool ExecuteAutoFilterSort(
        CellRange dataRange,
        SpreadsheetFilterSortState sortState,
        Guid? tableId,
        TableAutoFilter? tableFilterBefore,
        TableAutoFilter? tableFilterAfter,
        WorksheetAutoFilter? worksheetFilterBefore,
        WorksheetAutoFilter? worksheetFilterAfter,
        string description)
    {
        var materialized = checked((long)dataRange.RowCount * dataRange.ColumnCount);
        if (materialized > _maximumMaterializedCells)
        {
            throw new InvalidOperationException(
                "The AutoFilter data range is too large to materialize for sorting.");
        }
        if (_session.ActiveWorksheet.MergedCells.Intersects(dataRange))
        {
            throw new InvalidOperationException(
                "Sorting an AutoFilter range that intersects merged cells is not supported.");
        }
        RejectFormulaSpillIntersection(dataRange);

        var rows = MaterializeRows(dataRange);
        var sorted = rows.ToArray();
        Array.Sort(sorted, (left, right) => CompareRows(left, right, sortState));
        var orderChanged = !rows.Select(static row => row.OriginalRow)
            .SequenceEqual(sorted.Select(static row => row.OriginalRow));
        var metadataChanged = tableId is not null
            ? !Equals(tableFilterBefore?.SortState, tableFilterAfter?.SortState)
            : !Equals(worksheetFilterBefore, worksheetFilterAfter);
        if (!orderChanged && !metadataChanged)
        {
            return false;
        }

        var beforeCells = CreateCellUpdates(dataRange, rows);
        var afterCells = CreateCellUpdates(dataRange, sorted);
        _session.Execute(new AutoFilterSortOperation(
            _session.ActiveWorksheet,
            dataRange,
            beforeCells,
            afterCells,
            tableId,
            tableFilterBefore,
            tableFilterAfter,
            worksheetFilterBefore,
            worksheetFilterAfter,
            description));
        return true;
    }

    private bool IntersectsFormulaSpill(CellRange range) =>
        _session.ActiveWorksheet.GetFormulaSpills()
            .Any(spill => spill.Range.Intersects(range));

    private void RejectFormulaSpillIntersection(CellRange range)
    {
        if (IntersectsFormulaSpill(range))
        {
            throw new InvalidOperationException(
                "Sorting a range that intersects a dynamic-array spill is not supported.");
        }
    }

    private List<SortRow> MaterializeRows(CellRange range)
    {
        var rows = new List<SortRow>(range.RowCount);
        for (var row = range.Top; row <= range.Bottom; row++)
        {
            var cells = new CellData[range.ColumnCount];
            for (var columnOffset = 0; columnOffset < range.ColumnCount; columnOffset++)
            {
                cells[columnOffset] = _session.ActiveWorksheet.GetCell(
                    new CellAddress(row, range.Left + columnOffset));
            }
            rows.Add(new SortRow(row, cells));
        }
        return rows;
    }

    private static KeyValuePair<CellAddress, CellData>[] CreateCellUpdates(
        CellRange range,
        IReadOnlyList<SortRow> rows)
    {
        var updates = new KeyValuePair<CellAddress, CellData>[
            checked(rows.Count * range.ColumnCount)];
        var index = 0;
        for (var rowOffset = 0; rowOffset < rows.Count; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < range.ColumnCount; columnOffset++)
            {
                var target = new CellAddress(
                    range.Top + rowOffset,
                    range.Left + columnOffset);
                var source = new CellAddress(
                    rows[rowOffset].OriginalRow,
                    range.Left + columnOffset);
                var cell = rows[rowOffset].Cells[columnOffset];
                if (cell.Formula is { } formula && source != target)
                {
                    cell = new CellData(
                        cell.Value,
                        FormulaReferenceTranslator.Translate(formula, source, target),
                        cell.StyleId);
                }
                updates[index++] = new KeyValuePair<CellAddress, CellData>(
                    target,
                    cell);
            }
        }
        return updates;
    }

    private static int CompareRows(
        SortRow left,
        SortRow right,
        SpreadsheetFilterSortState state)
    {
        foreach (var condition in state.Conditions)
        {
            var leftValue = left.Cells[condition.ColumnOffset].Value;
            var rightValue = right.Cells[condition.ColumnOffset].Value;
            if (leftValue.Kind == CellValueKind.Blank ||
                rightValue.Kind == CellValueKind.Blank)
            {
                var blankComparison = leftValue.Kind == CellValueKind.Blank
                    ? rightValue.Kind == CellValueKind.Blank ? 0 : 1
                    : -1;
                if (blankComparison != 0)
                {
                    return blankComparison;
                }
            }
            var comparison = CompareCondition(
                leftValue,
                rightValue,
                condition,
                state.CaseSensitive);
            if (comparison != 0)
            {
                return condition.Descending ? -comparison : comparison;
            }
        }
        return left.OriginalRow.CompareTo(right.OriginalRow);
    }

    private static int CompareCondition(
        CellValue left,
        CellValue right,
        SpreadsheetFilterSortCondition condition,
        bool caseSensitive)
    {
        if (condition.SortBy != SpreadsheetFilterSortBy.Value)
        {
            throw new NotSupportedException(
                "Physical color and icon sorting requires evaluated visual-rule semantics and is not supported.");
        }
        if (condition.CustomList is { } customList)
        {
            var items = customList.Split(',', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);
            var comparer = caseSensitive
                ? StringComparer.CurrentCulture
                : StringComparer.CurrentCultureIgnoreCase;
            var leftIndex = Array.FindIndex(items, item => comparer.Equals(item, left.ToString()));
            var rightIndex = Array.FindIndex(items, item => comparer.Equals(item, right.ToString()));
            if (leftIndex >= 0 || rightIndex >= 0)
            {
                if (leftIndex < 0) return 1;
                if (rightIndex < 0) return -1;
                var customComparison = leftIndex.CompareTo(rightIndex);
                if (customComparison != 0) return customComparison;
            }
        }
        return CompareCellValues(left, right, caseSensitive);
    }

    private static void ValidateSortState(
        SpreadsheetFilterSortState state,
        int columnCount)
    {
        if (state.SortLeftToRight)
        {
            throw new NotSupportedException(
                "Left-to-right AutoFilter sorting is preservation-only and cannot be executed as a row sort.");
        }
        if (state.Conditions.Any(condition => condition.ColumnOffset >= columnCount))
        {
            throw new ArgumentException(
                "A sort key is outside the current AutoFilter range.", nameof(state));
        }
        if (state.Conditions.Any(condition => condition.SortBy != SpreadsheetFilterSortBy.Value))
        {
            throw new NotSupportedException(
                "Physical color and icon sorting requires evaluated visual-rule semantics and is not supported.");
        }
    }

    private static int CompareCellValues(
        CellValue left,
        CellValue right,
        bool caseSensitive = false)
    {
        if (left.Kind == CellValueKind.Blank)
        {
            return right.Kind == CellValueKind.Blank ? 0 : 1;
        }
        if (right.Kind == CellValueKind.Blank)
        {
            return -1;
        }
        if (left.Kind == right.Kind)
        {
            return left.Kind switch
            {
                CellValueKind.Number => ((double)left.RawValue!).CompareTo((double)right.RawValue!),
                CellValueKind.DateTime => ((DateTime)left.RawValue!).CompareTo((DateTime)right.RawValue!),
                CellValueKind.Boolean => ((bool)left.RawValue!).CompareTo((bool)right.RawValue!),
                CellValueKind.Text or CellValueKind.Error =>
                    (caseSensitive ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase)
                    .Compare((string?)left.RawValue, (string?)right.RawValue),
                _ => 0,
            };
        }

        var numericLeft = TryGetNumeric(left, out var leftNumber);
        var numericRight = TryGetNumeric(right, out var rightNumber);
        if (numericLeft && numericRight)
        {
            return leftNumber.CompareTo(rightNumber);
        }
        return (caseSensitive ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase)
            .Compare(left.ToString(), right.ToString());
    }

    private static bool TryGetNumeric(CellValue value, out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.DateTime:
                number = ((DateTime)value.RawValue!).ToOADate();
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }

    private sealed record SortRow(int OriginalRow, CellData[] Cells);

    private sealed class AutoFilterSortOperation :
        ISpreadsheetEditOperation,
        IIncrementalCalculationOperation
    {
        private readonly KeyValuePair<CellAddress, CellData>[] _beforeCells;
        private readonly KeyValuePair<CellAddress, CellData>[] _afterCells;
        private readonly Guid? _tableId;
        private readonly TableAutoFilter? _tableFilterBefore;
        private readonly TableAutoFilter? _tableFilterAfter;
        private readonly WorksheetAutoFilter? _worksheetFilterBefore;
        private readonly WorksheetAutoFilter? _worksheetFilterAfter;

        public AutoFilterSortOperation(
            Worksheet worksheet,
            CellRange affectedRange,
            KeyValuePair<CellAddress, CellData>[] beforeCells,
            KeyValuePair<CellAddress, CellData>[] afterCells,
            Guid? tableId,
            TableAutoFilter? tableFilterBefore,
            TableAutoFilter? tableFilterAfter,
            WorksheetAutoFilter? worksheetFilterBefore,
            WorksheetAutoFilter? worksheetFilterAfter,
            string description)
        {
            Worksheet = worksheet;
            AffectedRange = affectedRange;
            _beforeCells = beforeCells;
            _afterCells = afterCells;
            _tableId = tableId;
            _tableFilterBefore = tableFilterBefore?.Copy();
            _tableFilterAfter = tableFilterAfter?.Copy();
            _worksheetFilterBefore = worksheetFilterBefore?.Copy();
            _worksheetFilterAfter = worksheetFilterAfter?.Copy();
            Description = description;
        }

        public string Description { get; }
        public Worksheet Worksheet { get; }
        public CellRange AffectedRange { get; }

        public void Execute()
        {
            try
            {
                Apply(
                    _afterCells,
                    _tableFilterAfter,
                    _worksheetFilterAfter);
            }
            catch
            {
                Apply(
                    _beforeCells,
                    _tableFilterBefore,
                    _worksheetFilterBefore);
                throw;
            }
        }

        public void Undo() => Apply(
            _beforeCells,
            _tableFilterBefore,
            _worksheetFilterBefore);

        private void Apply(
            KeyValuePair<CellAddress, CellData>[] cells,
            TableAutoFilter? tableFilter,
            WorksheetAutoFilter? worksheetFilter)
        {
            Worksheet.SetCells(cells);
            if (_tableId is Guid tableId)
            {
                Worksheet.SetTableAutoFilter(tableId, tableFilter);
            }
            else
            {
                Worksheet.SetAutoFilter(worksheetFilter);
            }
        }
    }
}

public static class SpreadsheetSortCommandIds
{
    public static CommandId SortAscending { get; } = new("Data.SortAscending");
    public static CommandId SortDescending { get; } = new("Data.SortDescending");
}

public static class SpreadsheetSortCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetSortController sort)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(sort);
        registry.Register(
            new CommandDescriptor(SpreadsheetSortCommandIds.SortAscending, "Sort ascending", iconKey: "data.sort-ascending"),
            new SortCommandHandler(sort, ascending: true));
        registry.Register(
            new CommandDescriptor(SpreadsheetSortCommandIds.SortDescending, "Sort descending", iconKey: "data.sort-descending"),
            new SortCommandHandler(sort, ascending: false));
    }

    private sealed class SortCommandHandler : IStatefulCommandHandler
    {
        private readonly SpreadsheetSortController _sort;
        private readonly bool _ascending;

        public SortCommandHandler(SpreadsheetSortController sort, bool ascending)
        {
            _sort = sort;
            _ascending = ascending;
        }

        public bool CanExecute(CommandContext context) => _sort.CanSortPrimarySelection;
        public CommandState GetState(CommandContext context) => new(_sort.CanSortPrimarySelection);
        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _sort.SortPrimarySelection(_ascending);
            return ValueTask.CompletedTask;
        }
    }
}
