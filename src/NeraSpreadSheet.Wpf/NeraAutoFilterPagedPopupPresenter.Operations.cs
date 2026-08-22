using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraAutoFilterPagedPopupPresenter
{
    private void StartSelectionChange(int pageIndex, bool selected) =>
        StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.SetSelectedAsync(
                pageIndex,
                selected,
                token);
        });

    private void ScheduleSearch(string? searchText)
    {
        _searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        _ = SearchAsync(searchText, cancellation);
    }

    private async Task SearchAsync(
        string? searchText,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(SearchDelay, cancellation.Token);
            if (_binding is null)
            {
                return;
            }
            await _binding.SearchAsync(
                searchText,
                cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                RebuildPage();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                _searchCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private void StartOperation(
        Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operationCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        _ = RunOperationAsync(operation, cancellation);
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationTokenSource cancellation)
    {
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (_status is not null)
            {
                _status.Text = exception.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private async Task ApplyAndCloseAsync(CancellationToken token)
    {
        if (_binding is null)
        {
            return;
        }
        await _binding.ApplyValueSelectionAsync(token);
        CloseAndRefresh();
    }

    private async Task ClearAndCloseAsync(CancellationToken token)
    {
        if (_binding is null)
        {
            return;
        }
        await _binding.ClearColumnFilterAsync(token);
        CloseAndRefresh();
    }

    private void CancelOperations()
    {
        _operationCancellation?.Cancel();
        _operationCancellation = null;
        _searchCancellation?.Cancel();
        _searchCancellation = null;
    }

    private void DisposeBinding()
    {
        _binding?.Dispose();
        _binding = null;
    }

    private void FocusSearchBox(Popup popup)
    {
        _control.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!ReferenceEquals(_popup, popup) ||
                    !popup.IsOpen ||
                    _searchBox is null)
                {
                    return;
                }
                _searchBox.Focus();
                _searchBox.SelectAll();
            }));
    }

    private void RestoreFocus(IInputElement? focusTarget)
    {
        _control.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (_disposed)
                {
                    return;
                }
                if (focusTarget is not null &&
                    Keyboard.Focus(focusTarget) is not null)
                {
                    return;
                }
                _control.Focus();
            }));
    }

    private static Button CreateCommandButton(
        string text,
        string automationId)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 78d,
            Margin = new Thickness(2d),
            Padding = new Thickness(8d, 3d, 8d, 3d),
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, text);
        return button;
    }

    private void CloseAndRefresh()
    {
        Close();
        _viewport?.InvalidateMetrics();
        _control.InvalidateVisual();
        _adorner?.InvalidateVisual();
    }
}
