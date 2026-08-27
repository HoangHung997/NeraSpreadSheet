using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum SpreadsheetAnalyticsItemKind
{
    Chart,
    Pivot,
}

public readonly record struct SpreadsheetAnalyticsItemKey
{
    public SpreadsheetAnalyticsItemKey(
        SpreadsheetAnalyticsItemKind kind,
        Guid id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Analytics item IDs must be non-empty.",
                nameof(id));
        }

        Kind = kind;
        Id = id;
    }

    public SpreadsheetAnalyticsItemKind Kind { get; }

    public Guid Id { get; }

    public static SpreadsheetAnalyticsItemKey ForChart(Guid id) =>
        new(SpreadsheetAnalyticsItemKind.Chart, id);

    public static SpreadsheetAnalyticsItemKey ForPivot(Guid id) =>
        new(SpreadsheetAnalyticsItemKind.Pivot, id);
}

public sealed record SpreadsheetAnalyticsPlacement
{
    public SpreadsheetAnalyticsPlacement(
        SpreadsheetAnalyticsItemKey item,
        RectD documentBounds,
        int zIndex)
    {
        if (item.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Analytics placement items must have a non-empty ID.",
                nameof(item));
        }
        if (documentBounds.IsEmpty)
        {
            throw new ArgumentException(
                "Analytics placement bounds must have positive width and height.",
                nameof(documentBounds));
        }
        if (documentBounds.X < 0d || documentBounds.Y < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(documentBounds),
                "Analytics placement coordinates must be non-negative document coordinates.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(zIndex);

        Item = item;
        DocumentBounds = documentBounds;
        ZIndex = zIndex;
    }

    public SpreadsheetAnalyticsItemKey Item { get; }

    public RectD DocumentBounds { get; }

    public int ZIndex { get; }

    public SpreadsheetAnalyticsPlacement WithBounds(RectD documentBounds) =>
        new(Item, documentBounds, ZIndex);

    public SpreadsheetAnalyticsPlacement WithZIndex(int zIndex) =>
        new(Item, DocumentBounds, zIndex);
}
