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
        Groups = (groups ?? throw new ArgumentNullException(nameof(groups))).ToArray();
        Order = order;
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonGroupDefinition> Groups { get; }

    public int Order { get; }
}
