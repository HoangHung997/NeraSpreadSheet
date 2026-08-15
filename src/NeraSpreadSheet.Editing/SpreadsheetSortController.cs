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

    private static int CompareCellValues(CellValue left, CellValue right)
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
                CellValueKind.Text or CellValueKind.Error => StringComparer.CurrentCultureIgnoreCase.Compare((string?)left.RawValue, (string?)right.RawValue),
                _ => 0,
            };
        }

        var numericLeft = TryGetNumeric(left, out var leftNumber);
        var numericRight = TryGetNumeric(right, out var rightNumber);
        if (numericLeft && numericRight)
        {
            return leftNumber.CompareTo(rightNumber);
        }
        return StringComparer.CurrentCultureIgnoreCase.Compare(left.ToString(), right.ToString());
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
            new CommandDescriptor(SpreadsheetSortCommandIds.SortAscending, "Sort ascending"),
            new SortCommandHandler(sort, ascending: true));
        registry.Register(
            new CommandDescriptor(SpreadsheetSortCommandIds.SortDescending, "Sort descending"),
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
