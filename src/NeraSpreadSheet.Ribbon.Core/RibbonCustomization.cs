using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Defines host-neutral visibility, order and item-size overrides for a ribbon.
/// Unknown target ids are ignored so one customization can survive optional modules.
/// </summary>
public sealed class RibbonCustomization
{
    public RibbonCustomization(IEnumerable<RibbonTabCustomization> tabs)
    {
        Tabs = CustomizationValidation.MaterializeUnique(
            tabs,
            static tab => tab.TabId,
            "ribbon tab customization");
    }

    public IReadOnlyList<RibbonTabCustomization> Tabs { get; }

    /// <summary>
    /// Applies the overrides without mutating the source definition.
    /// </summary>
    public RibbonDefinition ApplyTo(RibbonDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tabOverrides = Tabs.ToDictionary(
            tab => tab.TabId,
            StringComparer.OrdinalIgnoreCase);
        var tabs = new List<RibbonTabDefinition>(definition.Tabs.Count);

        foreach (var tab in definition.Tabs)
        {
            tabOverrides.TryGetValue(tab.Id, out var tabOverride);
            if (tabOverride is { IsVisible: false })
            {
                continue;
            }

            tabs.Add(ApplyTab(tab, tabOverride));
        }

        var retainedTabIds = tabs.Select(static tab => tab.Id).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        return new RibbonDefinition(
            tabs,
            definition.ContextualTabs.Where(rule => retainedTabIds.Contains(rule.TabId)),
            definition.QuickAccessToolbar,
            definition.Backstage);
    }

    private static RibbonTabDefinition ApplyTab(
        RibbonTabDefinition tab,
        RibbonTabCustomization? customization)
    {
        var groupOverrides = (customization?.Groups ?? [])
            .ToDictionary(
                group => group.GroupId,
                StringComparer.OrdinalIgnoreCase);
        var groups = new List<RibbonGroupDefinition>(tab.Groups.Count);

        foreach (var group in tab.Groups)
        {
            groupOverrides.TryGetValue(group.Id, out var groupOverride);
            if (groupOverride is { IsVisible: false })
            {
                continue;
            }

            groups.Add(ApplyGroup(group, groupOverride));
        }

        return new RibbonTabDefinition(
            tab.Id,
            tab.Caption,
            groups,
            customization?.Order ?? tab.Order);
    }

    private static RibbonGroupDefinition ApplyGroup(
        RibbonGroupDefinition group,
        RibbonGroupCustomization? customization)
    {
        var itemOverrides = (customization?.Items ?? [])
            .ToDictionary(
                item => item.CommandId.Value,
                StringComparer.OrdinalIgnoreCase);
        var items = new List<RibbonItemDefinition>(group.Items.Count);

        foreach (var item in group.Items)
        {
            itemOverrides.TryGetValue(item.CommandId.Value, out var itemOverride);
            if (itemOverride is { IsVisible: false })
            {
                continue;
            }

            items.Add(item.WithLayout(
                itemOverride?.IsLarge ?? item.IsLarge,
                itemOverride?.Order ?? item.Order));
        }

        return new RibbonGroupDefinition(
            group.Id,
            group.Caption,
            items,
            customization?.Order ?? group.Order,
            group.CollapsePriority);
    }
}

public sealed class RibbonTabCustomization
{
    /// <summary>
    /// Creates overrides scoped to one ribbon tab id.
    /// </summary>
    public RibbonTabCustomization(
        string tabId,
        bool isVisible = true,
        int? order = null,
        IEnumerable<RibbonGroupCustomization>? groups = null)
    {
        TabId = CustomizationValidation.RequiredId(tabId, nameof(tabId));
        IsVisible = isVisible;
        Order = order;
        Groups = CustomizationValidation.MaterializeUnique(
            groups ?? [],
            static group => group.GroupId,
            $"ribbon group customization in tab '{TabId}'");
    }

    public string TabId { get; }

    public bool IsVisible { get; }

    public int? Order { get; }

    public IReadOnlyList<RibbonGroupCustomization> Groups { get; }
}

public sealed class RibbonGroupCustomization
{
    /// <summary>
    /// Creates overrides scoped to one ribbon group id within its containing tab.
    /// </summary>
    public RibbonGroupCustomization(
        string groupId,
        bool isVisible = true,
        int? order = null,
        IEnumerable<RibbonItemCustomization>? items = null)
    {
        GroupId = CustomizationValidation.RequiredId(groupId, nameof(groupId));
        IsVisible = isVisible;
        Order = order;
        Items = CustomizationValidation.MaterializeUnique(
            items ?? [],
            static item => item.CommandId.Value,
            $"ribbon item customization in group '{GroupId}'");
    }

    public string GroupId { get; }

    public bool IsVisible { get; }

    public int? Order { get; }

    public IReadOnlyList<RibbonItemCustomization> Items { get; }
}

/// <summary>
/// Defines visibility, order and size overrides for one command within a group.
/// </summary>
public sealed record RibbonItemCustomization(
    CommandId CommandId,
    bool IsVisible = true,
    int? Order = null,
    bool? IsLarge = null);

internal static class CustomizationValidation
{
    public static string RequiredId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A customization target id is required.", parameterName);
        }

        return value.Trim();
    }

    public static IReadOnlyList<T> MaterializeUnique<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materialized = values.ToArray();
        string[] duplicates = materialized
            .GroupBy(idSelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {scope} id(s): {string.Join(", ", duplicates)}.");
        }

        return materialized;
    }
}
