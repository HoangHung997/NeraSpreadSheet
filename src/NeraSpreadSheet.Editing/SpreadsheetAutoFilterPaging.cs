using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetAutoFilterOwnerKind
{
    Table,
    Worksheet,
}

public readonly record struct SpreadsheetAutoFilterTarget
{
    public SpreadsheetAutoFilterTarget(
        SpreadsheetAutoFilterOwnerKind ownerKind,
        CellRange filterRange,
        int columnOffset,
        int worksheetColumnIndex,
        CellAddress headerCell,
        string ownerName,
        string columnName,
        bool isFiltered,
        Guid? tableId = null,
        Guid? tableColumnId = null)
    {
        if (!Enum.IsDefined(ownerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerKind));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(columnOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(worksheetColumnIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (columnOffset >= filterRange.ColumnCount ||
            worksheetColumnIndex != checked(filterRange.Left + columnOffset) ||
            headerCell.RowIndex != filterRange.Top ||
            headerCell.ColumnIndex != worksheetColumnIndex)
        {
            throw new ArgumentException(
                "The filter target geometry is inconsistent with its range.",
                nameof(filterRange));
        }
        if (ownerKind == SpreadsheetAutoFilterOwnerKind.Table &&
            (tableId is null ||
             tableId == Guid.Empty ||
             tableColumnId is null ||
             tableColumnId == Guid.Empty))
        {
            throw new ArgumentException(
                "A Table filter target requires stable Table and column identities.",
                nameof(tableId));
        }
        if (ownerKind == SpreadsheetAutoFilterOwnerKind.Worksheet &&
            (tableId is not null || tableColumnId is not null))
        {
            throw new ArgumentException(
                "A worksheet filter target cannot contain Table identities.",
                nameof(tableId));
        }

        OwnerKind = ownerKind;
        FilterRange = filterRange;
        ColumnOffset = columnOffset;
        WorksheetColumnIndex = worksheetColumnIndex;
        HeaderCell = headerCell;
        OwnerName = ownerName.Trim();
        ColumnName = columnName.Trim();
        IsFiltered = isFiltered;
        TableId = tableId;
        TableColumnId = tableColumnId;
    }

    public SpreadsheetAutoFilterOwnerKind OwnerKind { get; }

    public Guid? TableId { get; }

    public Guid? TableColumnId { get; }

    public CellRange FilterRange { get; }

    public int ColumnOffset { get; }

    public int WorksheetColumnIndex { get; }

    public CellAddress HeaderCell { get; }

    public string OwnerName { get; }

    public string ColumnName { get; }

    public bool IsFiltered { get; }
}

public sealed record SpreadsheetAutoFilterPagedPage(
    long Generation,
    SpreadsheetAutoFilterTarget Target,
    string SearchText,
    int Offset,
    int PageSize,
    int TotalVisibleValueCount,
    bool HasPreviousPage,
    bool HasNextPage,
    bool IsSourceTruncated,
    IReadOnlyList<SpreadsheetTableFilterValueItem> Values);

public interface ISpreadsheetAutoFilterPagedSession :
    IDisposable,
    IAsyncDisposable
{
    event EventHandler? Refreshed;

    event EventHandler? Invalidated;

    SpreadsheetAutoFilterTarget Target { get; }

    long Generation { get; }

    bool IsReady { get; }

    Task<long> RefreshAsync(
        CancellationToken cancellationToken = default);

    Task<SpreadsheetAutoFilterPagedPage> GetPageAsync(
        string? searchText,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SetSelectedAsync(
        long generation,
        CellValue value,
        bool selected,
        CancellationToken cancellationToken = default);

    Task SelectAllVisibleAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default);

    Task ClearVisibleSelectionAsync(
        long generation,
        string? searchText,
        CancellationToken cancellationToken = default);

    Task<long> ApplyValueSelectionAsync(
        long generation,
        CancellationToken cancellationToken = default);

    Task<long> ApplyCustomFilterAsync(
        long generation,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        CancellationToken cancellationToken = default);

    Task<long> ClearColumnFilterAsync(
        long generation,
        CancellationToken cancellationToken = default);
}

public static class SpreadsheetAutoFilterTargetResolver
{
    public static bool TryResolveActiveAutoFilterTarget(
        this SpreadsheetSession session,
        out SpreadsheetAutoFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.TryResolveAutoFilterTarget(
            session.Selection.ActiveCell,
            out target);
    }

    public static bool TryResolveAutoFilterTarget(
        this SpreadsheetSession session,
        CellAddress address,
        out SpreadsheetAutoFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.TryResolveTableFilterTarget(
                address,
                out var tableTarget) &&
            session.ActiveWorksheet.TryGetTable(
                tableTarget.TableId,
                out var table) &&
            table is not null)
        {
            var columnOffset = checked(
                tableTarget.WorksheetColumnIndex -
                tableTarget.TableRange.Left);
            target = new SpreadsheetAutoFilterTarget(
                SpreadsheetAutoFilterOwnerKind.Table,
                tableTarget.TableRange,
                columnOffset,
                tableTarget.WorksheetColumnIndex,
                new CellAddress(
                    tableTarget.TableRange.Top,
                    tableTarget.WorksheetColumnIndex),
                tableTarget.TableName,
                tableTarget.ColumnName,
                table.AutoFilter?.Columns.Any(column =>
                    column.ColumnId == tableTarget.ColumnId) == true,
                tableTarget.TableId,
                tableTarget.ColumnId);
            return true;
        }

        if (session.TryResolveWorksheetFilterTarget(
                address,
                out var worksheetTarget))
        {
            var headerValue = session.ActiveWorksheet
                .GetCell(worksheetTarget.HeaderCell)
                .Value;
            var columnName = headerValue.IsBlank
                ? worksheetTarget.HeaderCell.ToA1()
                : headerValue.ToString();
            target = new SpreadsheetAutoFilterTarget(
                SpreadsheetAutoFilterOwnerKind.Worksheet,
                worksheetTarget.FilterRange,
                worksheetTarget.ColumnOffset,
                worksheetTarget.WorksheetColumnIndex,
                worksheetTarget.HeaderCell,
                session.ActiveWorksheet.Name,
                columnName,
                worksheetTarget.IsFiltered);
            return true;
        }

        target = default;
        return false;
    }
}

public static class SpreadsheetAutoFilterPagedSessionFactory
{
    public static ISpreadsheetAutoFilterPagedSession Create(
        SpreadsheetSession session,
        SpreadsheetAutoFilterTarget target,
        int maximumRows =
            SpreadsheetTablePresenterController.DefaultMaximumRows,
        int maximumDistinctValues =
            SpreadsheetTablePresenterController.DefaultMaximumDistinctValues)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumDistinctValues);

        return target.OwnerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table =>
                new TablePagedSessionAdapter(
                    target,
                    new SpreadsheetTableFilterPagedSession(
                        session,
                        target.TableId ??
                        throw new ArgumentException(
                            "The Table target is missing its Table identity.",
                            nameof(target)),
                        target.TableColumnId ??
                        throw new ArgumentException(
                            "The Table target is missing its column identity.",
                            nameof(target)),
                        maximumRows,
                        maximumDistinctValues)),
            SpreadsheetAutoFilterOwnerKind.Worksheet =>
                new WorksheetPagedSessionAdapter(
                    target,
                    new SpreadsheetWorksheetFilterPagedSession(
                        session,
                        target.WorksheetColumnIndex,
                        maximumRows,
                        maximumDistinctValues)),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private sealed class TablePagedSessionAdapter :
        ISpreadsheetAutoFilterPagedSession
    {
        private readonly SpreadsheetTableFilterPagedSession _inner;

        public TablePagedSessionAdapter(
            SpreadsheetAutoFilterTarget target,
            SpreadsheetTableFilterPagedSession inner)
        {
            Target = target;
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _inner.Refreshed += OnRefreshed;
            _inner.Invalidated += OnInvalidated;
        }

        public event EventHandler? Refreshed;

        public event EventHandler? Invalidated;

        public SpreadsheetAutoFilterTarget Target { get; }

        public long Generation => _inner.Generation;

        public bool IsReady => _inner.IsReady;

        public Task<long> RefreshAsync(
            CancellationToken cancellationToken = default) =>
            _inner.RefreshAsync(cancellationToken);

        public async Task<SpreadsheetAutoFilterPagedPage> GetPageAsync(
            string? searchText,
            int offset,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var result = await _inner.GetPageAsync(
                searchText,
                offset,
                pageSize,
                cancellationToken).ConfigureAwait(false);
            return new SpreadsheetAutoFilterPagedPage(
                result.Generation,
                Target,
                result.Page.SearchText,
                result.Page.Offset,
                result.Page.PageSize,
                result.Page.TotalVisibleValueCount,
                result.Page.HasPreviousPage,
                result.Page.HasNextPage,
                result.Page.IsSourceTruncated,
                result.Page.Values);
        }

        public Task SetSelectedAsync(
            long generation,
            CellValue value,
            bool selected,
            CancellationToken cancellationToken = default) =>
            _inner.SetSelectedAsync(
                generation,
                value,
                selected,
                cancellationToken);

        public Task SelectAllVisibleAsync(
            long generation,
            string? searchText,
            CancellationToken cancellationToken = default) =>
            _inner.SelectAllVisibleAsync(
                generation,
                searchText,
                cancellationToken);

        public Task ClearVisibleSelectionAsync(
            long generation,
            string? searchText,
            CancellationToken cancellationToken = default) =>
            _inner.ClearVisibleSelectionAsync(
                generation,
                searchText,
                cancellationToken);

        public Task<long> ApplyValueSelectionAsync(
            long generation,
            CancellationToken cancellationToken = default) =>
            _inner.ApplyValueSelectionAsync(
                generation,
                cancellationToken);

        public Task<long> ApplyCustomFilterAsync(
            long generation,
            TableFilterCondition firstCondition,
            TableFilterCondition? secondCondition = null,
            bool combineWithAnd = true,
            CancellationToken cancellationToken = default) =>
            _inner.ApplyCustomFilterAsync(
                generation,
                firstCondition,
                secondCondition,
                combineWithAnd,
                cancellationToken);

        public Task<long> ClearColumnFilterAsync(
            long generation,
            CancellationToken cancellationToken = default) =>
            _inner.ClearColumnFilterAsync(
                generation,
                cancellationToken);

        public void Dispose()
        {
            _inner.Refreshed -= OnRefreshed;
            _inner.Invalidated -= OnInvalidated;
            _inner.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            _inner.Refreshed -= OnRefreshed;
            _inner.Invalidated -= OnInvalidated;
            await _inner.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void OnRefreshed(object? sender, EventArgs e) =>
            Refreshed?.Invoke(this, e);

        private void OnInvalidated(object? sender, EventArgs e) =>
            Invalidated?.Invoke(this, e);
    }

    private sealed class WorksheetPagedSessionAdapter :
        ISpreadsheetAutoFilterPagedSession
    {
        private readonly SpreadsheetWorksheetFilterPagedSession _inner;

        public WorksheetPagedSessionAdapter(
            SpreadsheetAutoFilterTarget target,
            SpreadsheetWorksheetFilterPagedSession inner)
        {
            Target = target;
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _inner.Refreshed += OnRefreshed;
            _inner.Invalidated += OnInvalidated;
        }

        public event EventHandler? Refreshed;

        public event EventHandler? Invalidated;

        public SpreadsheetAutoFilterTarget Target { get; }

        public long Generation => _inner.Generation;

        public bool IsReady => _inner.IsReady;

        public Task<long> RefreshAsync(
            CancellationToken cancellationToken = default) =>
            _inner.RefreshAsync(cancellationToken);

        public async Task<SpreadsheetAutoFilterPagedPage> GetPageAsync(
            string? searchText,
            int offset,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var result = await _inner.GetPageAsync(
                searchText,
                offset,
                pageSize,
                cancellationToken).ConfigureAwait(false);
            return new SpreadsheetAutoFilterPagedPage(
                result.Generation,
                Target,
                result.Page.SearchText,
                result.Page.Offset,
                result.Page.PageSize,
                result.Page.TotalVisibleValueCount,
                result.Page.HasPreviousPage,
                result.Page.HasNextPage,
                result.Page.IsSourceTruncated,
                result.Page.Values);
        }

        public Task SetSelectedAsync(
            long generation,
            CellValue value,
            bool selected,
            CancellationToken cancellationToken = default) =>
            _inner.SetSelectedAsync(
                generation,
                value,
                selected,
                cancellationToken);

        public Task SelectAllVisibleAsync(
            long generation,
            string? searchText,
            CancellationToken cancellationToken = default) =>
            _inner.SelectAllVisibleAsync(
                generation,
                searchText,
                cancellationToken);

        public Task ClearVisibleSelectionAsync(
            long generation,
            string? searchText,
            CancellationToken cancellationToken = default) =>
            _inner.ClearVisibleSelectionAsync(
                generation,
                searchText,
                cancellationToken);

        public Task<long> ApplyValueSelectionAsync(
            long generation,
            CancellationToken cancellationToken = default) =>
            _inner.ApplyValueSelectionAsync(
                generation,
                cancellationToken);

        public Task<long> ApplyCustomFilterAsync(
            long generation,
            TableFilterCondition firstCondition,
            TableFilterCondition? secondCondition = null,
            bool combineWithAnd = true,
            CancellationToken cancellationToken = default) =>
            _inner.ApplyCustomFilterAsync(
                generation,
                firstCondition,
                secondCondition,
                combineWithAnd,
                cancellationToken);

        public Task<long> ClearColumnFilterAsync(
            long generation,
            CancellationToken cancellationToken = default) =>
            _inner.ClearColumnFilterAsync(
                generation,
                cancellationToken);

        public void Dispose()
        {
            _inner.Refreshed -= OnRefreshed;
            _inner.Invalidated -= OnInvalidated;
            _inner.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            _inner.Refreshed -= OnRefreshed;
            _inner.Invalidated -= OnInvalidated;
            await _inner.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void OnRefreshed(object? sender, EventArgs e) =>
            Refreshed?.Invoke(this, e);

        private void OnInvalidated(object? sender, EventArgs e) =>
            Invalidated?.Invoke(this, e);
    }
}
