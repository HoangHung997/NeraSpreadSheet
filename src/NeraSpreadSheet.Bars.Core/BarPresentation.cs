using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Bars.Core;

/// <summary>
/// Immutable command-aware bar snapshot for platform presenters.
/// </summary>
public sealed class BarPresentationSnapshot
{
    internal BarPresentationSnapshot(
        string id,
        BarKind kind,
        string? caption,
        IReadOnlyList<BarItemPresentation> items)
    {
        Id = id;
        Kind = kind;
        Caption = caption;
        Items = items;
    }

    public string Id { get; }

    public BarKind Kind { get; }

    public string? Caption { get; }

    public IReadOnlyList<BarItemPresentation> Items { get; }
}

public sealed class BarItemPresentation
{
    internal BarItemPresentation(
        BarItemKind kind,
        string? id,
        string? caption,
        CommandPresentation? command,
        IReadOnlyList<BarItemPresentation> children,
        bool isEnabled)
    {
        Kind = kind;
        Id = id;
        Caption = caption;
        Command = command;
        Children = children;
        IsEnabled = isEnabled;
    }

    public BarItemKind Kind { get; }

    public string? Id { get; }

    public string? Caption { get; }

    public CommandPresentation? Command { get; }

    public IReadOnlyList<BarItemPresentation> Children { get; }

    public bool IsEnabled { get; }
}

/// <summary>
/// Projects a toolbar, menu or context menu into one consistent command-state snapshot.
/// </summary>
public sealed class BarPresentationProjector
{
    private readonly CommandPresentationResolver _resolver;

    public BarPresentationProjector(CommandRegistry registry)
    {
        _resolver = new CommandPresentationResolver(registry);
    }

    /// <summary>
    /// Projects the current registry state, resolving each command at most once.
    /// </summary>
    public BarPresentationSnapshot Project(
        BarDefinition definition,
        CommandContext context = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var cache = new Dictionary<CommandId, CommandPresentation>();
        var items = ProjectItems(definition.Items, context, cache);
        return new BarPresentationSnapshot(
            definition.Id,
            definition.Kind,
            definition.Caption,
            items);
    }

    private BarItemPresentation[] ProjectItems(
        IReadOnlyList<BarItemDefinition> definitions,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var result = new BarItemPresentation[definitions.Count];
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            result[index] = definition.Kind switch
            {
                BarItemKind.Command => ProjectCommand(definition, context, cache),
                BarItemKind.Separator => new BarItemPresentation(
                    BarItemKind.Separator,
                    definition.Id,
                    caption: null,
                    command: null,
                    children: [],
                    isEnabled: false),
                BarItemKind.Submenu => ProjectSubmenu(definition, context, cache),
                _ => throw new InvalidOperationException(
                    $"Unsupported bar item kind '{definition.Kind}'."),
            };
        }
        return result;
    }

    private BarItemPresentation ProjectCommand(
        BarItemDefinition definition,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var commandId = definition.CommandId!.Value;
        var command = Resolve(commandId, context, cache);
        return new BarItemPresentation(
            BarItemKind.Command,
            definition.Id,
            command.Caption,
            command,
            children: [],
            command.IsEnabled);
    }

    private BarItemPresentation ProjectSubmenu(
        BarItemDefinition definition,
        CommandContext context,
        IDictionary<CommandId, CommandPresentation> cache)
    {
        var children = ProjectItems(definition.Children, context, cache);
        return new BarItemPresentation(
            BarItemKind.Submenu,
            definition.Id,
            definition.Caption,
            command: null,
            children,
            children.Any(static child => child.IsEnabled));
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
