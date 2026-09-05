using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Bars.Core;

/// <summary>
/// Owns the effective bar definition and its current command presentation snapshot.
/// </summary>
public sealed class BarRuntimeController
{
    private readonly CommandDispatcher _dispatcher;
    private readonly BarPresentationProjector _projector;
    private HashSet<CommandId> _visibleCommands = [];
    private CommandShortcutMap _shortcuts = CommandShortcutMap.Create([]);

    /// <summary>
    /// Creates a host-neutral runtime over one immutable toolbar, menu or context menu.
    /// </summary>
    public BarRuntimeController(
        BarDefinition definition,
        CommandRegistry registry,
        BarCustomization? customization = null,
        CommandContext context = default)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(registry);

        _dispatcher = new CommandDispatcher(registry);
        _projector = new BarPresentationProjector(registry);
        Customization = customization;
        EffectiveDefinition = ApplyCustomization(customization);
        Snapshot = Project(EffectiveDefinition, context);
    }

    /// <summary>Gets the resources used by this runtime and its native chrome.</summary>
    public PresentationLocalization Localization => _projector.Localization;

    /// <summary>
    /// Switches presentation resources and republishes command state. Call on the
    /// host UI context, supplying the same command context as a normal refresh.
    /// Definitions, customization profiles, shortcuts and workbook history are unchanged.
    /// </summary>
    public BarPresentationSnapshot SetLocalization(PresentationLocalization localization, CommandContext context = default)
    {
        ArgumentNullException.ThrowIfNull(localization);
        var previous = _projector.Localization;
        _projector.Localization = localization;
        BarPresentationSnapshot snapshot;
        try { snapshot = Project(EffectiveDefinition, context); }
        catch { _projector.Localization = previous; throw; }
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
        return Snapshot;
    }

    /// <summary>
    /// Raised synchronously after <see cref="Snapshot"/> is replaced.
    /// </summary>
    public event EventHandler? SnapshotChanged;

    /// <summary>
    /// Gets the application-supplied definition before user customization.
    /// </summary>
    public BarDefinition Definition { get; }

    /// <summary>
    /// Gets the customization currently applied to <see cref="Definition"/>.
    /// </summary>
    public BarCustomization? Customization { get; private set; }

    /// <summary>
    /// Gets the immutable definition currently visible to presenters.
    /// </summary>
    public BarDefinition EffectiveDefinition { get; private set; }

    /// <summary>
    /// Gets the latest command-state snapshot.
    /// </summary>
    public BarPresentationSnapshot Snapshot { get; private set; }

    /// <summary>
    /// Replaces the customization and publishes a new presentation snapshot.
    /// Pass <see langword="null"/> to restore the application definition.
    /// </summary>
    public BarPresentationSnapshot SetCustomization(
        BarCustomization? customization,
        CommandContext context = default)
    {
        Customization = customization;
        EffectiveDefinition = ApplyCustomization(customization);
        return Publish(EffectiveDefinition, context);
    }

    /// <summary>
    /// Re-queries command state and publishes a new immutable snapshot.
    /// </summary>
    public BarPresentationSnapshot Refresh(CommandContext context = default) =>
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

    public bool TryResolveShortcut(string shortcut, out CommandId commandId) =>
        _shortcuts.TryResolve(shortcut, out commandId);

    public ValueTask<bool> TryActivateShortcutAsync(
        string shortcut,
        CommandContext context = default) =>
        TryResolveShortcut(shortcut, out var commandId)
            ? TryActivateAsync(commandId, context)
            : ValueTask.FromResult(false);

    private BarDefinition ApplyCustomization(BarCustomization? customization) =>
        customization is null ? Definition : customization.ApplyTo(Definition);

    private BarPresentationSnapshot Publish(
        BarDefinition definition,
        CommandContext context)
    {
        Snapshot = Project(definition, context);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
        return Snapshot;
    }

    private BarPresentationSnapshot Project(
        BarDefinition definition,
        CommandContext context)
    {
        var snapshot = _projector.Project(definition, context);
        _shortcuts = CommandShortcutMap.Create(EnumeratePresentations(snapshot.Items));
        _visibleCommands = EnumerateCommands(definition.Items).ToHashSet();
        return snapshot;
    }

    private static IEnumerable<CommandPresentation> EnumeratePresentations(
        IReadOnlyList<BarItemPresentation> items)
    {
        foreach (var item in items)
        {
            if (item.Command is CommandPresentation command)
            {
                yield return command;
            }
            foreach (var child in EnumeratePresentations(item.Children))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<CommandId> EnumerateCommands(
        IReadOnlyList<BarItemDefinition> items)
    {
        foreach (var item in items)
        {
            if (item.CommandId is CommandId commandId)
            {
                yield return commandId;
            }

            foreach (var child in EnumerateCommands(item.Children))
            {
                yield return child;
            }
        }
    }
}
