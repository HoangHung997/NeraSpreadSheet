namespace NeraSpreadSheet.Ribbon.Core;

public sealed class RibbonDefinition
{
    public RibbonDefinition(IEnumerable<RibbonTabDefinition> tabs)
        : this(tabs, [], [], [])
    {
    }

    /// <summary>
    /// Creates a Ribbon definition with contextual visibility, Quick Access Toolbar,
    /// backstage commands and key-tip metadata.
    /// </summary>
    public RibbonDefinition(
        IEnumerable<RibbonTabDefinition> tabs,
        IEnumerable<RibbonContextualTabRule> contextualTabs,
        IEnumerable<RibbonCommandSurfaceItem> quickAccessToolbar,
        IEnumerable<RibbonCommandSurfaceItem> backstage)
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
        ContextualTabs = MaterializeContextualTabs(contextualTabs);
        QuickAccessToolbar = MaterializeSurface(quickAccessToolbar, "Quick Access Toolbar");
        Backstage = MaterializeSurface(backstage, "backstage");

        string[] unknownContextualTabs = ContextualTabs
            .Where(rule => !Tabs.Any(tab => string.Equals(
                tab.Id,
                rule.TabId,
                StringComparison.OrdinalIgnoreCase)))
            .Select(static rule => rule.TabId)
            .ToArray();
        if (unknownContextualTabs.Length > 0)
        {
            throw new InvalidOperationException(
                $"Unknown contextual ribbon tab id(s): {string.Join(", ", unknownContextualTabs)}.");
        }

        RibbonKeyTip.ValidateScope(QuickAccessToolbar, "Quick Access Toolbar");
        RibbonKeyTip.ValidateScope(Backstage, "backstage");
    }

    public IReadOnlyList<RibbonTabDefinition> Tabs { get; }

    public IReadOnlyList<RibbonContextualTabRule> ContextualTabs { get; }

    public IReadOnlyList<RibbonCommandSurfaceItem> QuickAccessToolbar { get; }

    public IReadOnlyList<RibbonCommandSurfaceItem> Backstage { get; }

    private static RibbonContextualTabRule[] MaterializeContextualTabs(
        IEnumerable<RibbonContextualTabRule> rules)
    {
        var result = (rules ?? throw new ArgumentNullException(nameof(rules))).ToArray();
        EnsureUnique(result.Select(static rule => rule.TabId), "contextual ribbon tab");
        return result;
    }

    private static RibbonCommandSurfaceItem[] MaterializeSurface(
        IEnumerable<RibbonCommandSurfaceItem> items,
        string surfaceName)
    {
        var result = (items ?? throw new ArgumentNullException(nameof(items))).ToArray();
        EnsureUnique(result.Select(static item => item.CommandId.Value), surfaceName);
        return result;
    }

    private static void EnsureUnique(IEnumerable<string> ids, string scope)
    {
        string[] duplicates = ids.GroupBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {scope} id(s): {string.Join(", ", duplicates)}.");
        }
    }
}
