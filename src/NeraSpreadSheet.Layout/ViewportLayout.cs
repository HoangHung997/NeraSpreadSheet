using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout;

public readonly record struct ViewportRequest(
    double ScrollX,
    double ScrollY,
    SizeD ViewportSize,
    double Overscan = 128d);

public sealed record ViewportLayout(
    double ScrollX,
    double ScrollY,
    SizeD ViewportSize,
    double ContentWidth,
    double ContentHeight,
    IReadOnlyList<AxisSlot> Rows,
    IReadOnlyList<AxisSlot> Columns)
{
    public long VisibleCellCount => (long)Rows.Count * Columns.Count;
}

public sealed class ViewportLayoutEngine
{
    private readonly SparseAxisMetricIndex _rows;
    private readonly SparseAxisMetricIndex _columns;

    public ViewportLayoutEngine(SparseAxisMetricIndex rows, SparseAxisMetricIndex columns)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        _columns = columns ?? throw new ArgumentNullException(nameof(columns));
    }

    public ViewportLayout Compute(ViewportRequest request)
    {
        if (!double.IsFinite(request.ScrollX) || request.ScrollX < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "ScrollX must be finite and non-negative.");
        }

        if (!double.IsFinite(request.ScrollY) || request.ScrollY < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "ScrollY must be finite and non-negative.");
        }

        if (!double.IsFinite(request.Overscan) || request.Overscan < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Overscan must be finite and non-negative.");
        }

        var maximumScrollX = Math.Max(0d, _columns.TotalExtent - request.ViewportSize.Width);
        var maximumScrollY = Math.Max(0d, _rows.TotalExtent - request.ViewportSize.Height);
        var scrollX = Math.Min(request.ScrollX, maximumScrollX);
        var scrollY = Math.Min(request.ScrollY, maximumScrollY);

        return new ViewportLayout(
            scrollX,
            scrollY,
            request.ViewportSize,
            _columns.TotalExtent,
            _rows.TotalExtent,
            _rows.GetSlots(scrollY, request.ViewportSize.Height, request.Overscan),
            _columns.GetSlots(scrollX, request.ViewportSize.Width, request.Overscan));
    }
}
