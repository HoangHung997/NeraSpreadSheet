namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonTabDefinition
{
    public RibbonTabDefinition(
        string id,
        string caption,
        IEnumerable<RibbonGroupDefinition> groups,
        int order = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A ribbon tab id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A ribbon tab caption is required.", nameof(caption));
        }

        Id = id.Trim();
        Caption = caption.Trim();
        Groups = (groups ?? throw new ArgumentNullException(nameof(groups)))
            .OrderBy(group => group.Order)
            .ToArray();
        Order = order;

        string[] duplicates = Groups
            .GroupBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate ribbon group id(s) in tab '{Id}': {string.Join(", ", duplicates)}.");
        }
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonGroupDefinition> Groups { get; }

    public int Order { get; }
}
