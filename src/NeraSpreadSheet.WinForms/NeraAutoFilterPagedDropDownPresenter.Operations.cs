using System.Windows.Forms;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraAutoFilterPagedDropDownPresenter
{
    private void ScheduleSearch(string? searchText)
    {
        _searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        _ = SearchAsync(searchText, cancellation, _binding);
    }

    private async Task SearchAsync(
        string? searchText,
        CancellationTokenSource cancellation,
        NeraWinFormsAutoFilterPagedBinding? binding)
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
        NeraWinFormsAutoFilterPagedBinding? binding)
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
            if (_status is not null && ReferenceEquals(_binding, binding))
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
        var binding = _binding;
        if (binding is null)
        {
            return;
        }
        await ApplyCurrentCriterionAsync(binding, token);
        if (ReferenceEquals(_binding, binding)) CloseAndRefresh();
    }

    private async Task ApplyCurrentCriterionAsync(
        NeraWinFormsAutoFilterPagedBinding binding,
        CancellationToken token)
    {
        var selectedIndex = _menuKindBox?.SelectedIndex ?? 0;
        var kind = selectedIndex >= 0 && selectedIndex < binding.MenuKinds.Count
            ? binding.MenuKinds[selectedIndex]
            : SpreadsheetAutoFilterMenuKind.Values;
        if (kind == SpreadsheetAutoFilterMenuKind.Values)
        {
            await binding.ApplyValueSelectionAsync(token);
            return;
        }
        var parsed = SpreadsheetAutoFilterCriterionParser.Parse(kind, _criterionInput?.Text);
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
        if (ReferenceEquals(_binding, binding)) CloseAndRefresh();
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

    private void FocusSearchBox(ToolStripDropDown dropDown)
    {
        BeginInvokeIfHandleCreated(() =>
        {
            if (!ReferenceEquals(_dropDown, dropDown) ||
                !dropDown.Visible ||
                _searchBox is null)
            {
                return;
            }
            _searchBox.Focus();
            _searchBox.SelectAll();
        });
    }

    private void RestoreFocus()
    {
        var target = _focusBeforeOpen;
        _focusBeforeOpen = null;
        BeginInvokeIfHandleCreated(() =>
        {
            if (_disposed)
            {
                return;
            }
            if (target is not null &&
                !target.IsDisposed &&
                target.Visible &&
                target.Enabled &&
                target.Focus())
            {
                return;
            }
            _control.Focus();
        });
    }

    private void BeginInvokeIfHandleCreated(Action action)
    {
        if (_control.IsDisposed || !_control.IsHandleCreated)
        {
            return;
        }
        _control.BeginInvoke((MethodInvoker)(() => action()));
    }
}
