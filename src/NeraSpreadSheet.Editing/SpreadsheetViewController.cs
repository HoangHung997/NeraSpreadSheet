using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetViewChangedEventArgs : EventArgs
{
    public SpreadsheetViewChangedEventArgs(Worksheet worksheet, int frozenRows, int frozenColumns, long version)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        FrozenRows = frozenRows;
        FrozenColumns = frozenColumns;
        Version = version;
    }

    public Worksheet Worksheet { get; }
    public int FrozenRows { get; }
    public int FrozenColumns { get; }
    public long Version { get; }
}

public sealed class SpreadsheetViewController
{
    private readonly SpreadsheetSession _session;
    private readonly Dictionary<Worksheet, FreezeState> _freezeStates = [];

    public SpreadsheetViewController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public int FrozenRows => GetState(_session.ActiveWorksheet).Rows;
    public int FrozenColumns => GetState(_session.ActiveWorksheet).Columns;
    public long Version { get; private set; }
    public bool HasFrozenPanes => FrozenRows > 0 || FrozenColumns > 0;

    public event EventHandler<SpreadsheetViewChangedEventArgs>? Changed;

    public bool FreezeAtActiveCell() => SetFrozenPanes(
        _session.Selection.ActiveCell.RowIndex,
        _session.Selection.ActiveCell.ColumnIndex);

    public bool FreezeTopRows(int count) => SetFrozenPanes(count, FrozenColumns);

    public bool FreezeLeftColumns(int count) => SetFrozenPanes(FrozenRows, count);

    public bool SetFrozenPanes(int frozenRows, int frozenColumns)
    {
        if (frozenRows < 0 || frozenRows >= SpreadsheetLimits.MaxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenRows));
        }
        if (frozenColumns < 0 || frozenColumns >= SpreadsheetLimits.MaxColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenColumns));
        }

        var worksheet = _session.ActiveWorksheet;
        ValidateMergedBoundaries(worksheet, frozenRows, frozenColumns);
        var current = GetState(worksheet);
        var next = new FreezeState(frozenRows, frozenColumns);
        if (current == next)
        {
            return false;
        }

        if (next == default)
        {
            _freezeStates.Remove(worksheet);
        }
        else
        {
            _freezeStates[worksheet] = next;
        }
        Publish(worksheet, next);
        return true;
    }

    public bool Unfreeze() => SetFrozenPanes(0, 0);

    internal void NotifyActiveWorksheetChanged()
    {
        var worksheet = _session.ActiveWorksheet;
        Publish(worksheet, GetState(worksheet));
    }

    private FreezeState GetState(Worksheet worksheet) => _freezeStates.GetValueOrDefault(worksheet);

    private void Publish(Worksheet worksheet, FreezeState state)
    {
        Version++;
        Changed?.Invoke(this, new SpreadsheetViewChangedEventArgs(worksheet, state.Rows, state.Columns, Version));
    }

    private static void ValidateMergedBoundaries(Worksheet worksheet, int frozenRows, int frozenColumns)
    {
        foreach (var range in worksheet.MergedCells.Ranges)
        {
            if (frozenRows > 0 && range.Top < frozenRows && range.Bottom >= frozenRows)
            {
                throw new InvalidOperationException("The horizontal freeze boundary cannot split a merged range.");
            }
            if (frozenColumns > 0 && range.Left < frozenColumns && range.Right >= frozenColumns)
            {
                throw new InvalidOperationException("The vertical freeze boundary cannot split a merged range.");
            }
        }
    }

    private readonly record struct FreezeState(int Rows, int Columns);
}

public static class SpreadsheetViewCommandIds
{
    public static CommandId FreezePanes { get; } = new("View.FreezePanes");
    public static CommandId UnfreezePanes { get; } = new("View.UnfreezePanes");
}

public static class SpreadsheetViewCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetViewController view)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(view);
        registry.Register(
            new CommandDescriptor(SpreadsheetViewCommandIds.FreezePanes, "Freeze panes"),
            new ViewCommandHandler(
                () => new CommandState(true, IsChecked: view.HasFrozenPanes),
                view.FreezeAtActiveCell));
        registry.Register(
            new CommandDescriptor(SpreadsheetViewCommandIds.UnfreezePanes, "Unfreeze panes"),
            new ViewCommandHandler(
                () => new CommandState(view.HasFrozenPanes),
                view.Unfreeze));
    }

    private sealed class ViewCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _state;
        private readonly Func<bool> _execute;

        public ViewCommandHandler(Func<CommandState> state, Func<bool> execute)
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
