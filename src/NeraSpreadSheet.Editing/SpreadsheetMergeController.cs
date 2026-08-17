using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetMergeController
{
    public const long DefaultMaximumMergeCells = 1_000_000;
    private readonly SpreadsheetSession _session;
    private readonly long _maximumMergeCells;

    public SpreadsheetMergeController(SpreadsheetSession session, long maximumMergeCells = DefaultMaximumMergeCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMergeCells);
        _maximumMergeCells = maximumMergeCells;
    }

    public bool CanMergeSelection
    {
        get
        {
            if (_session.Selection.Ranges.Count != 1)
            {
                return false;
            }

            var range = _session.Selection.Ranges[0];
            return (range.RowCount > 1 || range.ColumnCount > 1) &&
                (long)range.RowCount * range.ColumnCount <= _maximumMergeCells &&
                !_session.ActiveWorksheet.MergedCells.Intersects(range) &&
                !CrossesFrozenBoundary(range);
        }
    }

    public bool CanUnmergeActiveCell =>
        _session.ActiveWorksheet.MergedCells.TryGetContaining(_session.Selection.ActiveCell, out _);

    public bool MergeSelection()
    {
        if (!CanMergeSelection)
        {
            return false;
        }

        var range = _session.Selection.Ranges[0];
        _session.Execute(new MergeCellsOperation(_session.ActiveWorksheet, range));
        _session.Selection.Select(range);
        _session.Selection.SetActiveCell(range.TopLeft, preserveRanges: true, preserveAnchor: true);
        return true;
    }

    public bool UnmergeActiveCell()
    {
        if (!_session.ActiveWorksheet.MergedCells.TryGetContaining(_session.Selection.ActiveCell, out var range))
        {
            return false;
        }

        _session.Execute(new UnmergeCellsOperation(_session.ActiveWorksheet, range));
        _session.Selection.Select(range);
        _session.Selection.SetActiveCell(range.TopLeft, preserveRanges: true, preserveAnchor: true);
        return true;
    }

    private bool CrossesFrozenBoundary(CellRange range)
    {
        var frozenRows = _session.View.FrozenRows;
        var frozenColumns = _session.View.FrozenColumns;
        return (frozenRows > 0 && range.Top < frozenRows && range.Bottom >= frozenRows) ||
            (frozenColumns > 0 && range.Left < frozenColumns && range.Right >= frozenColumns);
    }

    private sealed class MergeCellsOperation : ISpreadsheetEditOperation
    {
        private KeyValuePair<CellAddress, CellData>[]? _originalCells;

        public MergeCellsOperation(Worksheet worksheet, CellRange range)
        {
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            AffectedRange = range;
        }

        public string Description => "Merge cells";
        public Worksheet Worksheet { get; }
        public CellRange AffectedRange { get; }

        public void Execute()
        {
            _originalCells ??= Worksheet.EnumerateUsedCells()
                .Where(pair => AffectedRange.Contains(pair.Key))
                .ToArray();
            Worksheet.MergeCells(AffectedRange);
        }

        public void Undo()
        {
            if (_originalCells is null)
            {
                throw new InvalidOperationException("The operation has not been executed yet.");
            }

            Worksheet.UnmergeCells(AffectedRange);
            Worksheet.SetCells(_originalCells);
        }
    }

    private sealed class UnmergeCellsOperation : ISpreadsheetEditOperation
    {
        public UnmergeCellsOperation(Worksheet worksheet, CellRange range)
        {
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            AffectedRange = range;
        }

        public string Description => "Unmerge cells";
        public Worksheet Worksheet { get; }
        public CellRange AffectedRange { get; }

        public void Execute()
        {
            if (!Worksheet.UnmergeCells(AffectedRange))
            {
                throw new InvalidOperationException("The merged range no longer exists.");
            }
        }

        public void Undo() => Worksheet.MergeCells(AffectedRange, clearNonTopLeftCells: false);
    }
}

public static class SpreadsheetMergeCommandIds
{
    public static CommandId MergeCells { get; } = new("Cell.Merge");
    public static CommandId UnmergeCells { get; } = new("Cell.Unmerge");
    public static CommandId Merge => MergeCells;
    public static CommandId Unmerge => UnmergeCells;
}

public static class SpreadsheetMergeCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetMergeController merge)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(merge);

        registry.Register(
            new CommandDescriptor(SpreadsheetMergeCommandIds.MergeCells, "Merge cells"),
            new MergeCommandHandler(
                () => new CommandState(merge.CanMergeSelection),
                merge.MergeSelection));
        registry.Register(
            new CommandDescriptor(SpreadsheetMergeCommandIds.UnmergeCells, "Unmerge cells"),
            new MergeCommandHandler(
                () => new CommandState(merge.CanUnmergeActiveCell),
                merge.UnmergeActiveCell));
    }

    private sealed class MergeCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _state;
        private readonly Func<bool> _execute;

        public MergeCommandHandler(Func<CommandState> state, Func<bool> execute)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(CommandContext context) => _state().IsEnabled;
        public CommandState GetState(CommandContext context) => _state();

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
