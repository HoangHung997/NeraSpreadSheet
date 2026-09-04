using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Immutable command-aware ribbon snapshot for platform presenters.
/// </summary>
public sealed class RibbonPresentationSnapshot
{
    internal RibbonPresentationSnapshot(IReadOnlyList<RibbonTabPresentation> tabs)
    {
        Tabs = tabs;
    }

    public IReadOnlyList<RibbonTabPresentation> Tabs { get; }
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
        bool isLarge)
    {
        Command = command;
        IsLarge = isLarge;
    }

    public CommandPresentation Command { get; }

    public bool IsLarge { get; }
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

    /// <summary>
    /// Projects the current registry state, resolving each command at most once.
    /// </summary>
    public RibbonPresentationSnapshot Project(
        RibbonDefinition definition,
        CommandContext context = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var cache = new Dictionary<CommandId, CommandPresentation>();
        var tabs = definition.Tabs
            .Select(tab => ProjectTab(tab, context, cache))
            .ToArray();
        return new RibbonPresentationSnapshot(tabs);
    }

    private RibbonTabPresentation ProjectTab(
        RibbonTabDefinition tab,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var groups = tab.Groups
            .Select(group => ProjectGroup(group, context, cache))
            .ToArray();
        return new RibbonTabPresentation(tab.Id, tab.Caption, groups);
    }

    private RibbonGroupPresentation ProjectGroup(
        RibbonGroupDefinition group,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var items = group.Items
            .Select(item => new RibbonItemPresentation(
                Resolve(item.CommandId, context, cache),
                item.IsLarge))
            .ToArray();
        return new RibbonGroupPresentation(
            group.Id,
            group.Caption,
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
