using NeraSpreadSheet.Commands;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
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
        var cancellation = new CancellationTokenSource();
        var binding = _binding;
        lock (_operationStateGate)
        {
            _operationCancellations.Add(cancellation);
            var predecessor = _operationTail;
            _operationTail = RunOperationAsync(
                predecessor,
                operation,
                cancellation,
                binding);
        }
    }

    private async Task RunOperationAsync(
        Task predecessor,
        Func<CancellationToken, Task> operation,
        CancellationTokenSource cancellation,
        NeraMauiAutoFilterPagedBinding? binding)
    {
        try
        {
            try
            {
                await predecessor;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (binding is null || !ReferenceEquals(_binding, binding))
            {
                return;
            }
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
            lock (_operationStateGate)
            {
                _operationCancellations.Remove(cancellation);
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
        var kind = GetSelectedMenuKind(binding);
        if (kind == SpreadsheetAutoFilterMenuKind.Values)
        {
            await binding.ApplyValueSelectionAsync(token);
            return;
        }
        if (kind == SpreadsheetAutoFilterMenuKind.Date &&
            _selectedDateGroups.Count > 0)
        {
            await binding.ApplyRichFilterAsync(
                new SpreadsheetAutoFilterRichCriterion(
                    dateGroups: _selectedDateGroups),
                token);
            return;
        }
        var parsed = SpreadsheetAutoFilterCriterionParser.Parse(
            kind,
            _criterionInput.Text);
        if (parsed.CustomCondition is { } custom)
        {
            var second = string.IsNullOrWhiteSpace(_secondCriterionInput.Text)
                ? parsed.SecondCustomCondition
                : SpreadsheetAutoFilterCriterionParser.ParseCustomCondition(
                    _secondCriterionInput.Text);
            await binding.ApplyCustomFilterAsync(
                custom,
                second,
                second is null
                    ? parsed.CombineWithAnd
                    : _conditionJoinPicker.SelectedIndex != 1,
                token);
        }
        else
        {
            await binding.ApplyRichFilterAsync(parsed.RichCriterion!, token);
        }
    }

    private SpreadsheetAutoFilterMenuKind GetSelectedMenuKind(
        NeraMauiAutoFilterPagedBinding binding)
    {
        var selectedIndex = _menuKindPicker.SelectedIndex;
        return selectedIndex >= 0 && selectedIndex < binding.MenuKinds.Count
            ? binding.MenuKinds[selectedIndex]
            : SpreadsheetAutoFilterMenuKind.Values;
    }

    private async Task RefreshSelectedModeAsync(CancellationToken token)
    {
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        if (GetSelectedMenuKind(binding) == SpreadsheetAutoFilterMenuKind.Date)
        {
            _dateParent = new SpreadsheetAutoFilterDateParent(null, null);
            _datePage = await binding.GetDatePageAsync(
                _dateParent,
                0,
                PageSize,
                token);
        }
        if (ReferenceEquals(_binding, binding))
        {
            UpdateSheetState();
        }
    }

    private async Task MovePageAsync(bool next, CancellationToken token)
    {
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        if (GetSelectedMenuKind(binding) == SpreadsheetAutoFilterMenuKind.Date)
        {
            var offset = Math.Max(
                0,
                (_datePage?.Offset ?? 0) + (next ? PageSize : -PageSize));
            _datePage = await binding.GetDatePageAsync(
                _dateParent,
                offset,
                PageSize,
                token);
        }
        else
        {
            _ = next
                ? await binding.MoveNextPageAsync(token)
                : await binding.MovePreviousPageAsync(token);
        }
        if (ReferenceEquals(_binding, binding))
        {
            UpdateSheetState();
        }
    }

    private async Task NavigateDateIntoAsync(
        SpreadsheetAutoFilterDateNode node,
        CancellationToken token)
    {
        var parent = node.Level == SpreadsheetAutoFilterDateNodeLevel.Year
            ? new SpreadsheetAutoFilterDateParent(node.Year, null)
            : new SpreadsheetAutoFilterDateParent(node.Year, node.Month);
        await LoadDatePageAsync(parent, 0, token);
    }

    private Task NavigateDateBackAsync(CancellationToken token)
    {
        var parent = _dateParent.Month is not null
            ? new SpreadsheetAutoFilterDateParent(_dateParent.Year, null)
            : new SpreadsheetAutoFilterDateParent(null, null);
        return LoadDatePageAsync(parent, 0, token);
    }

    private async Task LoadDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        CancellationToken token)
    {
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        var page = await binding.GetDatePageAsync(parent, offset, PageSize, token);
        if (!ReferenceEquals(_binding, binding))
        {
            return;
        }
        _dateParent = parent;
        _datePage = page;
        UpdateSheetState();
    }

    private static SpreadsheetFilterDateGroup ToDateGroup(
        SpreadsheetAutoFilterDateNode node) => node.Level switch
        {
            SpreadsheetAutoFilterDateNodeLevel.Year => new(
                node.Year,
                SpreadsheetFilterDateGrouping.Year),
            SpreadsheetAutoFilterDateNodeLevel.Month => new(
                node.Year,
                SpreadsheetFilterDateGrouping.Month,
                node.Month),
            _ => new SpreadsheetFilterDateGroup(
                node.Year,
                SpreadsheetFilterDateGrouping.Day,
                node.Month,
                node.Day),
        };

    private string DisplayDateNode(
        SpreadsheetAutoFilterDateNode node) => node.Level switch
        {
            SpreadsheetAutoFilterDateNodeLevel.Year => Localization.Format("Năm {0}", node.Year),
            SpreadsheetAutoFilterDateNodeLevel.Month =>
                Localization.Format("Tháng {0}/{1}", node.Month, node.Year),
            _ => Localization.Format("Ngày {0}/{1}/{2}", node.Day, node.Month, node.Year),
        };

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

    private async Task SortAndCloseAsync(
        bool descending,
        string? customList,
        CancellationToken token)
    {
        var binding = _binding;
        if (binding is null) return;
        await binding.ApplyColumnSortAsync(
            descending,
            customList?.Contains(',', StringComparison.Ordinal) == true
                ? customList
                : null,
            token);
        CloseAfterSort(binding);
    }

    private async Task ReapplyAndCloseAsync(CancellationToken token)
    {
        var binding = _binding;
        if (binding is null) return;
        await binding.ReapplyAsync(token);
        CloseAfterSort(binding);
    }

    private async Task ClearSortAndCloseAsync(CancellationToken token)
    {
        var binding = _binding;
        if (binding is null) return;
        await binding.ClearSortAsync(token);
        CloseAfterSort(binding);
    }

    private void CloseAfterSort(NeraMauiAutoFilterPagedBinding binding)
    {
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
        CancellationTokenSource[] operations;
        lock (_operationStateGate)
        {
            operations = [.. _operationCancellations];
            _operationTail = Task.CompletedTask;
        }
        foreach (var operation in operations)
        {
            operation.Cancel();
        }
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
