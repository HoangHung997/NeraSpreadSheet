using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetCommandIds
{
    public static CommandId Undo { get; } = new("Edit.Undo");
    public static CommandId Redo { get; } = new("Edit.Redo");
    public static CommandId ClearContents { get; } = new("Cell.ClearContents");
    public static CommandId RecalculateWorkbook { get; } = new("Formula.RecalculateWorkbook");
}

public static class SpreadsheetCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);

        registry.Register(
            new CommandDescriptor(SpreadsheetCommandIds.Undo, "Undo", shortcut: "Ctrl+Z"),
            new SessionCommandHandler(
                () => new CommandState(session.History.CanUndo, DisplayText: session.History.NextUndoDescription),
                () => session.Undo()));

        registry.Register(
            new CommandDescriptor(SpreadsheetCommandIds.Redo, "Redo", shortcut: "Ctrl+Y"),
            new SessionCommandHandler(
                () => new CommandState(session.History.CanRedo, DisplayText: session.History.NextRedoDescription),
                () => session.Redo()));

        registry.Register(
            new CommandDescriptor(SpreadsheetCommandIds.ClearContents, "Clear contents", shortcut: "Delete"),
            new SessionCommandHandler(
                () => new CommandState(session.ActiveWorksheet.EnumerateUsedCells().Any(pair => session.Selection.Contains(pair.Key))),
                () => session.ClearSelection()));

        registry.Register(
            new CommandDescriptor(SpreadsheetCommandIds.RecalculateWorkbook, "Recalculate workbook", shortcut: "F9"),
            new SessionCommandHandler(
                () => CommandState.Enabled,
                () => { session.Recalculate(); return true; }));
    }

    private sealed class SessionCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _getState;
        private readonly Func<bool> _execute;

        public SessionCommandHandler(Func<CommandState> getState, Func<bool> execute)
        {
            _getState = getState ?? throw new ArgumentNullException(nameof(getState));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(CommandContext context) => _getState().IsEnabled;
        public CommandState GetState(CommandContext context) => _getState();

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
