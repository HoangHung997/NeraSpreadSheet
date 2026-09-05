using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

public enum RibbonCustomizationOperation
{
    Visibility,
    Rename,
    Reorder,
    Add,
    Remove,
    MoveCommand,
    ResizeCommand,
    QuickAccessToolbar,
    Reset,
    Import,
}

/// <summary>Application-owned restrictions applied before a customization mutation.</summary>
public sealed class RibbonCustomizationPolicy
{
    private readonly HashSet<string> _lockedTabs;
    private readonly HashSet<string> _lockedGroups;
    private readonly HashSet<string> _lockedCommands;

    public RibbonCustomizationPolicy(
        IEnumerable<string>? lockedTabIds = null,
        IEnumerable<string>? lockedGroupIds = null,
        IEnumerable<CommandId>? lockedCommandIds = null,
        bool allowCustomTabs = true,
        bool allowCustomGroups = true,
        bool allowQuickAccessToolbar = true,
        bool allowReset = true,
        bool allowImport = true)
    {
        _lockedTabs = Materialize(lockedTabIds);
        _lockedGroups = Materialize(lockedGroupIds);
        _lockedCommands = Materialize((lockedCommandIds ?? []).Select(static id => id.Value));
        AllowCustomTabs = allowCustomTabs;
        AllowCustomGroups = allowCustomGroups;
        AllowQuickAccessToolbar = allowQuickAccessToolbar;
        AllowReset = allowReset;
        AllowImport = allowImport;
    }

    public static RibbonCustomizationPolicy Unrestricted { get; } = new();

    public bool AllowCustomTabs { get; }

    public bool AllowCustomGroups { get; }

    public bool AllowQuickAccessToolbar { get; }

    public bool AllowReset { get; }

    public bool AllowImport { get; }

    public bool IsAllowed(RibbonCustomizationTarget target, RibbonCustomizationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_lockedTabs.Contains(target.TabId) ||
            target.GroupId is not null && _lockedGroups.Contains(target.GroupId) ||
            target.CommandId is CommandId commandId && _lockedCommands.Contains(commandId.Value))
        {
            return false;
        }

        return operation switch
        {
            RibbonCustomizationOperation.Add when target.Kind == RibbonCustomizationTargetKind.Tab => AllowCustomTabs,
            RibbonCustomizationOperation.Add when target.Kind == RibbonCustomizationTargetKind.Group => AllowCustomGroups,
            RibbonCustomizationOperation.QuickAccessToolbar => AllowQuickAccessToolbar,
            RibbonCustomizationOperation.Reset => AllowReset,
            RibbonCustomizationOperation.Import => AllowImport,
            _ => true,
        };
    }

    internal void Demand(RibbonCustomizationTarget target, RibbonCustomizationOperation operation)
    {
        if (!IsAllowed(target, operation))
        {
            throw new InvalidOperationException(
                $"Application policy locks '{target.TabId}/{target.GroupId}/{target.CommandId}' for {operation}.");
        }
    }

    internal void DemandImport(RibbonDefinition definition, RibbonCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(customization);
        if (!AllowImport) throw new InvalidOperationException("Application policy disables Ribbon profile import.");
        if (!AllowQuickAccessToolbar && customization.HasQuickAccessToolbarOverride &&
            !customization.QuickAccessToolbar.OrderBy(static item => item.Order).Select(static item => item.CommandId)
                .SequenceEqual(definition.QuickAccessToolbar.Select(static item => item.CommandId)))
            throw new InvalidOperationException("Application policy locks the Quick Access Toolbar.");
        foreach (var tab in customization.Tabs)
        {
            if (tab.IsCustom && !AllowCustomTabs) throw new InvalidOperationException("Application policy disables custom Ribbon tabs.");
            var sourceTab = definition.Tabs.FirstOrDefault(candidate => string.Equals(candidate.Id, tab.TabId, StringComparison.OrdinalIgnoreCase));
            if (_lockedTabs.Contains(tab.TabId) && (sourceTab is null || tab.IsCustom || !tab.IsVisible ||
                tab.Caption is not null && !string.Equals(tab.Caption, sourceTab.Caption, StringComparison.Ordinal) ||
                tab.Order is int tabOrder && tabOrder != sourceTab.Order))
                throw new InvalidOperationException($"Application policy locks Ribbon tab '{tab.TabId}'.");
            foreach (var group in tab.Groups)
            {
                if (group.IsCustom && !AllowCustomGroups) throw new InvalidOperationException("Application policy disables custom Ribbon groups.");
                var sourceGroup = sourceTab?.Groups.FirstOrDefault(candidate => string.Equals(candidate.Id, group.GroupId, StringComparison.OrdinalIgnoreCase));
                if (_lockedGroups.Contains(group.GroupId) && (sourceGroup is null || group.IsCustom || !group.IsVisible ||
                    group.Caption is not null && !string.Equals(group.Caption, sourceGroup.Caption, StringComparison.Ordinal) ||
                    group.Order is int groupOrder && groupOrder != sourceGroup.Order))
                    throw new InvalidOperationException($"Application policy locks Ribbon group '{group.GroupId}'.");
                foreach (var item in group.Items.Where(item => _lockedCommands.Contains(item.CommandId.Value)))
                {
                    var sourceItem = sourceGroup?.Items.FirstOrDefault(candidate => candidate.CommandId == item.CommandId);
                    if (sourceItem is null || item.IsPlacement || !item.IsVisible ||
                        item.IsLarge is bool isLarge && isLarge != sourceItem.IsLarge ||
                        item.Order is int itemOrder && itemOrder != sourceItem.Order)
                        throw new InvalidOperationException($"Application policy locks Ribbon command '{item.CommandId}'.");
                }
            }
        }
        var baseQuickAccess = definition.QuickAccessToolbar.Select(static item => item.CommandId).ToArray();
        foreach (var item in customization.QuickAccessToolbar.Where(item => _lockedCommands.Contains(item.CommandId.Value)))
            if (!baseQuickAccess.Contains(item.CommandId)) throw new InvalidOperationException($"Application policy locks Quick Access Toolbar command '{item.CommandId}'.");
    }

    private static HashSet<string> Materialize(IEnumerable<string>? ids)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids ?? [])
        {
            result.Add(CustomizationValidation.RequiredId(id, nameof(ids)));
        }
        return result;
    }
}

public sealed record RibbonCommandCatalogEntry(
    string CategoryId,
    string CategoryCaption,
    CommandId CommandId,
    string Caption,
    string? IconKey);

/// <summary>Bounded immutable command catalog grouped for customization presenters.</summary>
public sealed class RibbonCommandCatalog
{
    public RibbonCommandCatalog(IEnumerable<RibbonCommandCatalogEntry> entries)
    {
        Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
        CustomizationValidation.MaterializeUnique(
            Entries,
            static entry => entry.CommandId.Value,
            "ribbon command catalog");
        Categories = Entries.GroupBy(static entry => entry.CategoryId, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new RibbonCommandCatalogCategory(
                group.Key,
                group.First().CategoryCaption,
                group.ToArray()))
            .ToArray();
    }

    public IReadOnlyList<RibbonCommandCatalogEntry> Entries { get; }

    public IReadOnlyList<RibbonCommandCatalogCategory> Categories { get; }

    public static RibbonCommandCatalog FromDefinition(
        RibbonDefinition definition,
        CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);
        var entries = definition.Tabs.SelectMany(tab => tab.Groups.SelectMany(group =>
            group.Items.Where(static item => item.Kind != RibbonItemKind.Separator)
                .Select(item =>
                {
                    registry.TryResolve(item.CommandId, out var descriptor, out _);
                    return new RibbonCommandCatalogEntry(
                        tab.Id,
                        tab.Caption,
                        item.CommandId,
                        descriptor?.Caption ?? item.CommandId.Value,
                        descriptor?.IconKey);
                })))
            .GroupBy(static entry => entry.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        var placed = entries.Select(static entry => entry.CommandId.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var commandId in registry.RegisteredCommandIds.Where(commandId => !placed.Contains(commandId.Value)))
        {
            registry.TryResolve(commandId, out var descriptor, out _);
            entries.Add(new RibbonCommandCatalogEntry(
                "other",
                "Lệnh khác",
                commandId,
                descriptor?.Caption ?? commandId.Value,
                descriptor?.IconKey));
        }
        return new RibbonCommandCatalog(entries);
    }
}

public sealed record RibbonCommandCatalogCategory(
    string Id,
    string Caption,
    IReadOnlyList<RibbonCommandCatalogEntry> Commands);
