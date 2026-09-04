using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetClipboardCommandIds
{
    public static CommandId Copy { get; } = new("Edit.Copy");
    public static CommandId Cut { get; } = new("Edit.Cut");
    public static CommandId Paste { get; } = new("Edit.Paste");
}

public static class SpreadsheetClipboardCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetClipboardController clipboard)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(clipboard);

        registry.Register(
            new CommandDescriptor(SpreadsheetClipboardCommandIds.Copy, "Copy", iconKey: "edit.copy", shortcut: "Ctrl+C"),
            new ClipboardCommandHandler(
                () => CommandState.Enabled,
                () => { clipboard.CopyPrimarySelection(); return true; }));

        registry.Register(
            new CommandDescriptor(SpreadsheetClipboardCommandIds.Cut, "Cut", iconKey: "edit.cut", shortcut: "Ctrl+X"),
            new ClipboardCommandHandler(
                () => CommandState.Enabled,
                clipboard.CutPrimarySelection));

        registry.Register(
            new CommandDescriptor(SpreadsheetClipboardCommandIds.Paste, "Paste", iconKey: "edit.paste", shortcut: "Ctrl+V"),
            new ClipboardCommandHandler(
                () => new CommandState(clipboard.CanPaste),
                clipboard.PasteAtActiveCell));
    }

    private sealed class ClipboardCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _state;
        private readonly Func<bool> _execute;

        public ClipboardCommandHandler(Func<CommandState> state, Func<bool> execute)
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
