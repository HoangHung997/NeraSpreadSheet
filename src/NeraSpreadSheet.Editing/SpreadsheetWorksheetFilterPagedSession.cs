using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetWorksheetFilterPagedResult(
    long Generation,
    SpreadsheetWorksheetFilterValuePage Page);

/// <summary>
/// Owns one cancellable, generation-checked direct worksheet AutoFilter menu
/// for native paged presenters.
/// </summary>
public sealed class SpreadsheetWorksheetFilterPagedSession :
    IDisposable,
    IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SpreadsheetWorksheetFilterPresenterController _controller;
    private readonly int _worksheetColumnIndex;
    private readonly int _maximumRows;
    private readonly int _maximumDistinctValues;

    private CancellationTokenSource? _refreshCancellation;
    private SpreadsheetWorksheetFilterMenu? _menu;
    private long _generation;
    private bool _disposed;

    public SpreadsheetWorksheetFilterPagedSession(
        SpreadsheetSession session,
        int worksheetColumnIndex,
        int maximumRows =
            SpreadsheetWorksheetFilterPresenterController.DefaultMaximumRows,
        int maximumDistinctValues =
            SpreadsheetWorksheetFilterPresenterController
                .DefaultMaximumDistinctValues)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(
            worksheetColumnIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumDistinctValues);

        _controller =
            new SpreadsheetWorksheetFilterPresenterController(session);
        _worksheetColumnIndex = worksheetColumnIndex;
        _maximumRows = maximumRows;
        _maximumDistinctValues = maximumDistinctValues;
    }

    public event EventHandler? Refreshed;

    public event EventHandler? Invalidated;

    public long Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _menu is not null && !_disposed;
            }
        }
    }

    public async Task<long> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource refreshCancellation;
        long requestedGeneration;
        lock (_gate)
        {
            ThrowIfDisposed();
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            refreshCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            _refreshCancellation = refreshCancellation;
            requestedGeneration = checked(++_generation);
        }

        SpreadsheetWorksheetFilterMenu menu;
        try
        {
            menu = await Task.Run(
                () => _controller.OpenFilterMenu(
                    _worksheetColumnIndex,
                    _maximumRows,
                    _maximumDistinctValues),
                refreshCancellation.Token).ConfigureAwait(false);
            refreshCancellation.Token.ThrowIfCancellationRequested();
        }
        catch
        {
            CompleteRefresh(
                refreshCancellation,
                requestedGeneration,
                menu: null);
            throw;
        }

        if (!CompleteRefresh(
                refreshCancellation,
                requestedGeneration,
                menu))
        {
            throw new OperationCanceledException(
                "A newer worksheet filter refresh superseded this request.");
        }

        Refreshed?.Invoke(this, EventArgs.Empty);
        return requestedGeneration;
    }

    public async Task<SpreadsheetWorksheetFilterPagedResult> GetPageAsync(
        string? searchText,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var (menu, generation) = GetReadyMenu();
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    menu.SetSearchText(searchText);
                    var page = menu.CapturePage(
                        offset,
                        pageSize,
                        cancellationToken);
                    return new SpreadsheetWorksheetFilterPagedResult(
                        generation,
                        page);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SetSelectedAsync(
        long generation,
        CellValue value,
        bool selected,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetReadyMenu(generation).SetSelected(value, selected);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task SelectAllVisibleAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default) =>
        ChangeVisibleSelectionAsync(
            generation,
            searchText,
            select: true,
            cancellationToken);

    public Task ClearVisibleSelectionAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default) =>
        ChangeVisibleSelectionAsync(
            generation,
            searchText,
            select: false,
            cancellationToken);

    public async Task<long> ApplyValueSelectionAsync(
        long generation,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteMutation(
                generation,
                static menu => menu.ApplyValueSelection());
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<long> ApplyCustomFilterAsync(
        long generation,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteMutation(
                generation,
                menu => menu.ApplyCustomFilter(
                    firstCondition,
                    secondCondition,
                    combineWithAnd));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<long> ClearColumnFilterAsync(
        long generation,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteMutation(
                generation,
                static menu => menu.ClearColumnFilter());
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _menu = null;
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _refreshCancellation = null;
        }

        _operationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ChangeVisibleSelectionAsync(
        long generation,
        string? searchText,
        bool select,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var menu = GetReadyMenu(generation);
            menu.SetSearchText(searchText);
            if (select)
            {
                menu.SelectAllVisible();
            }
            else
            {
                menu.ClearVisibleSelection();
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private (
        SpreadsheetWorksheetFilterMenu Menu,
        long Generation) GetReadyMenu()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return (
                _menu ?? throw new InvalidOperationException(
                    "Refresh the worksheet filter session before requesting a page."),
                _generation);
        }
    }

    private SpreadsheetWorksheetFilterMenu GetReadyMenu(
        long generation)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (generation != _generation)
            {
                throw new InvalidOperationException(
                    "The worksheet filter page belongs to a stale generation.");
            }

            return _menu ?? throw new InvalidOperationException(
                "Refresh the worksheet filter session before changing selection.");
        }
    }

    private long ExecuteMutation(
        long generation,
        Action<SpreadsheetWorksheetFilterMenu> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        long invalidatedGeneration;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (generation != _generation || _menu is null)
            {
                throw new InvalidOperationException(
                    "The worksheet filter mutation belongs to a stale generation.");
            }

            mutation(_menu);
            _menu = null;
            invalidatedGeneration = checked(++_generation);
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    private bool CompleteRefresh(
        CancellationTokenSource refreshCancellation,
        long requestedGeneration,
        SpreadsheetWorksheetFilterMenu? menu)
    {
        var published = false;
        lock (_gate)
        {
            if (!_disposed &&
                requestedGeneration == _generation &&
                ReferenceEquals(
                    refreshCancellation,
                    _refreshCancellation) &&
                menu is not null &&
                !refreshCancellation.IsCancellationRequested)
            {
                _menu = menu;
                published = true;
            }
            if (ReferenceEquals(
                    refreshCancellation,
                    _refreshCancellation))
            {
                _refreshCancellation = null;
            }
        }

        refreshCancellation.Dispose();
        return published;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
