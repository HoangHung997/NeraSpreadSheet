using System.Windows.Forms;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraAutoFilterPagedDropDownPresenter
{
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
            await _binding.SearchAsync(searchText, cancellation.Token);
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
