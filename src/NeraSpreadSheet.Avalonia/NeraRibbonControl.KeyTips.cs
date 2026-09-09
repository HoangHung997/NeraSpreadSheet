using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraRibbonControl
{
    private IInputElement? _keyTipOrigin;
    private string? _keyTipOriginId;
    public RibbonKeyTipScope KeyTipScope => _runtime.KeyTips.Scope;

    public void EnterKeyTipMode()
    {
        VerifyUsable();
        _keyTipOriginId = CaptureFocusId();
        _keyTipOrigin = _keyTipOriginId is null ? TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() : null;
        _runtime.KeyTips.Enter(); _backstageOpen = false; Rebuild();
    }
    public void EscapeKeyTipMode()
    {
        VerifyUsable();
        var result = _runtime.KeyTips.Escape();
        _backstageOpen = KeyTipScope == RibbonKeyTipScope.Backstage;
        Rebuild();
        if (result.Action == RibbonKeyTipAction.Exit) RestoreKeyTipOrigin();
    }
    public ValueTask<bool> ProcessKeyTipAsync(string tip) => ApplyKeyTipAsync(_runtime.KeyTips.Process(tip));
    public ValueTask<bool> ProcessKeyTipCharacterAsync(char character) => ApplyKeyTipAsync(_runtime.KeyTips.ProcessCharacter(character));
    private async ValueTask<bool> ApplyKeyTipAsync(RibbonKeyTipResult result)
    {
        VerifyUsable();
        if (result.TabId is { } tab) { SelectTab(tab); if (IsMinimized) ShowMinimizedTab(); return true; }
        if (result.CommandId is { } command)
        {
            try { return await ActivateCommandAsync(command); }
            finally { if (!_disposed) { _backstageOpen = false; Rebuild(); RestoreKeyTipOrigin(); } }
        }
        if (result.Action != RibbonKeyTipAction.ScopeChanged) return false;
        _backstageOpen = KeyTipScope == RibbonKeyTipScope.Backstage; Rebuild(); return true;
    }
    private void RestoreKeyTipOrigin()
    {
        RestoreFocusId(_keyTipOriginId); _keyTipOrigin?.Focus(); _keyTipOrigin = null; _keyTipOriginId = null;
    }
    private sealed class RibbonInputBinding : IDisposable
    {
        private readonly NeraRibbonControl _ribbon;
        private readonly InputElement _owner;
        private readonly NeraAvaloniaShortcutBinding _shortcuts;
        private bool _disposed;
        public RibbonInputBinding(NeraRibbonControl ribbon, InputElement owner)
        {
            _ribbon = ribbon; _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            _shortcuts = new NeraAvaloniaShortcutBinding(owner, ribbon._runtime.TryResolveShortcut, ribbon.ActivateCommandAsync);
        }
        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_disposed || _ribbon._disposed || e.Handled) return;
            if (e.Key is Key.LeftAlt or Key.RightAlt && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
            {
                e.Handled = true;
                if (_ribbon.KeyTipScope == RibbonKeyTipScope.Inactive) _ribbon.EnterKeyTipMode(); else _ribbon.EscapeKeyTipMode();
                return;
            }
            if (_ribbon.KeyTipScope == RibbonKeyTipScope.Inactive) return;
            if (e.Key == Key.Escape) { e.Handled = true; _ribbon.EscapeKeyTipMode(); return; }
            char character;
            if (e.Key is >= Key.A and <= Key.Z) character = (char)('A' + (int)e.Key - (int)Key.A);
            else if (e.Key is >= Key.D0 and <= Key.D9) character = (char)('0' + (int)e.Key - (int)Key.D0);
            else return;
            e.Handled = true;
            await _ribbon.ProcessKeyTipCharacterAsync(character);
        }
        public void Dispose()
        {
            _owner.VerifyAccess(); if (_disposed) return; _disposed = true;
            _owner.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown); _shortcuts.Dispose();
        }
    }
}
