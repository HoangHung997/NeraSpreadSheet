namespace NeraSpreadSheet.DataGrid.Core;

public enum GridSortDirection : byte
{
    Ascending,
    Descending,
}

public readonly record struct GridSortDescriptor(
    string ColumnKey,
    GridSortDirection Direction,
    int Priority = 0);
