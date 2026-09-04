#if WINDOWS
using Microsoft.Maui.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetAutoFilterHost
{
    private UIElement? _platformKeyboardRoot;
    private int _platformSearchFocusGeneration;

    partial void AttachPlatformKeyboard()
    {
        var platformView = Spreadsheet.Handler?.PlatformView as UIElement;
        var root = platformView?.XamlRoot?.Content as UIElement;
        if (ReferenceEquals(root, _platformKeyboardRoot))
        {
            return;
        }
        DetachPlatformKeyboard();
        _platformKeyboardRoot = root;
        if (_platformKeyboardRoot is not null)
        {
            _platformKeyboardRoot.KeyDown += OnPlatformKeyDown;
        }
    }

    partial void DetachPlatformKeyboard()
    {
        if (_platformKeyboardRoot is not null)
        {
            _platformKeyboardRoot.KeyDown -= OnPlatformKeyDown;
        }
        _platformKeyboardRoot = null;
        _platformSearchFocusGeneration++;
    }

    partial void OnSheetOpenedPlatform()
    {
        var generation = ++_platformSearchFocusGeneration;
        _ = FocusSearchAsync(generation);
    }

    partial void OnSheetClosedPlatform()
    {
        _platformSearchFocusGeneration++;
        Dispatcher.Dispatch(() =>
        {
            if (_disposed)
            {
                return;
            }
            if (Spreadsheet.Handler?.PlatformView is UIElement platformView)
            {
                platformView.Focus(FocusState.Programmatic);
            }
        });
    }

    private void OnPlatformKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (IsFilterSheetOpen)
        {
            var focusedElement = _platformKeyboardRoot?.XamlRoot is { } xamlRoot
                ? FocusManager.GetFocusedElement(xamlRoot) as DependencyObject
                : null;
            var searchFocused = IsPlatformFocusWithin(_search, focusedElement);
            var valuesFocused = IsPlatformFocusWithin(_values, focusedElement);
            var dateValuesFocused = IsPlatformFocusWithin(_dateValues, focusedElement);
            if (!ShouldHandlePlatformFilterKey(
                    e.Key,
                    searchFocused,
                    valuesFocused,
                    dateValuesFocused))
            {
                return;
            }
            if (e.Key == VirtualKey.Escape)
            {
                Dispatcher.Dispatch(CloseFilterSheet);
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.PageDown)
            {
                Dispatcher.Dispatch(() => StartOperation(async token =>
                {
                    var binding = _binding;
                    if (binding is not null &&
                        await MovePageFromKeyboardAsync(binding, next: true, token) &&
                        ReferenceEquals(_binding, binding))
                    {
                        UpdateSheetState();
                    }
                }));
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.PageUp)
            {
                Dispatcher.Dispatch(() => StartOperation(async token =>
                {
                    var binding = _binding;
                    if (binding is not null &&
                        await MovePageFromKeyboardAsync(binding, next: false, token) &&
                        ReferenceEquals(_binding, binding))
                    {
                        UpdateSheetState();
                    }
                }));
                e.Handled = true;
                return;
            }
            if (e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End)
            {
                Dispatcher.Dispatch(() => MoveValueKeyboardFocus(e.Key, searchFocused));
                e.Handled = true;
                return;
            }
            if (e.Key is VirtualKey.Space or VirtualKey.Enter)
            {
                Dispatcher.Dispatch(ToggleKeyboardValue);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == VirtualKey.Down && IsAltPressed())
        {
            Dispatcher.Dispatch(() => TryOpenForActiveCell());
            e.Handled = true;
        }
    }

    private void MoveValueKeyboardFocus(VirtualKey key, bool fromSearch)
    {
        var binding = _binding;
        if (binding is null || binding.Items.Count == 0 ||
            GetSelectedMenuKind(binding) != SpreadsheetAutoFilterMenuKind.Values)
        {
            return;
        }
        _keyboardActiveIndex = key switch
        {
            VirtualKey.Home => 0,
            VirtualKey.End => binding.Items.Count - 1,
            VirtualKey.Up when fromSearch => binding.Items.Count - 1,
            VirtualKey.Down when fromSearch => 0,
            VirtualKey.Up => Math.Max(0, _keyboardActiveIndex - 1),
            _ => Math.Min(binding.Items.Count - 1, _keyboardActiveIndex + 1),
        };
        _values.ScrollTo(_keyboardActiveIndex, position: ScrollToPosition.MakeVisible, animate: false);
        SemanticProperties.SetHint(
            _values,
            $"Mục {_keyboardActiveIndex + 1:N0}/{binding.Items.Count:N0}: {binding.Items[_keyboardActiveIndex].DisplayText}");
    }

    internal static bool ShouldHandlePlatformFilterKey(
        VirtualKey key,
        bool searchFocused,
        bool valuesFocused,
        bool dateValuesFocused) =>
        key == VirtualKey.Escape ||
        (key is VirtualKey.PageUp or VirtualKey.PageDown &&
            (valuesFocused || dateValuesFocused)) ||
        (key is VirtualKey.Up or VirtualKey.Down &&
            (searchFocused || valuesFocused)) ||
        (key is VirtualKey.Home or VirtualKey.End or VirtualKey.Space or VirtualKey.Enter &&
            valuesFocused);

    private static bool IsPlatformFocusWithin(
        VisualElement element,
        DependencyObject? focusedElement)
    {
        if (focusedElement is null ||
            element.Handler?.PlatformView is not DependencyObject platformView)
        {
            return false;
        }
        for (var current = focusedElement;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, platformView))
            {
                return true;
            }
        }
        return false;
    }

    private void ToggleKeyboardValue()
    {
        var binding = _binding;
        if (binding is null || binding.Items.Count == 0 ||
            GetSelectedMenuKind(binding) != SpreadsheetAutoFilterMenuKind.Values)
        {
            return;
        }
        var index = Math.Clamp(_keyboardActiveIndex, 0, binding.Items.Count - 1);
        var selected = !binding.Items[index].IsSelected;
        StartOperation(async token =>
        {
            await binding.SetSelectedAsync(index, selected, token);
            if (ReferenceEquals(_binding, binding)) UpdateSheetState();
        });
    }

    private async Task<bool> MovePageFromKeyboardAsync(
        NeraMauiAutoFilterPagedBinding binding,
        bool next,
        CancellationToken token)
    {
        if (GetSelectedMenuKind(binding) == SpreadsheetAutoFilterMenuKind.Date)
        {
            var current = _datePage;
            if (current is null || (next ? !current.HasNextPage : !current.HasPreviousPage))
            {
                return false;
            }
            var offset = Math.Max(0, current.Offset + (next ? PageSize : -PageSize));
            _datePage = await binding.GetDatePageAsync(
                _dateParent,
                offset,
                PageSize,
                token);
            return true;
        }
        return next
            ? await binding.MoveNextPageAsync(token)
            : await binding.MovePreviousPageAsync(token);
    }

    private async Task FocusSearchAsync(int generation)
    {
        const int maximumAttempts = 40;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            if (_disposed ||
                !IsFilterSheetOpen ||
                generation != _platformSearchFocusGeneration)
            {
                return;
            }

            var focused = false;
            await Dispatcher.DispatchAsync(() =>
            {
                if (_search.Handler?.PlatformView is TextBox textBox &&
                    textBox.IsLoaded &&
                    textBox.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
                {
                    focused = textBox.Focus(FocusState.Programmatic);
                    if (focused)
                    {
                        textBox.SelectAll();
                    }
                }
            });
            if (focused)
            {
                return;
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static bool IsAltPressed()
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(
            VirtualKey.Menu);
        return (state & CoreVirtualKeyStates.Down) != 0;
    }
}
#endif
