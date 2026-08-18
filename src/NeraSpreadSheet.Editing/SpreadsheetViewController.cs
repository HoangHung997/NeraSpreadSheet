using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetViewChangedEventArgs : EventArgs
{
    public SpreadsheetViewChangedEventArgs(
        Worksheet worksheet,
        int frozenRows,
        int frozenColumns,
        long version)
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

public enum SpreadsheetSplitViewChangeKind
{
    State,
    Topology,
    ActivePane,
    PaneScroll,
    ActiveWorksheet,
    History,
}

public sealed class SpreadsheetSplitViewChangedEventArgs : EventArgs
{
    public SpreadsheetSplitViewChangedEventArgs(
        Worksheet worksheet,
        SpreadsheetSplitViewState state,
        SpreadsheetSplitViewChangeKind changeKind,
        object? source,
        long version)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        State = state;
        ChangeKind = changeKind;
        Source = source;
        Version = version;
    }

    public Worksheet Worksheet { get; }

    public SpreadsheetSplitViewState State { get; }

    public SpreadsheetSplitViewChangeKind ChangeKind { get; }

    public object? Source { get; }

    public long Version { get; }
}

public sealed partial class SpreadsheetViewController
{
    private readonly SpreadsheetSession _session;
    private readonly Dictionary<Worksheet, FreezeState> _freezeStates = [];
    private readonly Dictionary<Worksheet, SpreadsheetSplitViewState> _splitStates = [];

    public SpreadsheetViewController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public int FrozenRows => GetFreezeState(_session.ActiveWorksheet).Rows;

    public int FrozenColumns => GetFreezeState(_session.ActiveWorksheet).Columns;

    public SpreadsheetSplitViewState SplitState => GetSplitState(_session.ActiveWorksheet);

    public long Version { get; private set; }

    public bool HasFrozenPanes => FrozenRows > 0 || FrozenColumns > 0;

    public bool HasSplitPanes => SplitState.HasSplitPanes;

    public event EventHandler<SpreadsheetViewChangedEventArgs>? Changed;

    public event EventHandler<SpreadsheetSplitViewChangedEventArgs>? SplitChanged;

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
        var current = GetFreezeState(worksheet);
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
        PublishFreeze(worksheet, next);
        return true;
    }

    public bool Unfreeze() => SetFrozenPanes(0, 0);

    public SpreadsheetSplitViewState GetSplitState(Worksheet worksheet)
    {
        ValidateWorksheet(worksheet);
        return _splitStates.GetValueOrDefault(worksheet);
    }

    public bool TryGetSplitState(
        Worksheet worksheet,
        out SpreadsheetSplitViewState state)
    {
        ValidateWorksheet(worksheet);
        return _splitStates.TryGetValue(worksheet, out state);
    }

    public bool SetSplitState(
        SpreadsheetSplitViewState state,
        SpreadsheetSplitViewChangeKind changeKind = SpreadsheetSplitViewChangeKind.State,
        object? source = null) =>
        SetSplitState(_session.ActiveWorksheet, state, changeKind, source);

    public bool SetSplitState(
        Worksheet worksheet,
        SpreadsheetSplitViewState state,
        SpreadsheetSplitViewChangeKind changeKind = SpreadsheetSplitViewChangeKind.State,
        object? source = null)
    {
        ValidateWorksheet(worksheet);
        ValidateChangeKind(changeKind);
        var current = GetSplitState(worksheet);
        if (current == state)
        {
            return false;
        }

        if (state == default)
        {
            _splitStates.Remove(worksheet);
        }
        else
        {
            _splitStates[worksheet] = state;
        }
        PublishSplit(worksheet, state, changeKind, source);
        return true;
    }

    public bool SetSplitTopology(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY,
        object? source = null) =>
        SetSplitState(
            SplitState.WithTopology(mode, splitX, splitY),
            SpreadsheetSplitViewChangeKind.Topology,
            source);

    public bool SetSplitActivePane(
        SpreadsheetSplitViewPane pane,
        object? source = null) =>
        SetSplitState(
            SplitState.WithActivePane(pane),
            SpreadsheetSplitViewChangeKind.ActivePane,
            source);

    public bool SetSplitPaneScroll(
        SpreadsheetSplitViewPane pane,
        double offsetX,
        double offsetY,
        object? source = null) =>
        SetSplitState(
            SplitState.WithPaneScroll(pane, offsetX, offsetY),
            SpreadsheetSplitViewChangeKind.PaneScroll,
            source);

    public bool ClearSplitPanes(object? source = null) =>
        SetSplitTopology(SpreadsheetSplitViewMode.None, null, null, source);

    internal void NotifyActiveWorksheetChanged()
    {
        var worksheet = _session.ActiveWorksheet;
        PublishFreeze(worksheet, GetFreezeState(worksheet));
        PublishSplit(
            worksheet,
            GetSplitState(worksheet),
            SpreadsheetSplitViewChangeKind.ActiveWorksheet,
            source: null);
    }

    private FreezeState GetFreezeState(Worksheet worksheet) =>
        _freezeStates.GetValueOrDefault(worksheet);

    private void PublishFreeze(Worksheet worksheet, FreezeState state)
    {
        Version++;
        Changed?.Invoke(
            this,
            new SpreadsheetViewChangedEventArgs(
                worksheet,
                state.Rows,
                state.Columns,
                Version));
    }

    private void PublishSplit(
        Worksheet worksheet,
        SpreadsheetSplitViewState state,
        SpreadsheetSplitViewChangeKind changeKind,
        object? source)
    {
        Version++;
        SplitChanged?.Invoke(
            this,
            new SpreadsheetSplitViewChangedEventArgs(
                worksheet,
                state,
                changeKind,
                source,
                Version));
    }

    private void ValidateWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!_session.Workbook.Worksheets.Contains(worksheet))
        {
            throw new ArgumentException(
                "Worksheet must belong to the session workbook.",
                nameof(worksheet));
        }
    }

    private static void ValidateChangeKind(SpreadsheetSplitViewChangeKind changeKind)
    {
        if (!Enum.IsDefined(changeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(changeKind));
        }
    }

    private static void ValidateMergedBoundaries(
        Worksheet worksheet,
        int frozenRows,
        int frozenColumns)
    {
        foreach (var range in worksheet.MergedCells.Ranges)
        {
            if (frozenRows > 0 && range.Top < frozenRows && range.Bottom >= frozenRows)
            {
                throw new InvalidOperationException(
                    "The horizontal freeze boundary cannot split a merged range.");
            }
            if (frozenColumns > 0 && range.Left < frozenColumns && range.Right >= frozenColumns)
            {
                throw new InvalidOperationException(
                    "The vertical freeze boundary cannot split a merged range.");
            }
        }
    }

    private readonly record struct FreezeState(int Rows, int Columns);
}

public static class SpreadsheetViewCommandIds
{
    public static CommandId FreezePanes { get; } = new("View.FreezePanes");

    public static CommandId UnfreezePanes { get; } = new("View.UnfreezePanes");

    public static CommandId UndoSplitViewChange { get; } =
        new("View.Split.Undo");

    public static CommandId RedoSplitViewChange { get; } =
        new("View.Split.Redo");
}

public static class SpreadsheetViewCommandCatalog
{
    public static void Register(
        CommandRegistry registry,
        SpreadsheetViewController view)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(view);
        registry.Register(
            new CommandDescriptor(
                SpreadsheetViewCommandIds.FreezePanes,
                "Freeze panes"),
            new ViewCommandHandler(
                () => new CommandState(true, IsChecked: view.HasFrozenPanes),
                view.FreezeAtActiveCell));
        registry.Register(
            new CommandDescriptor(
                SpreadsheetViewCommandIds.UnfreezePanes,
                "Unfreeze panes"),
            new ViewCommandHandler(
                () => new CommandState(view.HasFrozenPanes),
                view.Unfreeze));
        registry.Register(
            new CommandDescriptor(
                SpreadsheetViewCommandIds.UndoSplitViewChange,
                "Undo split view change"),
            new ViewCommandHandler(
                () => new CommandState(view.CanUndoSplitViewChange),
                view.UndoSplitViewChange));
        registry.Register(
            new CommandDescriptor(
                SpreadsheetViewCommandIds.RedoSplitViewChange,
                "Redo split view change"),
            new ViewCommandHandler(
                () => new CommandState(view.CanRedoSplitViewChange),
                view.RedoSplitViewChange));
    }

    private sealed class ViewCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _state;
        private readonly Func<bool> _execute;

        public ViewCommandHandler(
            Func<CommandState> state,
            Func<bool> execute)
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
