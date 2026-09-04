using Microsoft.Maui.Controls;
using NeraSpreadSheet.Editing;

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
        _ = SearchAsync(searchText, cancellation, _binding);
    }

    private async Task SearchAsync(
        string? searchText,
        CancellationTokenSource cancellation,
        NeraMauiAutoFilterPagedBinding? binding)
    {
        try
        {
            await Task.Delay(SearchDelay, cancellation.Token);
            if (binding is null || !ReferenceEquals(_binding, binding))
            {
                return;
            }
            await binding.SearchAsync(searchText, cancellation.Token);
            if (!cancellation.IsCancellationRequested && ReferenceEquals(_binding, binding))
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
        _ = RunOperationAsync(operation, cancellation, _binding);
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationTokenSource cancellation,
        NeraMauiAutoFilterPagedBinding? binding)
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
            if (ReferenceEquals(_binding, binding)) Dispatcher.Dispatch(() =>
            {
                if (ReferenceEquals(_binding, binding))
                {
                    _status.Text = exception.Message;
                    SemanticProperties.SetDescription(
                        _status,
                        exception.Message);
                }
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
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        await ApplyCurrentCriterionAsync(binding, token);
        if (!ReferenceEquals(_binding, binding)) return;
        Dispatcher.Dispatch(() =>
        {
            CloseFilterSheet();
            _viewport?.InvalidateMetrics();
            UpdateButtons();
        });
    }

    private async Task ApplyCurrentCriterionAsync(
        NeraMauiAutoFilterPagedBinding binding,
        CancellationToken token)
    {
        var selectedIndex = _menuKindPicker.SelectedIndex;
        var kind = selectedIndex >= 0 && selectedIndex < binding.MenuKinds.Count
            ? binding.MenuKinds[selectedIndex]
            : SpreadsheetAutoFilterMenuKind.Values;
        if (kind == SpreadsheetAutoFilterMenuKind.Values)
        {
            await binding.ApplyValueSelectionAsync(token);
            return;
        }
        var parsed = SpreadsheetAutoFilterCriterionParser.Parse(kind, _criterionInput.Text);
        if (parsed.CustomCondition is { } custom)
        {
            await binding.ApplyCustomFilterAsync(custom, cancellationToken: token);
        }
        else
        {
            await binding.ApplyRichFilterAsync(parsed.RichCriterion!, token);
        }
    }

    private async Task ClearAndCloseAsync(CancellationToken token)
    {
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        await binding.ClearColumnFilterAsync(token);
        if (!ReferenceEquals(_binding, binding)) return;
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
