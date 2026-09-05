using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Immutable command-aware ribbon snapshot for platform presenters.
/// </summary>
public sealed class RibbonPresentationSnapshot
{
    internal RibbonPresentationSnapshot(
        IReadOnlyList<RibbonTabPresentation> tabs,
        IReadOnlyList<CommandPresentation> quickAccessToolbar,
        IReadOnlyList<CommandPresentation> backstage)
    {
        Tabs = tabs;
        QuickAccessToolbar = quickAccessToolbar;
        Backstage = backstage;
    }

    public IReadOnlyList<RibbonTabPresentation> Tabs { get; }

    public IReadOnlyList<CommandPresentation> QuickAccessToolbar { get; }

    public IReadOnlyList<CommandPresentation> Backstage { get; }
}

public sealed class RibbonTabPresentation
{
    internal RibbonTabPresentation(
        string id,
        string caption,
        IReadOnlyList<RibbonGroupPresentation> groups)
    {
        Id = id;
        Caption = caption;
        Groups = groups;
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonGroupPresentation> Groups { get; }
}

public sealed class RibbonGroupPresentation
{
    internal RibbonGroupPresentation(
        string id,
        string caption,
        IReadOnlyList<RibbonItemPresentation> items,
        int collapsePriority)
    {
        Id = id;
        Caption = caption;
        Items = items;
        CollapsePriority = collapsePriority;
    }

    public string Id { get; }

    public string Caption { get; }

    public IReadOnlyList<RibbonItemPresentation> Items { get; }

    /// <summary>
    /// Gets the relative importance used by responsive group collapse.
    /// </summary>
    public int CollapsePriority { get; }
}

public sealed class RibbonItemPresentation
{
    internal RibbonItemPresentation(
        CommandPresentation command,
        RibbonItemDefinition definition)
    {
        Command = command;
        Definition = definition;
    }

    public CommandPresentation Command { get; }

    public RibbonItemDefinition Definition { get; }

    public RibbonItemKind Kind => Definition.Kind;

    public bool IsLarge => Definition.IsLarge;

    /// <summary>Gets whether the item uses toggle chrome.</summary>
    public bool IsToggle => Kind == RibbonItemKind.Toggle ||
        (Definition.UsesLegacyAutomaticToggle && Command.IsChecked.HasValue);

    /// <summary>Gets the automation name, falling back to the command caption.</summary>
    public string AutomationName => Definition.AutomationName ?? Command.Caption;
}

/// <summary>
/// Projects a ribbon definition into one consistent command-state snapshot.
/// </summary>
public sealed class RibbonPresentationProjector
{
    private readonly CommandPresentationResolver _resolver;

    public RibbonPresentationProjector(CommandRegistry registry)
    {
        _resolver = new CommandPresentationResolver(registry);
    }

    /// <summary>Gets or sets resources used on the next projection.</summary>
    public PresentationLocalization Localization
    {
        get => _resolver.Localization;
        set => _resolver.Localization = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Projects the current registry state, resolving each command at most once.
    /// </summary>
    public RibbonPresentationSnapshot Project(
        RibbonDefinition definition,
        CommandContext context = default) =>
        Project(definition, context, default);

    /// <summary>
    /// Projects the current registry and contextual-selection state, resolving each
    /// command at most once.
    /// </summary>
    public RibbonPresentationSnapshot Project(
        RibbonDefinition definition,
        CommandContext context,
        RibbonSelectionContext selectionContext)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var cache = new Dictionary<CommandId, CommandPresentation>();
        var tabs = definition.Tabs
            .Where(tab => IsTabVisible(definition, tab, selectionContext))
            .Select(tab => ProjectTab(tab, context, cache))
            .ToArray();
        var quickAccessToolbar = definition.QuickAccessToolbar
            .Select(item => Resolve(item.CommandId, context, cache))
            .ToArray();
        var backstage = definition.Backstage
            .Select(item => Resolve(item.CommandId, context, cache))
            .ToArray();
        return new RibbonPresentationSnapshot(tabs, quickAccessToolbar, backstage);
    }

    private static bool IsTabVisible(
        RibbonDefinition definition,
        RibbonTabDefinition tab,
        RibbonSelectionContext context)
    {
        var rule = definition.ContextualTabs.FirstOrDefault(candidate => string.Equals(
            candidate.TabId,
            tab.Id,
            StringComparison.OrdinalIgnoreCase));
        return rule is null || rule.IsVisible(context);
    }

    private RibbonTabPresentation ProjectTab(
        RibbonTabDefinition tab,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var groups = tab.Groups
            .Select(group => ProjectGroup(group, context, cache))
            .ToArray();
        return new RibbonTabPresentation(tab.Id, tab.CaptionResourceKey is { } key ? Localization.Get(key) : tab.Caption, groups);
    }

    private RibbonGroupPresentation ProjectGroup(
        RibbonGroupDefinition group,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var items = group.Items
            .Select(item => new RibbonItemPresentation(
                Resolve(item.CommandId, context, cache),
                item))
            .ToArray();
        return new RibbonGroupPresentation(
            group.Id,
            group.CaptionResourceKey is { } key ? Localization.Get(key) : group.Caption,
            items,
            group.CollapsePriority);
    }

    private CommandPresentation Resolve(
        CommandId commandId,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        if (!cache.TryGetValue(commandId, out var presentation))
        {
            presentation = _resolver.Resolve(commandId, context);
            cache.Add(commandId, presentation);
        }
        return presentation;
    }
}
