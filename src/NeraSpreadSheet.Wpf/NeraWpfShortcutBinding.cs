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

        var modifiers = Keyboard.Modifiers;
        if (IsUnmodifiedTextEntryKey(key, modifiers))
        {
            return;
        }

        KeyGesture gesture;
        try
        {
            gesture = new KeyGesture(key, modifiers);
        }
        catch (NotSupportedException)
        {
            // WPF rejects unmodified text-producing keys as KeyGestures. A
            // window-level binding must leave those keys to the active editor.
            return;
        }
        if (GestureConverter.ConvertToInvariantString(gesture) is not { Length: > 0 } text ||
            !_resolver(text, out var commandId))
        {
            return;
        }

        e.Handled = true;
        await _activate(commandId);
    }

    private static bool IsUnmodifiedTextEntryKey(
        Key key,
        ModifierKeys modifiers)
    {
        var commandModifiers = modifiers &
            (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows);
        if (commandModifiers != ModifierKeys.None)
        {
            return false;
        }

        return key is >= Key.A and <= Key.Z or
            >= Key.D0 and <= Key.D9 or
            >= Key.NumPad0 and <= Key.NumPad9 or
            Key.Space or
            Key.Oem1 or Key.Oem2 or Key.Oem3 or Key.Oem4 or Key.Oem5 or
            Key.Oem6 or Key.Oem7 or Key.Oem8 or Key.OemComma or
            Key.OemMinus or Key.OemPeriod or Key.OemPlus or
            Key.Add or Key.Subtract or Key.Multiply or Key.Divide or
            Key.Decimal;
    }
}
