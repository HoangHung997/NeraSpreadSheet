namespace NeraSpreadSheet.Commands;

/// <summary>
/// Immutable selectable value exposed by command-backed composite chrome.
/// </summary>
public sealed class CommandItem
{
    /// <summary>Creates one selectable command value.</summary>
    public CommandItem(
        string value,
        string caption,
        bool isEnabled = true,
        bool? isChecked = null,
        string? tooltip = null,
        string? iconKey = null,
        IEnumerable<CommandItem>? children = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A command item value is required.", nameof(value));
        }
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A command item caption is required.", nameof(caption));
        }

        Value = value.Trim();
        Caption = caption.Trim();
        IsEnabled = isEnabled;
        IsChecked = isChecked;
        Tooltip = tooltip;
        IconKey = iconKey;
        Children = MaterializeUnique(children ?? [], $"children of '{Value}'");
    }

    public string Value { get; }

    public string Caption { get; }

    public bool IsEnabled { get; }

    public bool? IsChecked { get; }

    public string? Tooltip { get; }

    public string? IconKey { get; }

    public IReadOnlyList<CommandItem> Children { get; }

    internal static IReadOnlyList<CommandItem> MaterializeUnique(
        IEnumerable<CommandItem> items,
        string scope)
    {
        var materialized = items.ToArray();
        var duplicate = materialized
            .GroupBy(static item => item.Value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate command item value '{duplicate.Key}' in {scope}.");
        }
        return Array.AsReadOnly(materialized);
    }
}

/// <summary>
/// Immutable command state consumed by command and Ribbon presentation snapshots.
/// </summary>
public readonly record struct CommandState
{
    private readonly IReadOnlyList<CommandItem>? _itemsSource;

    public CommandState(
        bool IsEnabled,
        bool? IsChecked = null,
        string? DisplayText = null,
        string? SelectedValue = null,
        IEnumerable<CommandItem>? ItemsSource = null)
    {
        this.IsEnabled = IsEnabled;
        this.IsChecked = IsChecked;
        this.DisplayText = DisplayText;
        this.SelectedValue = SelectedValue;
        _itemsSource = ItemsSource is null
            ? null
            : CommandItem.MaterializeUnique(ItemsSource, "command state items source");
    }

    public bool IsEnabled { get; }

    public bool? IsChecked { get; }

    public string? DisplayText { get; }

    public string? SelectedValue { get; }

    public IReadOnlyList<CommandItem> ItemsSource => _itemsSource ?? [];

    public static CommandState Enabled { get; } = new(true);

    public static CommandState Disabled { get; } = new(false);
}

public interface IStatefulCommandHandler : ICommandHandler
{
    CommandState GetState(CommandContext context);
}
