using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetAutoFilterHost
{
    private void ScheduleSearch(string? searchText)
    {
        if (!_sheetOverlay.IsVisible)
        {
            return;
        }
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
            await _binding.SearchAsync(searchText, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                UpdateSheetState();
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

    private void StartOperation(Func<CancellationToken, Task> operation)
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
            Dispatcher.Dispatch(() =>
            {
                _status.Text = exception.Message;
                SemanticProperties.SetDescription(
                    _status,
                    exception.Message);
            });
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
        Dispatcher.Dispatch(() =>
        {
            CloseFilterSheet();
            _viewport?.InvalidateMetrics();
            UpdateButtons();
        });
    }

    private async Task ClearAndCloseAsync(CancellationToken token)
    {
        if (_binding is null)
        {
            return;
        }
        await _binding.ClearColumnFilterAsync(token);
        Dispatcher.Dispatch(() =>
        {
            CloseFilterSheet();
            _viewport?.InvalidateMetrics();
            UpdateButtons();
        });
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

    private void FocusSearch()
    {
        Dispatcher.Dispatch(() =>
        {
            if (_disposed || !_sheetOverlay.IsVisible)
            {
                return;
            }
            _search.Focus();
        });
    }

    private void RestoreFocus()
    {
        var target = _focusBeforeOpen;
        _focusBeforeOpen = null;
        Dispatcher.Dispatch(() =>
        {
            if (_disposed)
            {
                return;
            }
            if (target is not null &&
                target.IsVisible &&
                target.IsEnabled &&
                target.Focus())
            {
                return;
            }
            Spreadsheet.Focus();
        });
    }
}
