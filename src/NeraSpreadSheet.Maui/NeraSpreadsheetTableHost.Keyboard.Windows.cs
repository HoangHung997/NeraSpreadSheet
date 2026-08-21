#if WINDOWS
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using NeraSpreadSheet.Editing;
using Windows.System;
using Windows.UI.Core;
using WinUiControl = Microsoft.UI.Xaml.Controls.Control;
using WinUiTextBox = Microsoft.UI.Xaml.Controls.TextBox;
using WinUiVisibility = Microsoft.UI.Xaml.Visibility;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetTableHost
{
    private const int PlatformSearchFocusMaximumAttempts = 40;
    private static readonly TimeSpan PlatformSearchFocusRetryDelay =
        TimeSpan.FromMilliseconds(50d);

    private FrameworkElement? _platformKeyboardRoot;
    private WinUiTextBox? _platformSearchTextBox;
    private bool _platformSearchFocusPending;
    private bool _platformSearchFocusOperationActive;
    private int _platformSearchFocusAttempt;

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
        AttachPlatformSearchTextBox();
        if (IsFilterSheetOpen)
        {
            BeginPlatformSearchFocus();
        }
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

        DetachPlatformSearchTextBox();
        _sheetOverlay.PropertyChanged -= OnFilterOverlayPropertyChanged;
        _search.Focused -= OnSearchEntryFocused;
        _search.HandlerChanged -= OnSearchHandlerChanged;
        CancelPlatformSearchFocus();
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

        if (IsFilterSheetOpen)
        {
            BeginPlatformSearchFocus();
            return;
        }

        CancelPlatformSearchFocus();
        MovePlatformFocusAwayFromSearch();
    }

    private void OnPlatformLayoutUpdated(object? sender, object e)
    {
        if (_platformSearchFocusPending)
        {
            QueuePlatformSearchFocus();
        }
    }

    private void OnSearchHandlerChanged(object? sender, EventArgs e)
    {
        AttachPlatformSearchTextBox();
        if (_platformSearchFocusPending)
        {
            QueuePlatformSearchFocus();
        }
    }

    private void OnSearchEntryFocused(
        object? sender,
        Microsoft.Maui.Controls.FocusEventArgs e)
    {
        ConfirmPlatformSearchFocus();
    }

    private void OnPlatformSearchTextBoxGotFocus(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmPlatformSearchFocus();
    }

    private void AttachPlatformSearchTextBox()
    {
        var textBox = _search.Handler?.PlatformView as WinUiTextBox;
        if (ReferenceEquals(_platformSearchTextBox, textBox))
        {
            return;
        }

        DetachPlatformSearchTextBox();
        _platformSearchTextBox = textBox;
        if (_platformSearchTextBox is not null)
        {
            _platformSearchTextBox.GotFocus +=
                OnPlatformSearchTextBoxGotFocus;
        }
    }

    private void DetachPlatformSearchTextBox()
    {
        if (_platformSearchTextBox is null)
        {
            return;
        }

        _platformSearchTextBox.GotFocus -=
            OnPlatformSearchTextBoxGotFocus;
        _platformSearchTextBox = null;
    }

    private void BeginPlatformSearchFocus()
    {
        _platformSearchFocusPending = true;
        _platformSearchFocusAttempt = 0;
        QueuePlatformSearchFocus();
    }

    private void CancelPlatformSearchFocus()
    {
        _platformSearchFocusPending = false;
        _platformSearchFocusAttempt = 0;
    }

    private void ConfirmPlatformSearchFocus()
    {
        CancelPlatformSearchFocus();
        SelectSearchText();
    }

    private void QueuePlatformSearchFocus()
    {
        if (!_platformSearchFocusPending ||
            _platformSearchFocusOperationActive ||
            _platformSearchFocusAttempt >=
                PlatformSearchFocusMaximumAttempts ||
            _disposed ||
            !IsFilterSheetOpen)
        {
            return;
        }

        if (IsPlatformSearchFocused())
        {
            ConfirmPlatformSearchFocus();
            return;
        }

        if (IsValueListFocused())
        {
            CancelPlatformSearchFocus();
            return;
        }

        AttachPlatformSearchTextBox();
        var textBox = _platformSearchTextBox;
        if (textBox is null ||
            !textBox.IsLoaded ||
            textBox.Visibility != WinUiVisibility.Visible)
        {
            return;
        }

        _platformSearchFocusOperationActive = true;
        _platformSearchFocusAttempt++;
        _ = TryCompletePlatformSearchFocusAsync(textBox);
    }

    private async Task TryCompletePlatformSearchFocusAsync(
        WinUiTextBox textBox)
    {
        var retry = false;
        try
        {
            _search.Focus();
            var result = await FocusManager.TryFocusAsync(
                textBox,
                FocusState.Programmatic);
            if (result.Succeeded && IsPlatformSearchFocused())
            {
                ConfirmPlatformSearchFocus();
            }
            else
            {
                retry = ShouldRetryPlatformSearchFocus();
            }
        }
        catch (COMException)
        {
            retry = ShouldRetryPlatformSearchFocus();
        }
        catch (InvalidOperationException)
        {
            retry = ShouldRetryPlatformSearchFocus();
        }
        finally
        {
            _platformSearchFocusOperationActive = false;
        }

        if (!retry)
        {
            return;
        }

        await Task.Delay(PlatformSearchFocusRetryDelay);
        QueuePlatformSearchFocus();
    }

    private bool ShouldRetryPlatformSearchFocus() =>
        _platformSearchFocusPending &&
        !_disposed &&
        IsFilterSheetOpen &&
        _platformSearchFocusAttempt <
            PlatformSearchFocusMaximumAttempts;

    private void MovePlatformFocusAwayFromSearch()
    {
        if (!IsPlatformSearchFocused())
        {
            return;
        }

        _search.Unfocus();
        VisualElement target = Spreadsheet;
        if (_session?.TryResolveActiveTableFilterTarget(
                out var activeTarget) == true &&
            _buttons.TryGetValue(
                (activeTarget.TableId, activeTarget.ColumnId),
                out var button) &&
            button.IsVisible)
        {
            target = button;
        }

        target.Focus();
        if (target.Handler?.PlatformView is WinUiControl control)
        {
            control.Focus(FocusState.Programmatic);
            return;
        }

        if (_platformKeyboardRoot is WinUiControl rootControl)
        {
            rootControl.Focus(FocusState.Programmatic);
        }
    }

    private bool IsPlatformSearchFocused() =>
        _search.IsFocused ||
        _platformSearchTextBox?.FocusState is
            FocusState.Keyboard or
            FocusState.Pointer or
            FocusState.Programmatic;

    private void SelectSearchText()
    {
        _search.CursorPosition = 0;
        _search.SelectionLength = _search.Text?.Length ?? 0;
        _platformSearchTextBox?.SelectAll();
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

        var searchFocused = IsPlatformSearchFocused();
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
