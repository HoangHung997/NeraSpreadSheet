using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetTableFilterPagedResult(
    long Generation,
    SpreadsheetTableFilterValuePage Page);

/// <summary>
/// Owns one cancellable, generation-checked Table filter-value snapshot for
/// native virtualized presenters. A completed refresh publishes only when it is
/// still the newest request; stale refreshes cannot replace a newer menu.
/// </summary>
public sealed class SpreadsheetTableFilterPagedSession :
    IDisposable,
    IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly SpreadsheetTablePresenterController _controller;
    private readonly Guid _tableId;
    private readonly Guid _columnId;
    private readonly int _maximumRows;
    private readonly int _maximumDistinctValues;

    private CancellationTokenSource? _refreshCancellation;
    private SpreadsheetTableFilterMenu? _menu;
    private long _generation;
    private bool _disposed;

    public SpreadsheetTableFilterPagedSession(
        SpreadsheetSession session,
        Guid tableId,
        Guid columnId,
        int maximumRows =
            SpreadsheetTablePresenterController.DefaultMaximumRows,
        int maximumDistinctValues =
            SpreadsheetTablePresenterController.DefaultMaximumDistinctValues)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (tableId == Guid.Empty)
        {
            throw new ArgumentException(
                "Table identity must not be empty.",
                nameof(tableId));
        }
        if (columnId == Guid.Empty)
        {
            throw new ArgumentException(
                "Table-column identity must not be empty.",
                nameof(columnId));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumDistinctValues);

        _controller = new SpreadsheetTablePresenterController(session);
        _tableId = tableId;
        _columnId = columnId;
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
        CancellationTokenSource? previousCancellation;
        long requestedGeneration;
        lock (_gate)
        {
            ThrowIfDisposed();
            previousCancellation = _refreshCancellation;
            refreshCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken,
                    _disposeCancellation.Token);
            _refreshCancellation = refreshCancellation;
            requestedGeneration = checked(++_generation);
        }
        previousCancellation?.Cancel();

        var operationEntered = false;
        try
        {
            var requestToken = refreshCancellation.Token;
            await _operationGate.WaitAsync(requestToken)
                .ConfigureAwait(false);
            operationEntered = true;
            var menu = await Task.Run(
                () => _controller.OpenFilterMenu(
                    _tableId,
                    _columnId,
                    _maximumRows,
                    _maximumDistinctValues,
                    requestToken),
                requestToken).ConfigureAwait(false);
            requestToken.ThrowIfCancellationRequested();
            if (!CompleteRefresh(
                    refreshCancellation,
                    requestedGeneration,
                    menu))
            {
                throw new OperationCanceledException(
                    "A newer Table filter-value refresh superseded this request.",
                    requestToken);
            }
        }
        catch
        {
            CompleteRefresh(
                refreshCancellation,
                requestedGeneration,
                menu: null);
            throw;
        }
        finally
        {
            if (operationEntered)
            {
                _operationGate.Release();
            }
            refreshCancellation.Dispose();
        }

        lock (_gate)
        {
            if (_disposed ||
                requestedGeneration != _generation ||
                _menu is null)
            {
                throw new OperationCanceledException(
                    "A newer Table filter-value refresh superseded this request.");
            }
        }
        Refreshed?.Invoke(this, EventArgs.Empty);
        return requestedGeneration;
    }

    public async Task<SpreadsheetTableFilterPagedResult> GetPageAsync(
        string? searchText,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        try
        {
            operationToken.ThrowIfCancellationRequested();
            var (menu, generation) = GetReadyMenu();
            return await Task.Run(
                () =>
                {
                    operationToken.ThrowIfCancellationRequested();
                    menu.SetSearchText(searchText);
                    var page = menu.CapturePage(
                        offset,
                        pageSize,
                        operationToken);
                    return new SpreadsheetTableFilterPagedResult(
                        generation,
                        page);
                },
                operationToken).ConfigureAwait(false);
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
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        try
        {
            operationToken.ThrowIfCancellationRequested();
            GetReadyMenu(generation).SetSelected(value, selected);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SelectAllVisibleAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        await ChangeVisibleSelectionAsync(
            generation,
            searchText,
            select: true,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearVisibleSelectionAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        await ChangeVisibleSelectionAsync(
            generation,
            searchText,
            select: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        long generation,
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken).ConfigureAwait(false);
        try
        {
            operationToken.ThrowIfCancellationRequested();
            return GetReadyMenu(generation).CaptureDatePage(
                generation,
                parent,
                offset,
                pageSize,
                operationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<long> ApplyValueSelectionAsync(
        long generation,
        CancellationToken cancellationToken = default)
    {
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        long invalidatedGeneration;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            invalidatedGeneration = ExecuteMutation(
                generation,
                static menu => menu.ApplyValueSelection());
        }
        finally
        {
            _operationGate.Release();
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    public async Task<long> ApplyCustomFilterAsync(
        long generation,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        long invalidatedGeneration;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            invalidatedGeneration = ExecuteMutation(
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
        Invalidated?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    public async Task<long> ApplyRichFilterAsync(
        long generation,
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken).ConfigureAwait(false);
        long invalidatedGeneration;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            invalidatedGeneration = ExecuteMutation(
                generation,
                menu => menu.ApplyRichFilter(criterion));
        }
        finally
        {
            _operationGate.Release();
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    public async Task<long> ClearColumnFilterAsync(
        long generation,
        CancellationToken cancellationToken = default)
    {
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        long invalidatedGeneration;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            invalidatedGeneration = ExecuteMutation(
                generation,
                static menu => menu.ClearColumnFilter());
        }
        finally
        {
            _operationGate.Release();
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    public void Dispose()
    {
        CancellationTokenSource? refreshCancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _menu = null;
            refreshCancellation = _refreshCancellation;
            _refreshCancellation = null;
        }
        _disposeCancellation.Cancel();
        refreshCancellation?.Cancel();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _operationGate.WaitAsync().ConfigureAwait(false);
        _operationGate.Release();
    }

    private async Task ChangeVisibleSelectionAsync(
        long generation,
        string? searchText,
        bool select,
        CancellationToken cancellationToken)
    {
        using var operationCancellation =
            CreateOperationCancellation(cancellationToken);
        var operationToken = operationCancellation.Token;
        await _operationGate.WaitAsync(operationToken)
            .ConfigureAwait(false);
        try
        {
            operationToken.ThrowIfCancellationRequested();
            var menu = GetReadyMenu(generation);
            menu.SetSearchText(searchText);
            if (select)
            {
                menu.SelectAllVisible(operationToken);
            }
            else
            {
                menu.ClearVisibleSelection(operationToken);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private (SpreadsheetTableFilterMenu Menu, long Generation)
        GetReadyMenu()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return (
                _menu ?? throw new InvalidOperationException(
                    "Refresh the paged Table filter session before requesting a page."),
                _generation);
        }
    }

    private SpreadsheetTableFilterMenu GetReadyMenu(long generation)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (generation != _generation)
            {
                throw new InvalidOperationException(
                    "The Table filter page belongs to a stale generation.");
            }

            return _menu ?? throw new InvalidOperationException(
                "Refresh the paged Table filter session before changing selection.");
        }
    }

    private long ExecuteMutation(
        long generation,
        Action<SpreadsheetTableFilterMenu> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        long invalidatedGeneration;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (generation != _generation || _menu is null)
            {
                throw new InvalidOperationException(
                    "The Table filter mutation belongs to a stale generation.");
            }

            mutation(_menu);
            _menu = null;
            invalidatedGeneration = checked(++_generation);
        }

        return invalidatedGeneration;
    }

    private bool CompleteRefresh(
        CancellationTokenSource refreshCancellation,
        long requestedGeneration,
        SpreadsheetTableFilterMenu? menu)
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
        return published;
    }

    private CancellationTokenSource CreateOperationCancellation(
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCancellation.Token);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
