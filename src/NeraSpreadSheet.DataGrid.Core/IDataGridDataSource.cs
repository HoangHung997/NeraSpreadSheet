namespace NeraSpreadSheet.DataGrid.Core;

public interface IDataGridDataSource
{
    ValueTask<long> GetRowCountAsync(CancellationToken cancellationToken = default);

    ValueTask<object?> GetValueAsync(
        long rowIndex,
        string columnKey,
        CancellationToken cancellationToken = default);

    ValueTask SetValueAsync(
        long rowIndex,
        string columnKey,
        object? value,
        CancellationToken cancellationToken = default);
}
