#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
            }
            return;
        }

        if (e.Key == VirtualKey.Down && IsAltPressed())
        {
            Dispatcher.Dispatch(() => TryOpenForActiveCell());
            e.Handled = true;
        }
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
