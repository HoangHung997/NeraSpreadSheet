using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetAutoFilterPagedPresenterSnapshot(
    SpreadsheetAutoFilterTarget Target,
    long Generation,
    string SearchText,
    int PageOffset,
    int PageSize,
    int TotalItemCount,
    bool IsInitialized,
    bool IsSourceTruncated,
    bool HasPreviousPage,
    bool HasNextPage,
    IReadOnlyList<SpreadsheetAutoFilterMenuKind> MenuKinds,
    IReadOnlyList<SpreadsheetTableFilterValueItem> Values);

/// <summary>
/// UI-neutral current-page coordinator shared by WPF, WinForms and MAUI.
/// It owns paging/search position while <see cref="SpreadsheetAutoFilterPagedView"/>
/// owns immutable page data and production filter mutations.
/// </summary>
public sealed class SpreadsheetAutoFilterPagedPresenter :
    IDisposable,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SpreadsheetAutoFilterPagedView _view;
    private readonly object _stateGate = new();

    private SpreadsheetAutoFilterPagedPage? _page;
    private bool _disposed;

    public SpreadsheetAutoFilterPagedPresenter(
        SpreadsheetSession session,
        SpreadsheetAutoFilterTarget target,
        int pageSize = SpreadsheetAutoFilterPagedView.DefaultPageSize,
        int maximumRows =
            SpreadsheetTablePresenterController.DefaultMaximumRows,
        int maximumDistinctValues =
            SpreadsheetTablePresenterController.DefaultMaximumDistinctValues)
        : this(
            new SpreadsheetAutoFilterPagedView(
                SpreadsheetAutoFilterPagedSessionFactory.Create(
                    session,
                    target,
                    maximumRows,
                    maximumDistinctValues),
                pageSize))
    {
    }

    public SpreadsheetAutoFilterPagedPresenter(
        SpreadsheetAutoFilterPagedView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    public event EventHandler? Changed;

    public SpreadsheetAutoFilterTarget Target => _view.Target;

    public SpreadsheetAutoFilterPagedPresenterSnapshot Capture()
    {
        lock (_stateGate)
        {
            EnsureNotDisposedUnsafe();
            return CreateSnapshotUnsafe();
        }
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await _view.InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            var page = await _view.GetPageAsync(
                0,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = page;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await _view.RefreshAsync(cancellationToken)
                .ConfigureAwait(false);
            var page = await _view.GetPageAsync(
                0,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = page;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetSearchTextAsync(
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await _view.SetSearchTextAsync(
                searchText,
                cancellationToken).ConfigureAwait(false);
            var page = await _view.GetPageAsync(
                0,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = page;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> MoveNextPageAsync(
        CancellationToken cancellationToken = default)
    {
        return await MovePageAsync(
            moveNext: true,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MovePreviousPageAsync(
        CancellationToken cancellationToken = default)
    {
        return await MovePageAsync(
            moveNext: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSelectedAsync(
        int pageIndex,
        bool selected,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            SpreadsheetAutoFilterPagedPage page;
            lock (_stateGate)
            {
                page = RequirePageUnsafe();
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                    pageIndex,
                    page.Values.Count);
            }

            await _view.SetSelectedAsync(
                checked(page.Offset + pageIndex),
                selected,
                cancellationToken).ConfigureAwait(false);
            var refreshed = await _view.GetPageAsync(
                page.Offset,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = refreshed;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SelectAllVisibleAsync(
        CancellationToken cancellationToken = default)
    {
        await ChangeVisibleSelectionAsync(
            select: true,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearVisibleSelectionAsync(
        CancellationToken cancellationToken = default)
    {
        await ChangeVisibleSelectionAsync(
            select: false,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        _view.GetDatePageAsync(parent, offset, pageSize, cancellationToken);

    public async Task<long> ApplyValueSelectionAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteMutationAsync(
            token => _view.ApplyValueSelectionAsync(token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> ApplyCustomFilterAsync(
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        return await ExecuteMutationAsync(
            token => _view.ApplyCustomFilterAsync(
                firstCondition,
                secondCondition,
                combineWithAnd,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> ApplyRichFilterAsync(
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        return await ExecuteMutationAsync(
            token => _view.ApplyRichFilterAsync(criterion, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> ClearColumnFilterAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteMutationAsync(
            token => _view.ClearColumnFilterAsync(token),
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _page = null;
        }
        _view.Dispose();
        _operationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _page = null;
        }
        await _view.DisposeAsync().ConfigureAwait(false);
        _operationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> MovePageAsync(
        bool moveNext,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        var moved = false;
        try
        {
            ThrowIfDisposed();
            SpreadsheetAutoFilterPagedPage page;
            lock (_stateGate)
            {
                page = RequirePageUnsafe();
            }

            var offset = moveNext
                ? checked(page.Offset + page.Values.Count)
                : Math.Max(0, page.Offset - page.PageSize);
            if ((moveNext && !page.HasNextPage) ||
                (!moveNext && !page.HasPreviousPage))
            {
                return false;
            }

            var replacement = await _view.GetPageAsync(
                offset,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = replacement;
                moved = true;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        if (moved)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return moved;
    }

    private async Task ChangeVisibleSelectionAsync(
        bool select,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            SpreadsheetAutoFilterPagedPage page;
            lock (_stateGate)
            {
                page = RequirePageUnsafe();
            }

            if (select)
            {
                await _view.SelectAllVisibleAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _view.ClearVisibleSelectionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            var viewSnapshot = _view.Capture();
            var lastPageOffset = Math.Max(
                0,
                (Math.Max(0, viewSnapshot.TotalItemCount - 1) /
                    page.PageSize) * page.PageSize);
            var replacement = await _view.GetPageAsync(
                Math.Min(page.Offset, lastPageOffset),
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = replacement;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<long> ExecuteMutationAsync(
        Func<CancellationToken, Task<long>> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        long generation;
        try
        {
            ThrowIfDisposed();
            generation = await mutation(cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = null;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return generation;
    }

    private SpreadsheetAutoFilterPagedPresenterSnapshot
        CreateSnapshotUnsafe()
    {
        if (_page is null)
        {
            var viewSnapshot = _view.Capture();
            return new SpreadsheetAutoFilterPagedPresenterSnapshot(
                Target,
                viewSnapshot.Generation,
                viewSnapshot.SearchText,
                0,
                _view.PageSize,
                viewSnapshot.TotalItemCount,
                false,
                viewSnapshot.IsSourceTruncated,
                false,
                false,
                viewSnapshot.MenuKinds,
                []);
        }

        return new SpreadsheetAutoFilterPagedPresenterSnapshot(
            Target,
            _page.Generation,
            _page.SearchText,
            _page.Offset,
            _page.PageSize,
            _page.TotalVisibleValueCount,
            true,
            _page.IsSourceTruncated,
            _page.HasPreviousPage,
            _page.HasNextPage,
            _page.MenuKinds,
            _page.Values);
    }

    private SpreadsheetAutoFilterPagedPage RequirePageUnsafe() =>
        _page ?? throw new InvalidOperationException(
            "Initialize the AutoFilter presenter before using it.");

    private void ThrowIfDisposed()
    {
        lock (_stateGate)
        {
            EnsureNotDisposedUnsafe();
        }
    }

    private void EnsureNotDisposedUnsafe()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
