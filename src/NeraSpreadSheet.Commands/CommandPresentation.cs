namespace NeraSpreadSheet.Commands;

/// <summary>
/// Immutable command metadata and runtime state consumed by host presenters.
/// </summary>
public sealed record CommandPresentation(
    CommandId CommandId,
    bool IsRegistered,
    string Caption,
    string? Tooltip,
    string? IconKey,
    string? Shortcut,
    bool IsEnabled,
    bool? IsChecked)
{
    /// <summary>
    /// Creates command presentation with selectable state while retaining the
    /// original eight-parameter record constructor for binary compatibility.
    /// </summary>
    public CommandPresentation(
        CommandId CommandId,
        bool IsRegistered,
        string Caption,
        string? Tooltip,
        string? IconKey,
        string? Shortcut,
        bool IsEnabled,
        bool? IsChecked,
        string? SelectedValue,
        IEnumerable<CommandItem>? ItemsSource)
        : this(
            CommandId,
            IsRegistered,
            Caption,
            Tooltip,
            IconKey,
            Shortcut,
            IsEnabled,
            IsChecked)
    {
        this.SelectedValue = SelectedValue;
        this.ItemsSource = CommandItem.MaterializeUnique(
            ItemsSource ?? [],
            "command presentation items source");
    }

    public string? SelectedValue { get; }

    public IReadOnlyList<CommandItem> ItemsSource { get; } = [];

    /// <summary>Gets the immutable selectable item list.</summary>
    public IReadOnlyList<CommandItem> SelectableItems => ItemsSource;
}

/// <summary>
/// Resolves registered descriptors and dispatcher state into presentation values.
/// </summary>
public sealed class CommandPresentationResolver
{
    private readonly CommandRegistry _registry;
    private readonly CommandDispatcher _dispatcher;

    public CommandPresentationResolver(CommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _dispatcher = new CommandDispatcher(registry);
    }

    /// <summary>Gets or sets resources used on the next projection.</summary>
    public PresentationLocalization Localization { get; set; } = PresentationLocalization.Default;

    /// <summary>
    /// Resolves one command. Unregistered commands remain visible as disabled fallback items.
    /// </summary>
    public CommandPresentation Resolve(
        CommandId commandId,
        CommandContext context = default)
    {
        if (!_registry.TryResolve(commandId, out var descriptor, out _)
            || descriptor is null)
        {
            return new CommandPresentation(
                commandId,
                IsRegistered: false,
                commandId.Value,
                Tooltip: null,
                IconKey: null,
                Shortcut: null,
                IsEnabled: false,
                IsChecked: null,
                SelectedValue: null,
                ItemsSource: []);
        }

        var state = _dispatcher.QueryState(commandId, context);
        var caption = string.IsNullOrWhiteSpace(state.DisplayText)
            ? descriptor.CaptionResourceKey is { } captionKey ? Localization.Get(captionKey) : Localization.CommandCaption(commandId, descriptor.Caption)
            : PresentationLocalization.IsDefaultCommand(descriptor) &&
                (string.Equals(commandId.Value, "Edit.Undo", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(commandId.Value, "Edit.Redo", StringComparison.OrdinalIgnoreCase))
                ? Localization.Get(state.DisplayText)
                : state.DisplayText;
        var items = PresentationLocalization.IsDefaultCommand(descriptor) && string.Equals(commandId.Value, "Table.TotalsFunction", StringComparison.OrdinalIgnoreCase)
            ? state.ItemsSource.Select(item => new CommandItem(item.Value, Localization.Get(item.Caption),
                item.IsEnabled, item.IsChecked, item.Tooltip, item.IconKey, item.Children))
            : state.ItemsSource;
        if (PresentationLocalization.IsDefaultCommand(descriptor) && string.Equals(commandId.Value, "Table.Style", StringComparison.OrdinalIgnoreCase))
        {
            caption = Localization.TableStyleCaption(caption);
            items = state.ItemsSource.Select(item => new CommandItem(item.Value,
                item.Caption == item.Value ? Localization.TableStyleCaption(item.Caption) : item.Caption,
                item.IsEnabled, item.IsChecked, item.Tooltip is null ? null : Localization.Get(item.Tooltip),
                item.IconKey, item.Children));
        }
        return new CommandPresentation(
            commandId,
            IsRegistered: true,
            caption,
            descriptor.TooltipResourceKey is { } tooltipKey ? Localization.Get(tooltipKey) : descriptor.Tooltip,
            descriptor.IconKey,
            descriptor.Shortcut,
            state.IsEnabled,
            state.IsChecked,
            state.SelectedValue,
            items);
    }
}
