namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonGroupDefinition
{
    public RibbonGroupDefinition(string id, string caption, IEnumerable<RibbonItemDefinition> items)
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
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonItemDefinition> Items { get; }
}
