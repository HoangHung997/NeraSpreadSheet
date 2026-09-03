using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Maui;

public delegate bool NeraMauiShortcutResolver(
    string shortcut,
    out CommandId commandId);

public sealed class NeraMauiShortcutEventArgs : EventArgs
{
    public NeraMauiShortcutEventArgs(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            throw new ArgumentException(
                "A shortcut is required.",
                nameof(shortcut));
        }

        Shortcut = shortcut;
    }

    public string Shortcut { get; }

    public bool Handled { get; set; }
}

public interface INeraMauiShortcutSource
{
    event EventHandler<NeraMauiShortcutEventArgs>? ShortcutPressed;
}

public sealed class NeraMauiShortcutBinding : IDisposable
{
    private readonly INeraMauiShortcutSource _source;
    private readonly NeraMauiShortcutResolver _resolver;
    private readonly Func<CommandId, ValueTask<bool>> _activate;
    private bool _disposed;

    public NeraMauiShortcutBinding(
        INeraMauiShortcutSource source,
        NeraMauiShortcutResolver resolver,
        Func<CommandId, ValueTask<bool>> activate)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        _source.ShortcutPressed += OnShortcutPressed;
    }

    public async ValueTask<bool> TryProcessShortcutAsync(string shortcut)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_resolver(shortcut, out var commandId))
        {
            return false;
        }

        await _activate(commandId).ConfigureAwait(false);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.ShortcutPressed -= OnShortcutPressed;
        GC.SuppressFinalize(this);
    }

    private async void OnShortcutPressed(
        object? sender,
        NeraMauiShortcutEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (!_resolver(e.Shortcut, out var commandId))
        {
            return;
        }

        e.Handled = true;
        await _activate(commandId).ConfigureAwait(false);
    }
}
