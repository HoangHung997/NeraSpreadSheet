using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.WinForms;

internal delegate bool ShortcutResolver(string shortcut, out CommandId commandId);

internal sealed class NeraWinFormsShortcutBinding : IDisposable
{
    private static readonly KeysConverter KeysConverter = new();
    private static readonly ConditionalWeakTable<Form, KeyPreviewLease> KeyPreviewLeases = new();
    private readonly Control _owner;
    private readonly ShortcutResolver _resolver;
    private readonly Func<CommandId, ValueTask<bool>> _activate;
    private readonly Action _enterKeyTips;
    private readonly Func<bool> _areKeyTipsActive;
    private readonly Func<char, ValueTask<bool>> _processKeyTipCharacter;
    private readonly Action _escapeKeyTips;
    private readonly bool _supportsKeyTips = true;
    private readonly Form? _form;
    private readonly KeyPreviewLease? _keyPreviewLease;
    private bool _disposed;

    public NeraWinFormsShortcutBinding(
        Control owner,
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

    public NeraWinFormsShortcutBinding(
        Control owner,
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
        _form = owner as Form;
        if (_form is not null)
        {
            _keyPreviewLease = KeyPreviewLeases.GetValue(_form, static form => new KeyPreviewLease(form.KeyPreview));
            _keyPreviewLease.Count++;
            _form.KeyPreview = true;
        }
        _owner.KeyDown += OnKeyDown;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _owner.KeyDown -= OnKeyDown;
        if (_form is not null && _keyPreviewLease is not null && --_keyPreviewLease.Count == 0)
        {
            if (!_form.IsDisposed) _form.KeyPreview = _keyPreviewLease.OriginalValue;
            KeyPreviewLeases.Remove(_form);
        }
    }

    public static bool TryConvertKeys(string? shortcut, out Keys keys)
    {
        keys = Keys.None;
        if (!CommandShortcut.TryParse(shortcut, out var normalized))
        {
            return false;
        }
        try
        {
            if (KeysConverter.ConvertFromInvariantString(
                    normalized.CanonicalText) is Keys converted &&
                converted != Keys.None)
            {
                keys = converted;
                return true;
            }
        }
        catch (NotSupportedException)
        {
            return false;
        }
        return false;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_disposed || e.Handled || e.SuppressKeyPress) return;
        if (_supportsKeyTips && e.KeyCode == Keys.Menu && !e.Control)
        {
            _enterKeyTips();
            MarkHandled(e);
            return;
        }
        if (_areKeyTipsActive() && e.KeyCode == Keys.Escape)
        {
            _escapeKeyTips();
            MarkHandled(e);
            return;
        }
        if (_areKeyTipsActive() && TryGetKeyTipCharacter(e.KeyCode, out var character))
        {
            MarkHandled(e);
            await _processKeyTipCharacter(character);
            return;
        }
        var text = KeysConverter.ConvertToString(
            context: null,
            CultureInfo.InvariantCulture,
            e.KeyData);
        if (string.IsNullOrWhiteSpace(text) || !_resolver(text, out var commandId))
        {
            return;
        }

        MarkHandled(e);
        await _activate(commandId);
    }

    private static bool TryGetKeyTipCharacter(Keys key, out char character)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            character = (char)('A' + ((int)key - (int)Keys.A));
            return true;
        }
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            character = (char)('0' + ((int)key - (int)Keys.D0));
            return true;
        }
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            character = (char)('0' + ((int)key - (int)Keys.NumPad0));
            return true;
        }
        character = default;
        return false;
    }

    private static void MarkHandled(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private sealed class KeyPreviewLease(bool originalValue)
    {
        internal bool OriginalValue { get; } = originalValue;
        internal int Count { get; set; }
    }
}
