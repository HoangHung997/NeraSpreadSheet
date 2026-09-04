namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonGroupDefinition
{
    public RibbonGroupDefinition(
        string id,
        string caption,
        IEnumerable<RibbonItemDefinition> items,
        int order = 0)
        : this(id, caption, items, order, collapsePriority: 0)
    {
    }

    /// <summary>
    /// Creates a Ribbon group with explicit ordering and responsive collapse priority.
    /// </summary>
    /// <param name="id">Stable group identity.</param>
    /// <param name="caption">Localized group caption.</param>
    /// <param name="items">Commands in the group.</param>
    /// <param name="order">Stable order inside the containing tab.</param>
    /// <param name="collapsePriority">
    /// Relative importance of keeping the group expanded; lower values collapse first.
    /// </param>
    public RibbonGroupDefinition(
        string id,
        string caption,
        IEnumerable<RibbonItemDefinition> items,
        int order,
        int collapsePriority)
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
        CollapsePriority = collapsePriority;

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

    /// <summary>
    /// Gets the relative importance of keeping this group expanded. Groups with
    /// lower values collapse first; ties collapse from right to left.
    /// </summary>
    public int CollapsePriority { get; }
}
