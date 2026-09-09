using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Avalonia;

internal delegate bool AvaloniaShortcutResolver(string gesture, out CommandId commandId);

/// <summary>Shared routed-key arbitration for native Ribbon and Bar presenters.
/// The first resolver claims the event before activation can yield or re-enter.</summary>
internal sealed class NeraAvaloniaShortcutBinding : IDisposable
{
    private readonly InputElement _owner;
    private readonly AvaloniaShortcutResolver _resolve;
    private readonly Func<CommandId, ValueTask<bool>> _activate;
    private bool _disposed;

    public NeraAvaloniaShortcutBinding(InputElement owner, AvaloniaShortcutResolver resolve,
        Func<CommandId, ValueTask<bool>> activate)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        owner.VerifyAccess();
        owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_disposed || e.Handled || IsTextEditing(e.Source as Visual)) return;
        if (!TryFormatGesture(e.Key, e.KeyModifiers, out var gesture) || !_resolve(gesture, out var id)) return;
        e.Handled = true;
        // Presenters translate command failures into their public failure event.
        // An unobserved application error is never silently treated as a successful command.
        await _activate(id);
    }

    internal static bool TryFormatGesture(Key key, KeyModifiers modifiers, out string gesture)
    {
        gesture = string.Empty;
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return false;
        // Ctrl+Alt may represent AltGr text input; never steal it for workbook commands.
        if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt)) return false;
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if ((modifiers & (primary | KeyModifiers.Alt)) == 0 && key is not (>= Key.F1 and <= Key.F24)) return false;
        var tokens = new List<string>(4);
        if ((modifiers & primary) != 0) tokens.Add("Ctrl");
        if ((modifiers & KeyModifiers.Alt) != 0) tokens.Add("Alt");
        if ((modifiers & KeyModifiers.Shift) != 0) tokens.Add("Shift");
        tokens.Add(key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.Return => "Enter",
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture),
            _ => key.ToString(),
        });
        gesture = string.Join('+', tokens);
        return true;
    }

    private static bool IsTextEditing(Visual? source) => source is TextBox ||
        source?.GetVisualAncestors().Any(static ancestor => ancestor is TextBox) == true;

    public void Dispose()
    {
        _owner.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _owner.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }
}
