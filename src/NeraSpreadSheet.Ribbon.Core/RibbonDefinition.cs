namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonDefinition
{
    public RibbonDefinition(IEnumerable<RibbonTabDefinition> tabs)
    {
        Tabs = (tabs ?? throw new ArgumentNullException(nameof(tabs)))
            .OrderBy(tab => tab.Order)
            .ToArray();

        string[] duplicates = Tabs
            .GroupBy(tab => tab.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate ribbon tab id(s): {string.Join(", ", duplicates)}.");
        }
    }

    public IReadOnlyList<RibbonTabDefinition> Tabs { get; }
}
