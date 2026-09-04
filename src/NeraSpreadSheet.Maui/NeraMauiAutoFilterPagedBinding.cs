using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// MAUI dispatcher binding for the shared paged AutoFilter presenter. The
/// collection contains one page and can be consumed by CollectionView or a
/// responsive bottom sheet without materializing all distinct values.
/// </summary>
public sealed class NeraMauiAutoFilterPagedBinding :
    INotifyPropertyChanged,
    IDisposable,
    IAsyncDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly SpreadsheetAutoFilterPagedPresenter _presenter;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private bool _disposed;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private int _pageOffset;
    private int _pageSize;
    private int _totalItemCount;
    private bool _hasPreviousPage;
    private bool _hasNextPage;
    private bool _isSourceTruncated;
    private IReadOnlyList<SpreadsheetAutoFilterMenuKind> _menuKinds = [];
    private string _accessibilityAnnouncement = string.Empty;

    public NeraMauiAutoFilterPagedBinding(
        SpreadsheetAutoFilterPagedPresenter presenter,
        IDispatcher dispatcher)
    {
        _presenter = presenter ??
            throw new ArgumentNullException(nameof(presenter));
        _dispatcher = dispatcher ??
            throw new ArgumentNullException(nameof(dispatcher));
        Items = new ObservableCollection<SpreadsheetTableFilterValueItem>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SpreadsheetTableFilterValueItem> Items
    {
        get;
    }

    public SpreadsheetAutoFilterTarget Target => _presenter.Target;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string SearchText
    {
        get => _searchText;
        private set => SetField(ref _searchText, value);
    }

    public int PageOffset
    {
        get => _pageOffset;
        private set => SetField(ref _pageOffset, value);
    }

    public int PageSize
    {
        get => _pageSize;
        private set => SetField(ref _pageSize, value);
    }

    public int TotalItemCount
    {
        get => _totalItemCount;
        private set => SetField(ref _totalItemCount, value);
    }

    public bool HasPreviousPage
    {
        get => _hasPreviousPage;
        private set => SetField(ref _hasPreviousPage, value);
    }

    public bool HasNextPage
    {
        get => _hasNextPage;
        private set => SetField(ref _hasNextPage, value);
    }

    public bool IsSourceTruncated
    {
        get => _isSourceTruncated;
        private set => SetField(ref _isSourceTruncated, value);
    }

    public IReadOnlyList<SpreadsheetAutoFilterMenuKind> MenuKinds
    {
        get => _menuKinds;
        private set => SetField(ref _menuKinds, value);
    }

    public string AccessibilityAnnouncement
    {
        get => _accessibilityAnnouncement;
        private set => SetField(ref _accessibilityAnnouncement, value);
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.InitializeAsync,
            cancellationToken);

    public Task RefreshAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.RefreshAsync,
            cancellationToken);

    public Task SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            token => _presenter.SetSearchTextAsync(
                searchText,
                token),
            cancellationToken);

    public Task<bool> MoveNextPageAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.MoveNextPageAsync,
            cancellationToken);

    public Task<bool> MovePreviousPageAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.MovePreviousPageAsync,
            cancellationToken);

    public Task SetSelectedAsync(
        int pageIndex,
        bool selected,
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            token => _presenter.SetSelectedAsync(
                pageIndex,
                selected,
                token),
            cancellationToken);

    public Task SelectAllVisibleAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.SelectAllVisibleAsync,
            cancellationToken);

    public Task ClearVisibleSelectionAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.ClearVisibleSelectionAsync,
            cancellationToken);

    public Task<bool> ApplyColumnSortAsync(
        bool descending,
        string? customList = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            token => _presenter.ApplyColumnSortAsync(descending, customList, token),
            cancellationToken);

    public Task<bool> ReapplyAsync(CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(_presenter.ReapplyAsync, cancellationToken);

    public Task<bool> ClearSortAsync(CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(_presenter.ClearSortAsync, cancellationToken);

    public Task<long> ApplyValueSelectionAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.ApplyValueSelectionAsync,
            cancellationToken);

    public Task<long> ApplyRichFilterAsync(
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            token => _presenter.ApplyRichFilterAsync(criterion, token),
            cancellationToken);

    public Task<long> ApplyCustomFilterAsync(
        NeraSpreadSheet.Core.TableFilterCondition firstCondition,
        NeraSpreadSheet.Core.TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            token => _presenter.ApplyCustomFilterAsync(
                firstCondition,
                secondCondition,
                combineWithAnd,
                token),
            cancellationToken);

    public Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        _presenter.GetDatePageAsync(parent, offset, pageSize, cancellationToken);

    public Task<long> ClearColumnFilterAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAndPublishAsync(
            _presenter.ClearColumnFilterAsync,
            cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _presenter.Dispose();
        _operationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await _presenter.DisposeAsync().ConfigureAwait(false);
        _operationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ExecuteAndPublishAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetBusyAsync(true).ConfigureAwait(false);
            try
            {
                await InvokeAsync(async () =>
                    {
                        await operation(cancellationToken);
                        return true;
                    })
                    .ConfigureAwait(false);
                await PublishAsync().ConfigureAwait(false);
            }
            finally
            {
                await SetBusyAsync(false).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<T> ExecuteAndPublishAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetBusyAsync(true).ConfigureAwait(false);
            try
            {
                var result = await InvokeAsync(() => operation(cancellationToken))
                    .ConfigureAwait(false);
                await PublishAsync().ConfigureAwait(false);
                return result;
            }
            finally
            {
                await SetBusyAsync(false).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private Task SetBusyAsync(bool value) =>
        InvokeAsync(() => IsBusy = value);

    private Task PublishAsync()
    {
        var snapshot = _presenter.Capture();
        return InvokeAsync(() =>
        {
            Items.Clear();
            foreach (var item in snapshot.Values)
            {
                Items.Add(item);
            }
            SearchText = snapshot.SearchText;
            PageOffset = snapshot.PageOffset;
            PageSize = snapshot.PageSize;
            TotalItemCount = snapshot.TotalItemCount;
            HasPreviousPage = snapshot.HasPreviousPage;
            HasNextPage = snapshot.HasNextPage;
            IsSourceTruncated = snapshot.IsSourceTruncated;
            MenuKinds = snapshot.MenuKinds;
            AccessibilityAnnouncement = snapshot.AccessibilityAnnouncement;
            OnPropertyChanged(nameof(Target));
        });
    }

    private Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();
        if (!_dispatcher.IsDispatchRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.Dispatch(() =>
            {
                try
                {
                    action();
                    completion.SetResult(null);
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException(
                "The MAUI dispatcher rejected the AutoFilter binding update."));
        }
        return completion.Task;
    }

    private Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();
        if (!_dispatcher.IsDispatchRequired)
        {
            return action();
        }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.Dispatch(async () =>
            {
                try
                {
                    completion.SetResult(await action());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException(
                "The MAUI dispatcher rejected the AutoFilter operation."));
        }
        return completion.Task;
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
