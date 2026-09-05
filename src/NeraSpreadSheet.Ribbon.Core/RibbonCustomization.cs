using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Defines host-neutral visibility, order and item-size overrides for a ribbon.
/// Unknown target ids are ignored so one customization can survive optional modules.
/// </summary>
public sealed class RibbonCustomization
{
    public RibbonCustomization(IEnumerable<RibbonTabCustomization> tabs)
        : this(tabs, null)
    {
    }

    public RibbonCustomization(
        IEnumerable<RibbonTabCustomization> tabs,
        IEnumerable<RibbonQuickAccessItemCustomization>? quickAccessToolbar)
    {
        Tabs = CustomizationValidation.MaterializeUnique(
            tabs,
            static tab => tab.TabId,
            "ribbon tab customization");
        HasQuickAccessToolbarOverride = quickAccessToolbar is not null;
        QuickAccessToolbar = CustomizationValidation.MaterializeUnique(
            quickAccessToolbar ?? [],
            static item => item.CommandId.Value,
            "Quick Access Toolbar customization");
        var duplicatePlacements = Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Where(static item => item.IsPlacement)
            .GroupBy(static item => item.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicatePlacements.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate ribbon command placement id(s): {string.Join(", ", duplicatePlacements)}.");
        }
    }

    public IReadOnlyList<RibbonTabCustomization> Tabs { get; }

    public bool HasQuickAccessToolbarOverride { get; }

    public IReadOnlyList<RibbonQuickAccessItemCustomization> QuickAccessToolbar { get; }

    /// <summary>
    /// Applies the overrides without mutating the source definition.
    /// </summary>
    public RibbonDefinition ApplyTo(RibbonDefinition definition) => ApplyTo(definition, null);

    /// <summary>Applies overrides with supplemental commands that are not in the base Ribbon.</summary>
    public RibbonDefinition ApplyTo(
        RibbonDefinition definition,
        RibbonCommandCatalog? commandCatalog)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tabOverrides = Tabs.ToDictionary(
            tab => tab.TabId,
            StringComparer.OrdinalIgnoreCase);
        var tabs = new List<RibbonTabDefinition>(definition.Tabs.Count + Tabs.Count);

        var sourceItems = definition.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .GroupBy(static item => item.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in commandCatalog?.Entries ?? [])
        {
            sourceItems.TryAdd(entry.CommandId.Value, new RibbonItemDefinition(entry.CommandId));
        }
        var placedItems = Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Where(static item => item.IsPlacement)
            .Select(static item => item.CommandId.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in definition.Tabs)
        {
            tabOverrides.TryGetValue(tab.Id, out var tabOverride);
            if (tabOverride is { IsVisible: false })
            {
                continue;
            }

            tabs.Add(ApplyTab(tab, tabOverride, sourceItems, placedItems));
        }

        foreach (var customTab in Tabs.Where(static tab => tab.IsCustom))
        {
            if (definition.Tabs.Any(tab => EqualsId(tab.Id, customTab.TabId)))
            {
                throw new InvalidOperationException($"Custom ribbon tab id '{customTab.TabId}' collides with the application definition.");
            }

            tabs.Add(CreateCustomTab(customTab, sourceItems));
        }

        var retainedTabIds = tabs.Select(static tab => tab.Id).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        return new RibbonDefinition(
            tabs,
            definition.ContextualTabs.Where(rule => retainedTabIds.Contains(rule.TabId)),
            CreateQuickAccessToolbar(definition),
            definition.Backstage);
    }

    private static RibbonTabDefinition ApplyTab(
        RibbonTabDefinition tab,
        RibbonTabCustomization? customization,
        IReadOnlyDictionary<string, RibbonItemDefinition> sourceItems,
        IReadOnlySet<string> placedItems)
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

            groups.Add(ApplyGroup(group, groupOverride, sourceItems, placedItems));
        }

        foreach (var customGroup in (customization?.Groups ?? []).Where(static group => group.IsCustom))
        {
            if (tab.Groups.Any(group => EqualsId(group.Id, customGroup.GroupId)))
            {
                throw new InvalidOperationException($"Custom ribbon group id '{customGroup.GroupId}' collides in tab '{tab.Id}'.");
            }

            groups.Add(CreateCustomGroup(customGroup, sourceItems));
        }

        return new RibbonTabDefinition(
            tab.Id,
            customization?.Caption ?? tab.Caption,
            groups,
            customization?.Order ?? tab.Order);
    }

    private static RibbonGroupDefinition ApplyGroup(
        RibbonGroupDefinition group,
        RibbonGroupCustomization? customization,
        IReadOnlyDictionary<string, RibbonItemDefinition> sourceItems,
        IReadOnlySet<string> placedItems)
    {
        var itemOverrides = (customization?.Items ?? [])
            .ToDictionary(
                item => item.CommandId.Value,
                StringComparer.OrdinalIgnoreCase);
        var items = new List<RibbonItemDefinition>(group.Items.Count);

        foreach (var item in group.Items)
        {
            if (placedItems.Contains(item.CommandId.Value) &&
                !(itemOverrides.TryGetValue(item.CommandId.Value, out var placement) && placement.IsPlacement))
            {
                continue;
            }
            itemOverrides.TryGetValue(item.CommandId.Value, out var itemOverride);
            if (itemOverride is { IsVisible: false })
            {
                continue;
            }

            items.Add(item.WithLayout(
                itemOverride?.IsLarge ?? item.IsLarge,
                itemOverride?.Order ?? item.Order));
        }

        foreach (var itemOverride in itemOverrides.Values.Where(static item => item.IsPlacement))
        {
            if (items.Any(item => EqualsId(item.CommandId.Value, itemOverride.CommandId.Value)))
            {
                continue;
            }
            if (sourceItems.TryGetValue(itemOverride.CommandId.Value, out var source))
            {
                items.Add(source.WithLayout(itemOverride.IsLarge ?? source.IsLarge, itemOverride.Order ?? source.Order));
            }
        }

        return new RibbonGroupDefinition(
            group.Id,
            customization?.Caption ?? group.Caption,
            items,
            customization?.Order ?? group.Order,
            group.CollapsePriority);
    }

    private RibbonCommandSurfaceItem[] CreateQuickAccessToolbar(RibbonDefinition definition)
    {
        if (!HasQuickAccessToolbarOverride)
        {
            return definition.QuickAccessToolbar.ToArray();
        }

        var existing = definition.QuickAccessToolbar.ToDictionary(
            static item => item.CommandId.Value,
            StringComparer.OrdinalIgnoreCase);
        var allocatedTips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return QuickAccessToolbar.OrderBy(static item => item.Order).Select((item, index) =>
        {
            var requested = item.KeyTip ?? existing.GetValueOrDefault(item.CommandId.Value)?.KeyTip;
            var keyTip = AllocateKeyTip(requested, index, allocatedTips);
            return new RibbonCommandSurfaceItem(item.CommandId, keyTip);
        }).ToArray();
    }

    private static RibbonTabDefinition CreateCustomTab(
        RibbonTabCustomization customization,
        IReadOnlyDictionary<string, RibbonItemDefinition> sourceItems) =>
        new(
            customization.TabId,
            customization.Caption ?? customization.TabId,
            customization.Groups.Where(static group => group.IsCustom)
                .Select(group => CreateCustomGroup(group, sourceItems)),
            customization.Order ?? 0);

    private static RibbonGroupDefinition CreateCustomGroup(
        RibbonGroupCustomization customization,
        IReadOnlyDictionary<string, RibbonItemDefinition> sourceItems) =>
        new(
            customization.GroupId,
            customization.Caption ?? customization.GroupId,
            customization.Items.Where(static item => item.IsPlacement)
                .Where(item => sourceItems.ContainsKey(item.CommandId.Value))
                .Select(item => sourceItems[item.CommandId.Value].WithLayout(
                    item.IsLarge ?? sourceItems[item.CommandId.Value].IsLarge,
                    item.Order ?? 0)),
            customization.Order ?? 0);

    private static string AllocateKeyTip(string? requested, int index, HashSet<string> used)
    {
        if (!string.IsNullOrWhiteSpace(requested) && !HasKeyTipCollision(used, requested))
        {
            used.Add(requested);
            return requested;
        }
        for (var candidate = index + 1; candidate < 46_656; candidate++)
        {
            var value = $"Q{ToBase36(candidate).PadLeft(3, '0')}";
            if (!HasKeyTipCollision(used, value))
            {
                used.Add(value);
                return value;
            }
        }
        throw new InvalidOperationException("The Quick Access Toolbar key-tip space is exhausted.");
    }

    private static bool HasKeyTipCollision(IEnumerable<string> used, string candidate) =>
        used.Any(value => value.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(value, StringComparison.OrdinalIgnoreCase));

    private static string ToBase36(int value)
    {
        const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<char> buffer = stackalloc char[3];
        var index = buffer.Length;
        do
        {
            buffer[--index] = Digits[value % Digits.Length];
            value /= Digits.Length;
        }
        while (value > 0);
        return new string(buffer[index..]);
    }

    private static bool EqualsId(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class RibbonTabCustomization
{
    public RibbonTabCustomization(
        string tabId,
        bool isVisible,
        int? order,
        IEnumerable<RibbonGroupCustomization>? groups)
        : this(tabId, isVisible, order, groups, null, false)
    {
    }

    /// <summary>
    /// Creates overrides scoped to one ribbon tab id.
    /// </summary>
    public RibbonTabCustomization(
        string tabId,
        bool isVisible = true,
        int? order = null,
        IEnumerable<RibbonGroupCustomization>? groups = null,
        string? caption = null,
        bool isCustom = false)
    {
        TabId = CustomizationValidation.RequiredId(tabId, nameof(tabId));
        IsVisible = isVisible;
        Order = order;
        Groups = CustomizationValidation.MaterializeUnique(
            groups ?? [],
            static group => group.GroupId,
            $"ribbon group customization in tab '{TabId}'");
        Caption = NormalizeCaption(caption);
        IsCustom = isCustom;
    }

    public string TabId { get; }

    public bool IsVisible { get; }

    public int? Order { get; }

    public IReadOnlyList<RibbonGroupCustomization> Groups { get; }

    public string? Caption { get; }

    public bool IsCustom { get; }

    private static string? NormalizeCaption(string? caption) =>
        string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
}

public sealed class RibbonGroupCustomization
{
    public RibbonGroupCustomization(
        string groupId,
        bool isVisible,
        int? order,
        IEnumerable<RibbonItemCustomization>? items)
        : this(groupId, isVisible, order, items, null, false)
    {
    }
    /// <summary>
    /// Creates overrides scoped to one ribbon group id within its containing tab.
    /// </summary>
    public RibbonGroupCustomization(
        string groupId,
        bool isVisible = true,
        int? order = null,
        IEnumerable<RibbonItemCustomization>? items = null,
        string? caption = null,
        bool isCustom = false)
    {
        GroupId = CustomizationValidation.RequiredId(groupId, nameof(groupId));
        IsVisible = isVisible;
        Order = order;
        Items = CustomizationValidation.MaterializeUnique(
            items ?? [],
            static item => item.CommandId.Value,
            $"ribbon item customization in group '{GroupId}'");
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        IsCustom = isCustom;
    }

    public string GroupId { get; }

    public bool IsVisible { get; }

    public int? Order { get; }

    public IReadOnlyList<RibbonItemCustomization> Items { get; }

    public string? Caption { get; }

    public bool IsCustom { get; }
}

/// <summary>
/// Defines visibility, order and size overrides for one command within a group.
/// </summary>
public sealed record RibbonItemCustomization(
    CommandId CommandId,
    bool IsVisible = true,
    int? Order = null,
    bool? IsLarge = null,
    bool IsPlacement = false)
{
    public RibbonItemCustomization(
        CommandId commandId,
        bool isVisible,
        int? order,
        bool? isLarge)
        : this(commandId, isVisible, order, isLarge, false)
    {
    }
}

public sealed record RibbonQuickAccessItemCustomization
{
    public RibbonQuickAccessItemCustomization(CommandId commandId, int order = 0, string? keyTip = null)
    {
        CommandId = commandId;
        Order = order;
        KeyTip = string.IsNullOrWhiteSpace(keyTip) ? null : RibbonKeyTip.NormalizeRequired(keyTip, nameof(keyTip));
    }

    public CommandId CommandId { get; init; }

    public int Order { get; init; }

    public string? KeyTip { get; init; }
}

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
