#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraMauiRibbonCustomizationView
{
    private FrameworkElement? _keyboardRoot;

    partial void InitializeKeyboard()
    {
        HandlerChanged += OnKeyboardHandlerChanged;
        OnKeyboardHandlerChanged(this, EventArgs.Empty);
    }

    private void OnKeyboardHandlerChanged(object? sender, EventArgs args)
    {
        if (_keyboardRoot is not null) _keyboardRoot.KeyDown -= OnShellKeyDown;
        _keyboardRoot = _disposed ? null : Handler?.PlatformView as FrameworkElement;
        if (_keyboardRoot is not null) _keyboardRoot.KeyDown += OnShellKeyDown;
    }

    private void OnShellKeyDown(object sender, KeyRoutedEventArgs args)
    {
        // Bubble after native controls so an open Picker consumes its own Escape.
        if (_disposed || args.Handled || args.Key != VirtualKey.Escape) return;
        if (new[] { _targets, _catalog, _destination, _qat }.Any(static picker =>
            picker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox { IsDropDownOpen: true })) return;
        args.Handled = true;
        Execute(CancelCustomization);
    }

    partial void DisposeKeyboard()
    {
        HandlerChanged -= OnKeyboardHandlerChanged;
        if (_keyboardRoot is not null) _keyboardRoot.KeyDown -= OnShellKeyDown;
        _keyboardRoot = null;
    }
}
#endif
