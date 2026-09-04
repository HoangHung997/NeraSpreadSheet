using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetAutoFilterPagedViewSnapshot(
    SpreadsheetAutoFilterTarget Target,
    long Generation,
    string SearchText,
    int PageSize,
    int TotalItemCount,
    int LoadedItemCount,
    bool IsInitialized,
    bool IsSourceTruncated,
    bool HasMoreItems,
    IReadOnlyList<SpreadsheetAutoFilterMenuKind> MenuKinds,
    IReadOnlyList<SpreadsheetTableFilterValueItem> LoadedItems);

/// <summary>
/// Platform-neutral incremental page cache for WPF, WinForms and MAUI filter
/// presenters. It serializes requests, guards generations and never owns live
/// worksheet data outside the underlying immutable filter menu snapshot.
/// </summary>
public sealed class SpreadsheetAutoFilterPagedView :
    IDisposable,
    IAsyncDisposable
{
    public const int DefaultPageSize = 100;

    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ISpreadsheetAutoFilterPagedSession _session;
    private readonly SortedDictionary<int, SpreadsheetAutoFilterPagedPage>
        _pages = [];
    private readonly int _pageSize;

    private string _searchText = string.Empty;
    private long _generation;
    private int _totalItemCount;
    private bool _isInitialized;
    private bool _isSourceTruncated;
    private IReadOnlyList<SpreadsheetAutoFilterMenuKind> _menuKinds = [];
    private bool _disposed;

    public SpreadsheetAutoFilterPagedView(
        ISpreadsheetAutoFilterPagedSession session,
        int pageSize = DefaultPageSize)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            pageSize,
            SpreadsheetTableFilterMenu.MaximumPageSize);
        _pageSize = pageSize;
        _session.Invalidated += OnSessionInvalidated;
    }

    public event EventHandler? Changed;

    public SpreadsheetAutoFilterTarget Target => _session.Target;

    public int PageSize => _pageSize;

    public SpreadsheetAutoFilterPagedViewSnapshot Capture()
    {
        lock (_stateGate)
        {
            return CaptureUnsafe();
        }
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var generation = await _session.RefreshAsync(
                cancellationToken).ConfigureAwait(false);
            var page = await _session.GetPageAsync(
                _searchText,
                0,
                _pageSize,
                cancellationToken).ConfigureAwait(false);
            EnsureGeneration(generation, page.Generation);
            lock (_stateGate)
            {
                _generation = generation;
                _pages.Clear();
                _pages.Add(page.Offset, page);
                _totalItemCount = page.TotalVisibleValueCount;
                _isSourceTruncated = page.IsSourceTruncated;
                _menuKinds = page.MenuKinds;
                _isInitialized = true;
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
        var normalized = searchText?.Trim() ?? string.Empty;
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        var changed = false;
        try
        {
            ThrowIfDisposed();
            long generation;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                if (string.Equals(
                        _searchText,
                        normalized,
                        StringComparison.Ordinal))
                {
                    return;
                }
                generation = _generation;
            }

            var page = await _session.GetPageAsync(
                normalized,
                0,
                _pageSize,
                cancellationToken).ConfigureAwait(false);
            EnsureGeneration(generation, page.Generation);
            lock (_stateGate)
            {
                _searchText = normalized;
                _pages.Clear();
                _pages.Add(page.Offset, page);
                _totalItemCount = page.TotalVisibleValueCount;
                _isSourceTruncated = page.IsSourceTruncated;
                _menuKinds = page.MenuKinds;
                changed = true;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<SpreadsheetAutoFilterPagedPage> GetPageAsync(
        int offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        SpreadsheetAutoFilterPagedPage page;
        var changed = false;
        try
        {
            ThrowIfDisposed();
            long generation;
            string searchText;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                if (_pages.TryGetValue(offset, out var cached))
                {
                    return cached;
                }
                if (offset >= _totalItemCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(offset),
                        offset,
                        "The page offset is outside the filtered value list.");
                }
                generation = _generation;
                searchText = _searchText;
            }

            page = await _session.GetPageAsync(
                searchText,
                offset,
                _pageSize,
                cancellationToken).ConfigureAwait(false);
            EnsureGeneration(generation, page.Generation);
            lock (_stateGate)
            {
                _pages[offset] = page;
                _totalItemCount = page.TotalVisibleValueCount;
                _isSourceTruncated = page.IsSourceTruncated;
                _menuKinds = page.MenuKinds;
                changed = true;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return page;
    }

    public async Task<SpreadsheetAutoFilterPagedPage?> LoadNextPageAsync(
        CancellationToken cancellationToken = default)
    {
        int? nextOffset;
        lock (_stateGate)
        {
            EnsureNotDisposedUnsafe();
            EnsureInitializedUnsafe();
            nextOffset = GetNextOffsetUnsafe();
        }
        return nextOffset is null
            ? null
            : await GetPageAsync(
                nextOffset.Value,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpreadsheetTableFilterValueItem> GetItemAsync(
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        int pageOffset;
        lock (_stateGate)
        {
            EnsureNotDisposedUnsafe();
            EnsureInitializedUnsafe();
            if (index >= _totalItemCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "The item index is outside the filtered value list.");
            }
            pageOffset = (index / _pageSize) * _pageSize;
            if (TryGetItemUnsafe(index, out var cached))
            {
                return cached;
            }
        }

        var page = await GetPageAsync(
            pageOffset,
            cancellationToken).ConfigureAwait(false);
        var pageIndex = index - page.Offset;
        if (pageIndex < 0 || pageIndex >= page.Values.Count)
        {
            throw new InvalidOperationException(
                "The filter page did not contain the requested item.");
        }
        return page.Values[pageIndex];
    }

    public bool TryGetLoadedItem(
        int index,
        out SpreadsheetTableFilterValueItem item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        lock (_stateGate)
        {
            EnsureNotDisposedUnsafe();
            return TryGetItemUnsafe(index, out item);
        }
    }

    public async Task SetSelectedAsync(
        int index,
        bool selected,
        CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            index,
            cancellationToken).ConfigureAwait(false);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            long generation;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                generation = _generation;
            }
            await _session.SetSelectedAsync(
                generation,
                item.Value,
                selected,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                ReplaceCachedSelectionUnsafe(item.Value, selected);
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task SelectAllVisibleAsync(
        CancellationToken cancellationToken = default) =>
        ChangeVisibleSelectionAsync(
            select: true,
            cancellationToken);

    public Task ClearVisibleSelectionAsync(
        CancellationToken cancellationToken = default) =>
        ChangeVisibleSelectionAsync(
            select: false,
            cancellationToken);

    public async Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            long generation;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                generation = _generation;
            }
            var page = await _session.GetDatePageAsync(
                generation,
                parent,
                offset,
                pageSize,
                cancellationToken).ConfigureAwait(false);
            EnsureGeneration(generation, page.Generation);
            return page;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<long> ApplyValueSelectionAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            (generation, token) =>
                _session.ApplyValueSelectionAsync(
                    generation,
                    token),
            cancellationToken);

    public Task<long> ApplyCustomFilterAsync(
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        return ExecuteMutationAsync(
            (generation, token) =>
                _session.ApplyCustomFilterAsync(
                    generation,
                    firstCondition,
                    secondCondition,
                    combineWithAnd,
                    token),
            cancellationToken);
    }

    public Task<long> ApplyRichFilterAsync(
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        return ExecuteMutationAsync(
            (generation, token) =>
                _session.ApplyRichFilterAsync(generation, criterion, token),
            cancellationToken);
    }

    public Task<long> ClearColumnFilterAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            (generation, token) =>
                _session.ClearColumnFilterAsync(
                    generation,
                    token),
            cancellationToken);

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _pages.Clear();
            _isInitialized = false;
        }
        _session.Invalidated -= OnSessionInvalidated;
        _session.Dispose();
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
            _pages.Clear();
            _isInitialized = false;
        }
        _session.Invalidated -= OnSessionInvalidated;
        await _session.DisposeAsync().ConfigureAwait(false);
        _operationGate.Dispose();
        GC.SuppressFinalize(this);
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
            long generation;
            string searchText;
            int totalItemCount;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                generation = _generation;
                searchText = _searchText;
                totalItemCount = _totalItemCount;
            }

            if (select)
            {
                await _session.SelectAllVisibleAsync(
                    generation,
                    searchText,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _session.ClearVisibleSelectionAsync(
                    generation,
                    searchText,
                    cancellationToken).ConfigureAwait(false);
            }

            var page = await _session.GetPageAsync(
                searchText,
                0,
                Math.Min(
                    _pageSize,
                    Math.Max(1, totalItemCount)),
                cancellationToken).ConfigureAwait(false);
            EnsureGeneration(generation, page.Generation);
            lock (_stateGate)
            {
                _pages.Clear();
                _pages.Add(page.Offset, page);
                _totalItemCount = page.TotalVisibleValueCount;
                _isSourceTruncated = page.IsSourceTruncated;
                _menuKinds = page.MenuKinds;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<long> ExecuteMutationAsync(
        Func<long, CancellationToken, Task<long>> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        long invalidatedGeneration;
        try
        {
            ThrowIfDisposed();
            long generation;
            lock (_stateGate)
            {
                EnsureInitializedUnsafe();
                generation = _generation;
            }
            invalidatedGeneration = await mutation(
                generation,
                cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                _generation = invalidatedGeneration;
                _pages.Clear();
                _totalItemCount = 0;
                _isInitialized = false;
                _isSourceTruncated = false;
                _menuKinds = [];
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return invalidatedGeneration;
    }

    private void ReplaceCachedSelectionUnsafe(
        CellValue value,
        bool selected)
    {
        foreach (var (offset, page) in _pages.ToArray())
        {
            var changed = false;
            var values = page.Values
                .Select(item =>
                {
                    if (item.Value != value ||
                        item.IsSelected == selected)
                    {
                        return item;
                    }
                    changed = true;
                    return item with
                    {
                        IsSelected = selected,
                    };
                })
                .ToArray();
            if (changed)
            {
                _pages[offset] = page with
                {
                    Values = values,
                };
            }
        }
    }

    private bool TryGetItemUnsafe(
        int index,
        out SpreadsheetTableFilterValueItem item)
    {
        var pageOffset = (index / _pageSize) * _pageSize;
        if (_pages.TryGetValue(pageOffset, out var page))
        {
            var pageIndex = index - pageOffset;
            if (pageIndex >= 0 && pageIndex < page.Values.Count)
            {
                item = page.Values[pageIndex];
                return true;
            }
        }

        item = default!;
        return false;
    }

    private int? GetNextOffsetUnsafe()
    {
        var nextOffset = 0;
        foreach (var page in _pages.Values.OrderBy(static page => page.Offset))
        {
            if (page.Offset > nextOffset)
            {
                break;
            }
            nextOffset = Math.Max(
                nextOffset,
                checked(page.Offset + page.Values.Count));
        }
        return nextOffset < _totalItemCount
            ? nextOffset
            : null;
    }

    private SpreadsheetAutoFilterPagedViewSnapshot CaptureUnsafe()
    {
        var items = new List<SpreadsheetTableFilterValueItem>();
        var nextOffset = 0;
        foreach (var page in _pages.Values.OrderBy(static page => page.Offset))
        {
            if (page.Offset != nextOffset)
            {
                break;
            }
            items.AddRange(page.Values);
            nextOffset = checked(nextOffset + page.Values.Count);
        }

        return new SpreadsheetAutoFilterPagedViewSnapshot(
            Target,
            _generation,
            _searchText,
            _pageSize,
            _totalItemCount,
            items.Count,
            _isInitialized,
            _isSourceTruncated,
            _isInitialized && items.Count < _totalItemCount,
            _menuKinds,
            items);
    }

    private void OnSessionInvalidated(object? sender, EventArgs e)
    {
        var changed = false;
        lock (_stateGate)
        {
            if (!_disposed && _isInitialized)
            {
                _generation = _session.Generation;
                _pages.Clear();
                _totalItemCount = 0;
                _isInitialized = false;
                _isSourceTruncated = false;
                _menuKinds = [];
                changed = true;
            }
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

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

    private void EnsureInitializedUnsafe()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "Initialize or refresh the filter value view before using it.");
        }
    }

    private static void EnsureGeneration(
        long expected,
        long actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                "The filter page belongs to a stale generation.");
        }
    }
}
