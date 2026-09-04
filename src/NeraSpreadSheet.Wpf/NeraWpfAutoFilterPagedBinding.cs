using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// WPF dispatcher binding for the shared paged AutoFilter presenter. The
/// collection contains only one native page and therefore does not materialize
/// every distinct value into WPF controls.
/// </summary>
public sealed class NeraWpfAutoFilterPagedBinding :
    INotifyPropertyChanged,
    IDisposable,
    IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SpreadsheetAutoFilterPagedPresenter _presenter;
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

    public NeraWpfAutoFilterPagedBinding(
        SpreadsheetAutoFilterPagedPresenter presenter,
        Dispatcher? dispatcher = null)
    {
        _presenter = presenter ??
            throw new ArgumentNullException(nameof(presenter));
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
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

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            _presenter.InitializeAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            _presenter.RefreshAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            token => _presenter.SetSearchTextAsync(
                searchText,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveNextPageAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAndPublishAsync(
            _presenter.MoveNextPageAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MovePreviousPageAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAndPublishAsync(
            _presenter.MovePreviousPageAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSelectedAsync(
        int pageIndex,
        bool selected,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            token => _presenter.SetSelectedAsync(
                pageIndex,
                selected,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SelectAllVisibleAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            _presenter.SelectAllVisibleAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearVisibleSelectionAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteAndPublishAsync(
            _presenter.ClearVisibleSelectionAsync,
            cancellationToken).ConfigureAwait(false);
    }

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

    public async Task<long> ApplyValueSelectionAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAndPublishAsync(
            _presenter.ApplyValueSelectionAsync,
            cancellationToken).ConfigureAwait(false);
    }

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

    public async Task<long> ClearColumnFilterAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAndPublishAsync(
            _presenter.ClearColumnFilterAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _presenter.Dispose();
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
        GC.SuppressFinalize(this);
    }

    private async Task ExecuteAndPublishAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        await SetBusyAsync(true).ConfigureAwait(false);
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            await PublishAsync().ConfigureAwait(false);
        }
        finally
        {
            await SetBusyAsync(false).ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteAndPublishAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        await SetBusyAsync(true).ConfigureAwait(false);
        try
        {
            var result = await operation(cancellationToken)
                .ConfigureAwait(false);
            await PublishAsync().ConfigureAwait(false);
            return result;
        }
        finally
        {
            await SetBusyAsync(false).ConfigureAwait(false);
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
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return _dispatcher.InvokeAsync(
            action,
            DispatcherPriority.DataBind).Task;
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
