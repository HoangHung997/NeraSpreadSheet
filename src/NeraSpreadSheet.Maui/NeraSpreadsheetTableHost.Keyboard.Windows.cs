#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using NeraSpreadSheet.Editing;
using Windows.System;
using Windows.UI.Core;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetTableHost
{
    private UIElement? _platformKeyboardRoot;

    partial void AttachPlatformKeyboard()
    {
        if (Handler?.PlatformView is not UIElement root ||
            ReferenceEquals(_platformKeyboardRoot, root))
        {
            return;
        }

        DetachPlatformKeyboard();
        _platformKeyboardRoot = root;
        root.PreviewKeyDown += OnPlatformPreviewKeyDown;
    }

    partial void DetachPlatformKeyboard()
    {
        if (_platformKeyboardRoot is null)
        {
            return;
        }

        _platformKeyboardRoot.PreviewKeyDown -=
            OnPlatformPreviewKeyDown;
        _platformKeyboardRoot = null;
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
