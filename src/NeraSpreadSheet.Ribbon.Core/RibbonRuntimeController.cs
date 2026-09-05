using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Owns the effective ribbon definition and its current command presentation snapshot.
/// </summary>
public sealed class RibbonRuntimeController
{
    private readonly CommandDispatcher _dispatcher;
    private readonly RibbonPresentationProjector _projector;
    private readonly RibbonCommandCatalog _commandCatalog;
    private HashSet<CommandId> _visibleCommands = [];
    private CommandShortcutMap _shortcuts = CommandShortcutMap.Create([]);
    private RibbonSelectionContext _selectionContext;

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
        _commandCatalog = RibbonCommandCatalog.FromDefinition(Definition, registry);
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

    /// <summary>Gets the immutable grouped catalog available to customization hosts.</summary>
    public RibbonCommandCatalog CommandCatalog => _commandCatalog;

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

    /// <summary>Gets the selection state currently controlling contextual tabs.</summary>
    public RibbonSelectionContext SelectionContext => _selectionContext;

    /// <summary>Gets the persisted expanded/minimized state.</summary>
    public bool IsMinimized { get; private set; }

    /// <summary>Gets the current host-neutral key-tip navigation state.</summary>
    public RibbonKeyTipController KeyTips { get; private set; } = null!;

    /// <summary>Updates selection/table state and atomically republishes visible tabs.</summary>
    public RibbonPresentationSnapshot SetSelectionContext(
        RibbonSelectionContext selectionContext,
        CommandContext context = default)
    {
        var projection = CreateProjection(EffectiveDefinition, context, selectionContext);
        _selectionContext = selectionContext;
        return Publish(projection);
    }

    /// <summary>Updates the expanded/minimized state without changing command projection.</summary>
    public void SetMinimized(bool isMinimized)
    {
        if (IsMinimized == isMinimized)
        {
            return;
        }
        IsMinimized = isMinimized;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Restores persisted view state and publishes one coherent update.</summary>
    public void RestoreViewState(RibbonViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (IsMinimized == state.IsMinimized)
        {
            return;
        }
        IsMinimized = state.IsMinimized;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces the customization and publishes a new presentation snapshot.
    /// Pass <see langword="null"/> to restore the application definition.
    /// </summary>
    public RibbonPresentationSnapshot SetCustomization(
        RibbonCustomization? customization,
        CommandContext context = default)
    {
        var effectiveDefinition = ApplyCustomization(customization);
        var projection = CreateProjection(effectiveDefinition, context, _selectionContext);
        Customization = customization;
        EffectiveDefinition = effectiveDefinition;
        return Publish(projection);
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
    /// Executes a selectable Ribbon value while retaining any host-supplied
    /// command parameter in a structured activation payload.
    /// </summary>
    public async ValueTask<bool> TryActivateItemAsync(
        CommandId commandId,
        string? selectedValue,
        CommandContext context = default)
    {
        if (!TryResolveSelectableLeaf(commandId, selectedValue, out _))
        {
            return false;
        }

        var activationContext = context with
        {
            Parameter = new RibbonItemActivation(
                selectedValue,
                context.Parameter),
        };
        var executed = await _dispatcher
            .TryExecuteAsync(commandId, activationContext)
            .ConfigureAwait(false);
        if (executed)
        {
            // Command state belongs to the workbook/host context. The structured
            // item activation payload is only an execution parameter.
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
        customization is null ? Definition : customization.ApplyTo(Definition, _commandCatalog);

    private bool TryResolveSelectableLeaf(
        CommandId commandId,
        string? selectedValue,
        out CommandItem? selectedItem)
    {
        selectedItem = null;
        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            return false;
        }

        var presentation = Snapshot.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .FirstOrDefault(item =>
                item.Command.CommandId == commandId &&
                IsSelectableKind(item.Kind));
        if (presentation is null || !presentation.Command.IsEnabled)
        {
            return false;
        }

        var ambiguous = false;
        var selectedPathEnabled = false;
        FindSelectableLeaf(
            presentation.Command.SelectableItems,
            selectedValue,
            ancestorsEnabled: true,
            ref selectedItem,
            ref selectedPathEnabled,
            ref ambiguous);
        return !ambiguous && selectedItem is not null && selectedPathEnabled;
    }

    private static bool IsSelectableKind(RibbonItemKind kind) => kind is
        RibbonItemKind.SplitButton or
        RibbonItemKind.DropDown or
        RibbonItemKind.Menu or
        RibbonItemKind.ComboBox or
        RibbonItemKind.Gallery or
        RibbonItemKind.ColorPicker;

    private static void FindSelectableLeaf(
        IReadOnlyList<CommandItem> items,
        string selectedValue,
        bool ancestorsEnabled,
        ref CommandItem? selectedItem,
        ref bool selectedPathEnabled,
        ref bool ambiguous)
    {
        foreach (var item in items)
        {
            if (item.Children.Count > 0)
            {
                FindSelectableLeaf(
                    item.Children,
                    selectedValue,
                    ancestorsEnabled && item.IsEnabled,
                    ref selectedItem,
                    ref selectedPathEnabled,
                    ref ambiguous);
                if (ambiguous)
                {
                    return;
                }
                continue;
            }
            if (!string.Equals(item.Value, selectedValue, StringComparison.Ordinal))
            {
                continue;
            }
            if (selectedItem is not null)
            {
                ambiguous = true;
                return;
            }
            selectedItem = item;
            selectedPathEnabled = ancestorsEnabled && item.IsEnabled;
        }
    }

    private RibbonPresentationSnapshot Publish(
        RibbonDefinition definition,
        CommandContext context)
    {
        return Publish(CreateProjection(definition, context, _selectionContext));
    }

    private RibbonPresentationSnapshot Publish(RibbonProjection projection)
    {
        Snapshot = projection.Snapshot;
        _shortcuts = projection.Shortcuts;
        _visibleCommands = projection.VisibleCommands;
        KeyTips = projection.KeyTips;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
        return Snapshot;
    }

    private RibbonPresentationSnapshot Project(
        RibbonDefinition definition,
        CommandContext context)
    {
        var projection = CreateProjection(definition, context, _selectionContext);
        _shortcuts = projection.Shortcuts;
        _visibleCommands = projection.VisibleCommands;
        KeyTips = projection.KeyTips;
        return projection.Snapshot;
    }

    private RibbonProjection CreateProjection(
        RibbonDefinition definition,
        CommandContext context,
        RibbonSelectionContext selectionContext)
    {
        var snapshot = _projector.Project(definition, context, selectionContext);
        var commands = snapshot.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Select(static item => item.Command)
            .Concat(snapshot.QuickAccessToolbar)
            .Concat(snapshot.Backstage)
            .DistinctBy(static command => command.CommandId)
            .ToArray();
        var shortcuts = CommandShortcutMap.Create(commands);
        var visibleCommands = commands
            .Select(static command => command.CommandId)
            .ToHashSet();
        var keyTips = new RibbonKeyTipController(definition, snapshot);
        if (KeyTips is { } previousKeyTips)
        {
            keyTips.RestoreScope(previousKeyTips.Scope, previousKeyTips.ActiveTabId);
        }
        return new RibbonProjection(snapshot, shortcuts, visibleCommands, keyTips);
    }

    private sealed record RibbonProjection(
        RibbonPresentationSnapshot Snapshot,
        CommandShortcutMap Shortcuts,
        HashSet<CommandId> VisibleCommands,
        RibbonKeyTipController KeyTips);
}
