using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Owns the effective ribbon definition and its current command presentation snapshot.
/// </summary>
public sealed class RibbonRuntimeController
{
    private readonly CommandDispatcher _dispatcher;
    private readonly RibbonPresentationProjector _projector;
    private HashSet<CommandId> _visibleCommands = [];
    private CommandShortcutMap _shortcuts = CommandShortcutMap.Create([]);

    /// <summary>
    /// Creates a host-neutral runtime over one immutable ribbon definition.
    /// </summary>
    public RibbonRuntimeController(
        RibbonDefinition definition,
        CommandRegistry registry,
        RibbonCustomization? customization = null,
        CommandContext context = default)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(registry);

        _dispatcher = new CommandDispatcher(registry);
        _projector = new RibbonPresentationProjector(registry);
        Customization = customization;
        EffectiveDefinition = ApplyCustomization(customization);
        Snapshot = Project(EffectiveDefinition, context);
    }

    /// <summary>
    /// Raised synchronously after <see cref="Snapshot"/> is replaced.
    /// </summary>
    public event EventHandler? SnapshotChanged;

    /// <summary>
    /// Gets the application-supplied definition before user customization.
    /// </summary>
    public RibbonDefinition Definition { get; }

    /// <summary>
    /// Gets the customization currently applied to <see cref="Definition"/>.
    /// </summary>
    public RibbonCustomization? Customization { get; private set; }

    /// <summary>
    /// Gets the immutable definition currently visible to presenters.
    /// </summary>
    public RibbonDefinition EffectiveDefinition { get; private set; }

    /// <summary>
    /// Gets the latest command-state snapshot.
    /// </summary>
    public RibbonPresentationSnapshot Snapshot { get; private set; }

    /// <summary>
    /// Replaces the customization and publishes a new presentation snapshot.
    /// Pass <see langword="null"/> to restore the application definition.
    /// </summary>
    public RibbonPresentationSnapshot SetCustomization(
        RibbonCustomization? customization,
        CommandContext context = default)
    {
        Customization = customization;
        EffectiveDefinition = ApplyCustomization(customization);
        return Publish(EffectiveDefinition, context);
    }

    /// <summary>
    /// Re-queries command state and publishes a new immutable snapshot.
    /// </summary>
    public RibbonPresentationSnapshot Refresh(CommandContext context = default) =>
        Publish(EffectiveDefinition, context);

    /// <summary>
    /// Executes a currently visible command through the shared dispatcher.
    /// A successful execution publishes a fresh presentation snapshot.
    /// </summary>
    public async ValueTask<bool> TryActivateAsync(
        CommandId commandId,
        CommandContext context = default)
    {
        if (!_visibleCommands.Contains(commandId))
        {
            return false;
        }

        var executed = await _dispatcher
            .TryExecuteAsync(commandId, context)
            .ConfigureAwait(false);
        if (executed)
        {
            Publish(EffectiveDefinition, context);
        }

        return executed;
    }

    /// <summary>
    /// Resolves a shortcut against commands visible in the current ribbon snapshot.
    /// </summary>
    public bool TryResolveShortcut(string shortcut, out CommandId commandId) =>
        _shortcuts.TryResolve(shortcut, out commandId);

    /// <summary>
    /// Activates a visible command by normalized shortcut through the shared dispatcher.
    /// </summary>
    public ValueTask<bool> TryActivateShortcutAsync(
        string shortcut,
        CommandContext context = default) =>
        TryResolveShortcut(shortcut, out var commandId)
            ? TryActivateAsync(commandId, context)
            : ValueTask.FromResult(false);

    private RibbonDefinition ApplyCustomization(RibbonCustomization? customization) =>
        customization is null ? Definition : customization.ApplyTo(Definition);

    private RibbonPresentationSnapshot Publish(
        RibbonDefinition definition,
        CommandContext context)
    {
        Snapshot = Project(definition, context);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
        return Snapshot;
    }

    private RibbonPresentationSnapshot Project(
        RibbonDefinition definition,
        CommandContext context)
    {
        var snapshot = _projector.Project(definition, context);
        _shortcuts = CommandShortcutMap.Create(snapshot.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Select(static item => item.Command));
        _visibleCommands = definition.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Select(static item => item.CommandId)
            .ToHashSet();
        return snapshot;
    }
}
