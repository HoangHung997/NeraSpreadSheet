namespace NeraSpreadSheet.Bars.Core;

public sealed class BarDefinition
{
    public BarDefinition(
        string id,
        BarKind kind,
        IEnumerable<BarItemDefinition> items,
        string? caption = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A bar id is required.", nameof(id));
        }

        Id = id.Trim();
        Kind = kind;
        Caption = caption;
        Items = (items ?? throw new ArgumentNullException(nameof(items))).ToArray();
    }

    public string Id { get; }

    public BarKind Kind { get; }

    public string? Caption { get; }

    public IReadOnlyList<BarItemDefinition> Items { get; }
}
