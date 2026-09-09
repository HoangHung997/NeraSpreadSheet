using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetClipboardCommandIds
{
    public static CommandId Copy { get; } = new("Edit.Copy");
    public static CommandId Cut { get; } = new("Edit.Cut");
    public static CommandId Paste { get; } = new("Edit.Paste");
    public static CommandId PasteValues { get; } = new("Edit.PasteValues");
    public static CommandId PasteFormulas { get; } = new("Edit.PasteFormulas");
    public static CommandId PasteFormats { get; } = new("Edit.PasteFormats");
    public static CommandId CancelCopyMode { get; } = new("Edit.CancelCopyMode");
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
                () => new CommandState(clipboard.CanCopy),
                () => { clipboard.CopyPrimarySelection(); return true; }));
        registry.Register(
            new CommandDescriptor(SpreadsheetClipboardCommandIds.Cut, "Cut", iconKey: "edit.cut", shortcut: "Ctrl+X"),
            new ClipboardCommandHandler(
                () => new CommandState(clipboard.CanCut),
                clipboard.CutPrimarySelection));
        RegisterPaste(registry, clipboard, SpreadsheetClipboardCommandIds.Paste, "Paste", SpreadsheetClipboardPasteMode.All, "Ctrl+V");
        RegisterPaste(registry, clipboard, SpreadsheetClipboardCommandIds.PasteValues, "Dán giá trị", SpreadsheetClipboardPasteMode.Values);
        RegisterPaste(registry, clipboard, SpreadsheetClipboardCommandIds.PasteFormulas, "Dán công thức", SpreadsheetClipboardPasteMode.Formulas);
        RegisterPaste(registry, clipboard, SpreadsheetClipboardCommandIds.PasteFormats, "Dán định dạng", SpreadsheetClipboardPasteMode.Formats);
        // No global Esc shortcut: hosts must give editor, IME and popup cancellation priority.
        registry.Register(
            new CommandDescriptor(SpreadsheetClipboardCommandIds.CancelCopyMode, "Hủy chế độ sao chép"),
            new ClipboardCommandHandler(
                () => new CommandState(clipboard.CanCancelCopyMode),
                clipboard.CancelCopyMode));
    }

    private static void RegisterPaste(CommandRegistry registry, SpreadsheetClipboardController clipboard,
        CommandId id, string name, SpreadsheetClipboardPasteMode mode, string? shortcut = null)
    {
        registry.Register(new CommandDescriptor(id, name, iconKey: "edit.paste", shortcut: shortcut),
            new ClipboardCommandHandler(() => new CommandState(clipboard.CanPasteSpecial(mode)),
                () => clipboard.PasteAtActiveCell(mode)));
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
