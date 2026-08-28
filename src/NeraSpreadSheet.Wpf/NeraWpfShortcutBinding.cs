using System.Windows;
using System.Windows.Input;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Wpf;

internal delegate bool ShortcutResolver(string shortcut, out CommandId commandId);

internal sealed class NeraWpfShortcutBinding : IDisposable
{
    private static readonly KeyGestureConverter GestureConverter = new();
    private readonly UIElement _owner;
    private readonly ShortcutResolver _resolver;
    private readonly Func<CommandId, ValueTask> _activate;
    private bool _disposed;

    public NeraWpfShortcutBinding(
        UIElement owner,
        ShortcutResolver resolver,
        Func<CommandId, ValueTask> activate)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        _owner.PreviewKeyDown += OnPreviewKeyDown;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _owner.PreviewKeyDown -= OnPreviewKeyDown;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.None or Key.LeftAlt or Key.RightAlt or
            Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin)
        {
            return;
        }

        var gesture = new KeyGesture(key, Keyboard.Modifiers);
        if (GestureConverter.ConvertToInvariantString(gesture) is not { Length: > 0 } text ||
            !_resolver(text, out var commandId))
        {
            return;
        }

        e.Handled = true;
        await _activate(commandId);
    }
}
