namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonGroupDefinition
{
    public RibbonGroupDefinition(
        string id,
        string caption,
        IEnumerable<RibbonItemDefinition> items,
        int order = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A ribbon group id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A ribbon group caption is required.", nameof(caption));
        }

        Id = id.Trim();
        Caption = caption.Trim();
        Items = (items ?? throw new ArgumentNullException(nameof(items)))
            .OrderBy(item => item.Order)
            .ToArray();
        Order = order;

        string[] duplicates = Items
            .GroupBy(item => item.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate ribbon command id(s) in group '{Id}': {string.Join(", ", duplicates)}.");
        }
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonItemDefinition> Items { get; }

    /// <summary>
    /// Gets the stable sort order used within the containing tab.
    /// </summary>
    public int Order { get; }
}
