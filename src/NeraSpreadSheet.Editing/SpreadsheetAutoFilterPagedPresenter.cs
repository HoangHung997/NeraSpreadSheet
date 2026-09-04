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
    int ResultRowCount,
    bool IsResultCountTruncated,
    bool HasPreviousPage,
    bool HasNextPage,
    IReadOnlyList<SpreadsheetAutoFilterMenuKind> MenuKinds,
    IReadOnlyList<SpreadsheetTableFilterValueItem> Values)
{
    /// <summary>Creates a snapshot using the pre-FILTER-007 result shape.</summary>
    public SpreadsheetAutoFilterPagedPresenterSnapshot(
        SpreadsheetAutoFilterTarget target,
        long generation,
        string searchText,
        int pageOffset,
        int pageSize,
        int totalItemCount,
        bool isInitialized,
        bool isSourceTruncated,
        bool hasPreviousPage,
        bool hasNextPage,
        IReadOnlyList<SpreadsheetAutoFilterMenuKind> menuKinds,
        IReadOnlyList<SpreadsheetTableFilterValueItem> values)
        : this(
            target,
            generation,
            searchText,
            pageOffset,
            pageSize,
            totalItemCount,
            isInitialized,
            isSourceTruncated,
            0,
            false,
            hasPreviousPage,
            hasNextPage,
            menuKinds,
            values)
    {
    }

    /// <summary>Deconstructs the snapshot using the pre-FILTER-007 result shape.</summary>
    public void Deconstruct(
        out SpreadsheetAutoFilterTarget target,
        out long generation,
        out string searchText,
        out int pageOffset,
        out int pageSize,
        out int totalItemCount,
        out bool isInitialized,
        out bool isSourceTruncated,
        out bool hasPreviousPage,
        out bool hasNextPage,
        out IReadOnlyList<SpreadsheetAutoFilterMenuKind> menuKinds,
        out IReadOnlyList<SpreadsheetTableFilterValueItem> values)
    {
        target = Target;
        generation = Generation;
        searchText = SearchText;
        pageOffset = PageOffset;
        pageSize = PageSize;
        totalItemCount = TotalItemCount;
        isInitialized = IsInitialized;
        isSourceTruncated = IsSourceTruncated;
        hasPreviousPage = HasPreviousPage;
        hasNextPage = HasNextPage;
        menuKinds = MenuKinds;
        values = Values;
    }

    public string AccessibilityAnnouncement
    {
        get
        {
            var state = Target.HeaderState switch
            {
                SpreadsheetFilterHeaderState.Filtered => "đang lọc",
                SpreadsheetFilterHeaderState.Sorted => Target.SortDescending == true
                    ? "đang sắp xếp giảm dần"
                    : "đang sắp xếp tăng dần",
                SpreadsheetFilterHeaderState.FilteredAndSorted => Target.SortDescending == true
                    ? "đang lọc và sắp xếp giảm dần"
                    : "đang lọc và sắp xếp tăng dần",
                _ => "chưa lọc hoặc sắp xếp",
            };
            var count = IsResultCountTruncated
                ? $"ít nhất {ResultRowCount:N0} kết quả"
                : $"{ResultRowCount:N0} kết quả";
            return $"Cột {Target.ColumnName} trong {Target.OwnerName}, {state}, {count}.";
        }
    }
}

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
    private readonly SpreadsheetSortController? _sortController;
    private readonly SpreadsheetSession? _session;
    private readonly int _maximumRows;
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
        _sortController = session.Sort;
        _session = session;
        _maximumRows = maximumRows;
    }

    public SpreadsheetAutoFilterPagedPresenter(
        SpreadsheetAutoFilterPagedView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _maximumRows = SpreadsheetTablePresenterController.DefaultMaximumRows;
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
            var resultCount = await CountVisibleRowsAsync(cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = page;
                (_resultRowCount, _isResultCountTruncated) = resultCount;
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
            var resultCount = await CountVisibleRowsAsync(cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = page;
                (_resultRowCount, _isResultCountTruncated) = resultCount;
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

    /// <summary>Physically sorts the current AutoFilter owner using one or more ordered keys.</summary>
    public Task<bool> ApplySortAsync(
        SpreadsheetFilterSortState sortState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sortState);
        return ExecuteSortMutationAsync(
            controller => controller.SortAutoFilter(
                ResolveCurrentTarget(),
                sortState),
            cancellationToken);
    }

    /// <summary>Sorts the target column in ascending or descending order.</summary>
    public Task<bool> ApplyColumnSortAsync(
        bool descending,
        string? customList = null,
        CancellationToken cancellationToken = default) =>
        ApplyColumnSortCoreAsync(descending, customList, cancellationToken);

    /// <summary>Reapplies the current owner sort after resolving its latest structural identity.</summary>
    public Task<bool> ReapplyAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteSortMutationAsync(
            controller => controller.ReapplyAutoFilter(ResolveCurrentTarget()),
            cancellationToken);

    /// <summary>Clears sort metadata without attempting to reverse the physical row order.</summary>
    public Task<bool> ClearSortAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteSortMutationAsync(
            controller => controller.ClearAutoFilterSort(ResolveCurrentTarget()),
            cancellationToken);

    private Task<bool> ApplyColumnSortCoreAsync(
        bool descending,
        string? customList,
        CancellationToken cancellationToken) =>
        ExecuteSortMutationAsync(
            controller =>
            {
                var currentTarget = ResolveCurrentTarget();
                return controller.SortAutoFilter(
                    currentTarget,
                    new SpreadsheetFilterSortState([
                        new SpreadsheetFilterSortCondition(
                            currentTarget.ColumnOffset,
                            descending,
                            customList: customList),
                    ]));
            },
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
            _page = null;
        }
        _view.Dispose();
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
        await _operationGate.WaitAsync().ConfigureAwait(false);
        _operationGate.Release();
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
            var resultCount = await CountVisibleRowsAsync(cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = null;
                (_resultRowCount, _isResultCountTruncated) = resultCount;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return generation;
    }

    private async Task<bool> ExecuteSortMutationAsync(
        Func<SpreadsheetSortController, bool> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool changed;
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var controller = _sortController ?? throw new InvalidOperationException(
                "This presenter was created from a detached paged view and cannot mutate sort state.");
            changed = mutation(controller);
            var resultCount = await CountVisibleRowsAsync(cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                _page = null;
                (_resultRowCount, _isResultCountTruncated) = resultCount;
            }
        }
        finally
        {
            _operationGate.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    private SpreadsheetAutoFilterPagedPresenterSnapshot
        CreateSnapshotUnsafe()
    {
        var currentTarget = ResolveCurrentTarget();
        if (_page is null)
        {
            var viewSnapshot = _view.Capture();
            return new SpreadsheetAutoFilterPagedPresenterSnapshot(
                currentTarget,
                viewSnapshot.Generation,
                viewSnapshot.SearchText,
                0,
                _view.PageSize,
                viewSnapshot.TotalItemCount,
                false,
                viewSnapshot.IsSourceTruncated,
                _resultRowCount,
                _isResultCountTruncated,
                false,
                false,
                viewSnapshot.MenuKinds,
                []);
        }

        return new SpreadsheetAutoFilterPagedPresenterSnapshot(
            currentTarget,
            _page.Generation,
            _page.SearchText,
            _page.Offset,
            _page.PageSize,
            _page.TotalVisibleValueCount,
            true,
            _page.IsSourceTruncated,
            _resultRowCount,
            _isResultCountTruncated,
            _page.HasPreviousPage,
            _page.HasNextPage,
            _page.MenuKinds,
            _page.Values);
    }

    private SpreadsheetAutoFilterTarget ResolveCurrentTarget()
    {
        if (_session is null)
        {
            return Target;
        }
        if (Target.OwnerKind == SpreadsheetAutoFilterOwnerKind.Table &&
            Target.TableId is Guid tableId &&
            Target.TableColumnId is Guid columnId &&
            _session.ActiveWorksheet.TryGetTable(tableId, out var table) &&
            table is not null &&
            table.TryGetColumn(columnId, out _))
        {
            var columnOffset = table.GetColumnIndex(columnId);
            var address = new CellAddress(
                table.Range.Top,
                table.Range.Left + columnOffset);
            if (_session.TryResolveAutoFilterTarget(address, out var refreshed))
            {
                return refreshed;
            }
        }
        if (Target.OwnerKind == SpreadsheetAutoFilterOwnerKind.Worksheet &&
            _session.ActiveWorksheet.AutoFilter is { } filter)
        {
            var offset = Math.Min(Target.ColumnOffset, filter.Range.ColumnCount - 1);
            var address = new CellAddress(filter.Range.Top, filter.Range.Left + offset);
            if (_session.TryResolveAutoFilterTarget(address, out var refreshed))
            {
                return refreshed;
            }
        }
        return Target;
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

    private int _resultRowCount;
    private bool _isResultCountTruncated;

    private Task<(int Count, bool Truncated)> CountVisibleRowsAsync(
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return Task.FromResult((0, false));
        }
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheet = _session.ActiveWorksheet;
            CellRange? dataRange = Target.OwnerKind switch
            {
                SpreadsheetAutoFilterOwnerKind.Table when
                    Target.TableId is Guid tableId &&
                    worksheet.TryGetTable(tableId, out var table) => table?.DataRange,
                SpreadsheetAutoFilterOwnerKind.Worksheet => worksheet.AutoFilter?.DataRange,
                _ => null,
            };
            if (dataRange is not { } range)
            {
                return (0, false);
            }
            var snapshot = WorksheetSnapshot.Capture(worksheet);
            var limit = Math.Min(range.RowCount, _maximumRows);
            var visible = 0;
            for (var offset = 0; offset < limit; offset++)
            {
                if ((offset & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (snapshot.IsRowVisible(range.Top + offset)) visible++;
            }
            return (visible, range.RowCount > limit);
        }, cancellationToken);
    }
}
