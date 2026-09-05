using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout;

public readonly record struct ViewportRequest(
    double ScrollX,
    double ScrollY,
    SizeD ViewportSize,
    double Overscan = 128d,
    int FrozenRows = 0,
    int FrozenColumns = 0);

public sealed record ViewportLayout(
    double ScrollX,
    double ScrollY,
    SizeD ViewportSize,
    double ContentWidth,
    double ContentHeight,
    double FrozenWidth,
    double FrozenHeight,
    IReadOnlyList<AxisSlot> Rows,
    IReadOnlyList<AxisSlot> Columns)
{
    public long VisibleCellCount => (long)Rows.Count * Columns.Count;
    public bool HasFrozenPanes => FrozenWidth > 0d || FrozenHeight > 0d;
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
        if (request.FrozenRows < 0 || request.FrozenRows > _rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "FrozenRows must be within the row axis.");
        }
        if (request.FrozenColumns < 0 || request.FrozenColumns > _columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "FrozenColumns must be within the column axis.");
        }

        var frozenHeight = _rows.GetOffset(request.FrozenRows);
        var frozenWidth = _columns.GetOffset(request.FrozenColumns);
        var scrollViewportWidth = Math.Max(0d, request.ViewportSize.Width - frozenWidth);
        var scrollViewportHeight = Math.Max(0d, request.ViewportSize.Height - frozenHeight);
        var maximumScrollX = scrollViewportWidth <= 0d
            ? 0d
            : Math.Max(0d, (_columns.TotalExtent - frozenWidth) - scrollViewportWidth);
        var maximumScrollY = scrollViewportHeight <= 0d
            ? 0d
            : Math.Max(0d, (_rows.TotalExtent - frozenHeight) - scrollViewportHeight);
        var scrollX = Math.Min(request.ScrollX, maximumScrollX);
        var scrollY = Math.Min(request.ScrollY, maximumScrollY);

        return new ViewportLayout(
            scrollX,
            scrollY,
            request.ViewportSize,
            _columns.TotalExtent,
            _rows.TotalExtent,
            Math.Min(frozenWidth, request.ViewportSize.Width),
            Math.Min(frozenHeight, request.ViewportSize.Height),
            BuildSlots(_rows, request.FrozenRows, frozenHeight, scrollY, request.ViewportSize.Height, request.Overscan),
            BuildSlots(_columns, request.FrozenColumns, frozenWidth, scrollX, request.ViewportSize.Width, request.Overscan));
    }

    private static List<AxisSlot> BuildSlots(
        SparseAxisMetricIndex index,
        int frozenCount,
        double frozenExtent,
        double scrollOffset,
        double viewportExtent,
        double overscan)
    {
        var slots = new List<AxisSlot>();
        if (frozenCount > 0 && frozenExtent > 0d)
        {
            foreach (var slot in index.GetSlots(0d, Math.Min(frozenExtent, viewportExtent), 0d))
            {
                if (slot.Index >= frozenCount)
                {
                    break;
                }
                slots.Add(new AxisSlot(slot.Index, slot.Start, slot.Size, IsFrozen: true));
            }
        }

        var scrollViewportExtent = Math.Max(0d, viewportExtent - frozenExtent);
        if (scrollViewportExtent <= 0d || frozenCount >= index.Count)
        {
            return slots;
        }

        var documentOffset = frozenExtent + scrollOffset;
        foreach (var slot in index.GetSlots(documentOffset, scrollViewportExtent, overscan))
        {
            if (slot.Index < frozenCount)
            {
                continue;
            }
            slots.Add(new AxisSlot(
                slot.Index,
                slot.Start + frozenExtent,
                slot.Size,
                IsFrozen: false));
        }
        return slots;
    }
}
