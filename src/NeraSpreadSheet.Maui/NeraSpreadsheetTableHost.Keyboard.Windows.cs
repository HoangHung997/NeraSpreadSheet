#if WINDOWS
using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using NeraSpreadSheet.Editing;
using Windows.System;
using Windows.UI.Core;
using WinUiTextBox = Microsoft.UI.Xaml.Controls.TextBox;
using WinUiVisibility = Microsoft.UI.Xaml.Visibility;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetTableHost
{
    private FrameworkElement? _platformKeyboardRoot;
    private bool _platformSearchFocusPending;

    partial void AttachPlatformKeyboard()
    {
        if (Handler?.PlatformView is not FrameworkElement root ||
            ReferenceEquals(_platformKeyboardRoot, root))
        {
            return;
        }

        DetachPlatformKeyboard();
        _platformKeyboardRoot = root;
        root.PreviewKeyDown += OnPlatformPreviewKeyDown;
        root.LayoutUpdated += OnPlatformLayoutUpdated;
        _sheetOverlay.PropertyChanged += OnFilterOverlayPropertyChanged;
        _search.Focused += OnSearchEntryFocused;
        _search.HandlerChanged += OnSearchHandlerChanged;
        _platformSearchFocusPending = IsFilterSheetOpen;
        TryCompletePlatformSearchFocus();
    }

    partial void DetachPlatformKeyboard()
    {
        if (_platformKeyboardRoot is not null)
        {
            _platformKeyboardRoot.PreviewKeyDown -=
                OnPlatformPreviewKeyDown;
            _platformKeyboardRoot.LayoutUpdated -=
                OnPlatformLayoutUpdated;
        }

        _sheetOverlay.PropertyChanged -= OnFilterOverlayPropertyChanged;
        _search.Focused -= OnSearchEntryFocused;
        _search.HandlerChanged -= OnSearchHandlerChanged;
        _platformSearchFocusPending = false;
        _platformKeyboardRoot = null;
    }

    private void OnFilterOverlayPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (!string.Equals(
                e.PropertyName,
                nameof(VisualElement.IsVisible),
                StringComparison.Ordinal))
        {
            return;
        }

        _platformSearchFocusPending = IsFilterSheetOpen;
        TryCompletePlatformSearchFocus();
    }

    private void OnPlatformLayoutUpdated(object? sender, object e)
    {
        if (_platformSearchFocusPending)
        {
            TryCompletePlatformSearchFocus();
        }
    }

    private void OnSearchHandlerChanged(object? sender, EventArgs e)
    {
        if (_platformSearchFocusPending)
        {
            TryCompletePlatformSearchFocus();
        }
    }

    private void OnSearchEntryFocused(
        object? sender,
        Microsoft.Maui.Controls.FocusEventArgs e)
    {
        _platformSearchFocusPending = false;
        SelectSearchText();
    }

    private void TryCompletePlatformSearchFocus()
    {
        if (!_platformSearchFocusPending ||
            _disposed ||
            !IsFilterSheetOpen)
        {
            return;
        }

        if (_search.IsFocused)
        {
            _platformSearchFocusPending = false;
            SelectSearchText();
            return;
        }

        if (IsValueListFocused())
        {
            _platformSearchFocusPending = false;
            return;
        }

        if (_search.Handler?.PlatformView is not WinUiTextBox textBox ||
            !textBox.IsLoaded ||
            textBox.Visibility != WinUiVisibility.Visible)
        {
            return;
        }

        if (textBox.Focus(FocusState.Programmatic))
        {
            _platformSearchFocusPending = false;
            SelectSearchText();
        }
    }

    private void SelectSearchText()
    {
        _search.CursorPosition = 0;
        _search.SelectionLength = _search.Text?.Length ?? 0;
    }

    private void OnPlatformPreviewKeyDown(
        object sender,
        KeyRoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var altPressed = IsVirtualKeyPressed(VirtualKey.Menu);
        var controlPressed = IsVirtualKeyPressed(VirtualKey.Control);
        var shiftPressed = IsVirtualKeyPressed(VirtualKey.Shift);

        if (!IsFilterSheetOpen)
        {
            if (altPressed &&
                e.Key == VirtualKey.Down &&
                TryOpenForActiveCell())
            {
                e.Handled = true;
            }
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            CloseFilterSheet();
            e.Handled = true;
            return;
        }

        var searchFocused = IsSearchFocused();
        var valueFocused = IsValueListFocused();
        if (controlPressed &&
            e.Key == VirtualKey.A &&
            !searchFocused)
        {
            HandleFilterNavigation(
                shiftPressed
                    ? SpreadsheetTableFilterNavigationCommand
                        .ClearVisibleSelection
                    : SpreadsheetTableFilterNavigationCommand
                        .SelectAllVisible);
            e.Handled = true;
            return;
        }

        SpreadsheetTableFilterNavigationCommand command;
        switch (e.Key)
        {
            case VirtualKey.Down when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MoveFirst;
                break;
            case VirtualKey.Up when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MoveLast;
                break;
            case VirtualKey.Down when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MoveNext;
                break;
            case VirtualKey.Up when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MovePrevious;
                break;
            case VirtualKey.Home when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MoveFirst;
                break;
            case VirtualKey.End when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .MoveLast;
                break;
            case VirtualKey.PageUp when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .PagePrevious;
                break;
            case VirtualKey.PageDown when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand
                    .PageNext;
                break;
            case VirtualKey.Space when valueFocused:
            case VirtualKey.Enter when valueFocused:
                HandleFilterNavigation(
                    SpreadsheetTableFilterNavigationCommand
                        .ToggleCurrent);
                e.Handled = true;
                return;
            case VirtualKey.Enter when searchFocused:
                e.Handled = ApplyCurrentFilterAndClose();
                return;
            default:
                return;
        }

        HandleFilterNavigation(command);
        e.Handled = true;
    }

    private static bool IsVirtualKeyPressed(VirtualKey key)
    {
        var state = InputKeyboardSource
            .GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) ==
               CoreVirtualKeyStates.Down;
    }
}
#endif
