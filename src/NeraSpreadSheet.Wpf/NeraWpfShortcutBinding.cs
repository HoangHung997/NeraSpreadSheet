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
    private readonly Func<CommandId, ValueTask<bool>> _activate;
    private readonly Action _enterKeyTips;
    private readonly Func<bool> _areKeyTipsActive;
    private readonly Func<char, ValueTask<bool>> _processKeyTipCharacter;
    private readonly Action _escapeKeyTips;
    private readonly bool _supportsKeyTips = true;
    private bool _disposed;

    public NeraWpfShortcutBinding(
        UIElement owner,
        ShortcutResolver resolver,
        Func<CommandId, ValueTask> activate)
        : this(
            owner,
            resolver,
            WrapActivation(activate),
            static () => { },
            static () => false,
            static _ => ValueTask.FromResult(false),
            static () => { })
    {
        _supportsKeyTips = false;
    }

    private static Func<CommandId, ValueTask<bool>> WrapActivation(
        Func<CommandId, ValueTask> activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        return async commandId =>
        {
            await activate(commandId);
            return true;
        };
    }

    public NeraWpfShortcutBinding(
        UIElement owner,
        ShortcutResolver resolver,
        Func<CommandId, ValueTask<bool>> activate,
        Action enterKeyTips,
        Func<bool> areKeyTipsActive,
        Func<char, ValueTask<bool>> processKeyTipCharacter,
        Action escapeKeyTips)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        _enterKeyTips = enterKeyTips ?? throw new ArgumentNullException(nameof(enterKeyTips));
        _areKeyTipsActive = areKeyTipsActive ?? throw new ArgumentNullException(nameof(areKeyTipsActive));
        _processKeyTipCharacter = processKeyTipCharacter ??
            throw new ArgumentNullException(nameof(processKeyTipCharacter));
        _escapeKeyTips = escapeKeyTips ?? throw new ArgumentNullException(nameof(escapeKeyTips));
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
        if (_disposed || e.Handled) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_supportsKeyTips && key is Key.LeftAlt or Key.RightAlt &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) == ModifierKeys.None)
        {
            _enterKeyTips();
            e.Handled = true;
            return;
        }
        if (_areKeyTipsActive() && key == Key.Escape)
        {
            _escapeKeyTips();
            e.Handled = true;
            return;
        }
        if (_areKeyTipsActive() && TryGetKeyTipCharacter(key, out var character))
        {
            e.Handled = true;
            await _processKeyTipCharacter(character);
            return;
        }
        if (key is Key.None or
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

    private static bool TryGetKeyTipCharacter(Key key, out char character)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            character = (char)('A' + ((int)key - (int)Key.A));
            return true;
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            character = (char)('0' + ((int)key - (int)Key.D0));
            return true;
        }
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            character = (char)('0' + ((int)key - (int)Key.NumPad0));
            return true;
        }
        character = default;
        return false;
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
