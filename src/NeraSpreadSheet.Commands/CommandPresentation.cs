namespace NeraSpreadSheet.Commands;

/// <summary>
/// Immutable command metadata and runtime state consumed by host presenters.
/// </summary>
public sealed record CommandPresentation
{
    public CommandPresentation(
        CommandId CommandId,
        bool IsRegistered,
        string Caption,
        string? Tooltip,
        string? IconKey,
        string? Shortcut,
        bool IsEnabled,
        bool? IsChecked,
        string? SelectedValue = null,
        IEnumerable<CommandItem>? ItemsSource = null)
    {
        this.CommandId = CommandId;
        this.IsRegistered = IsRegistered;
        this.Caption = Caption;
        this.Tooltip = Tooltip;
        this.IconKey = IconKey;
        this.Shortcut = Shortcut;
        this.IsEnabled = IsEnabled;
        this.IsChecked = IsChecked;
        this.SelectedValue = SelectedValue;
        this.ItemsSource = Array.AsReadOnly((ItemsSource ?? []).ToArray());
    }

    public CommandId CommandId { get; }

    public bool IsRegistered { get; }

    public string Caption { get; }

    public string? Tooltip { get; }

    public string? IconKey { get; }

    public string? Shortcut { get; }

    public bool IsEnabled { get; }

    public bool? IsChecked { get; }

    public string? SelectedValue { get; }

    public IReadOnlyList<CommandItem> ItemsSource { get; }

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
            ? descriptor.Caption
            : state.DisplayText;
        return new CommandPresentation(
            commandId,
            IsRegistered: true,
            caption,
            descriptor.Tooltip,
            descriptor.IconKey,
            descriptor.Shortcut,
            state.IsEnabled,
            state.IsChecked,
            state.SelectedValue,
            state.ItemsSource);
    }
}
