using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.WinForms;

internal delegate bool ShortcutResolver(string shortcut, out CommandId commandId);

internal sealed class NeraWinFormsShortcutBinding : IDisposable
{
    private static readonly KeysConverter KeysConverter = new();
    private readonly Control _owner;
    private readonly ShortcutResolver _resolver;
    private readonly Func<CommandId, ValueTask> _activate;
    private readonly Form? _form;
    private readonly bool _originalKeyPreview;
    private bool _disposed;

    public NeraWinFormsShortcutBinding(
        Control owner,
        ShortcutResolver resolver,
        Func<CommandId, ValueTask> activate)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        _form = owner as Form;
        if (_form is not null)
        {
            _originalKeyPreview = _form.KeyPreview;
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
        if (_form is not null && !_form.IsDisposed)
        {
            _form.KeyPreview = _originalKeyPreview;
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
        var text = KeysConverter.ConvertToString(
            context: null,
            CultureInfo.InvariantCulture,
            e.KeyData);
        if (string.IsNullOrWhiteSpace(text) || !_resolver(text, out var commandId))
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        await _activate(commandId);
    }
}
