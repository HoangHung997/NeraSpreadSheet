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
    private readonly SemaphoreSlim _pageGate = new(1, 1);
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

        SpreadsheetTableFilterMenu menu;
        try
        {
            menu = await Task.Run(
                () => _controller.OpenFilterMenu(
                    _tableId,
                    _columnId,
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
                "A newer Table filter-value refresh superseded this request.");
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
        await _pageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SpreadsheetTableFilterMenu menu;
            long generation;
            lock (_gate)
            {
                ThrowIfDisposed();
                menu = _menu
                    ?? throw new InvalidOperationException(
                        "Refresh the paged Table filter session before requesting a page.");
                generation = _generation;
            }

            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    menu.SetSearchText(searchText);
                    var page = menu.CapturePage(
                        offset,
                        pageSize,
                        cancellationToken);
                    return new SpreadsheetTableFilterPagedResult(
                        generation,
                        page);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pageGate.Release();
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
        _pageGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
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
        refreshCancellation.Dispose();
        return published;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
