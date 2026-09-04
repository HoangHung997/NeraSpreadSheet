using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>Identifies the interaction model used by one Ribbon item.</summary>
public enum RibbonItemKind
{
    Button,
    Toggle,
    SplitButton,
    DropDown,
    Menu,
    ComboBox,
    Gallery,
    ColorPicker,
    Separator,
}

/// <summary>Input supplied to an item-specific logical width measurement.</summary>
public readonly record struct RibbonItemMeasurementContext(
    RibbonItemKind Kind,
    RibbonItemSize Size,
    CommandPresentation Command,
    double DefaultWidth);

/// <summary>Measures one item in logical units before platform scale is applied.</summary>
public delegate double RibbonItemMeasurementCallback(
    RibbonItemMeasurementContext context);

/// <summary>Immutable host-neutral definition of one Ribbon item.</summary>
public sealed class RibbonItemDefinition
{
    /// <summary>
    /// Creates a source-compatible command button. A checked command state is
    /// presented as a toggle for definitions created before explicit item kinds.
    /// </summary>
    public RibbonItemDefinition(
        CommandId CommandId,
        bool IsLarge = false,
        int Order = 0)
        : this(
            CommandId,
            RibbonItemKind.Button,
            IsLarge,
            Order,
            automationName: null,
            measurement: null,
            usesLegacyAutomaticToggle: true)
    {
    }

    /// <summary>Creates an explicitly typed command-backed Ribbon item.</summary>
    public RibbonItemDefinition(
        CommandId commandId,
        RibbonItemKind kind,
        bool isLarge = false,
        int order = 0,
        string? automationName = null,
        RibbonItemMeasurementCallback? measurement = null)
        : this(
            commandId,
            kind,
            isLarge,
            order,
            automationName,
            measurement,
            usesLegacyAutomaticToggle: false)
    {
    }

    private RibbonItemDefinition(
        CommandId commandId,
        RibbonItemKind kind,
        bool isLarge,
        int order,
        string? automationName,
        RibbonItemMeasurementCallback? measurement,
        bool usesLegacyAutomaticToggle)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (kind == RibbonItemKind.Separator)
        {
            throw new ArgumentException(
                "Use RibbonItemDefinition.Separator to create a separator.",
                nameof(kind));
        }

        CommandId = commandId;
        Kind = kind;
        IsLarge = isLarge;
        Order = order;
        AutomationName = NormalizeOptional(automationName);
        Measurement = measurement;
        UsesLegacyAutomaticToggle = usesLegacyAutomaticToggle;
    }

    private RibbonItemDefinition(string separatorId, int order)
    {
        if (string.IsNullOrWhiteSpace(separatorId))
        {
            throw new ArgumentException(
                "A Ribbon separator id is required.",
                nameof(separatorId));
        }

        var normalizedId = separatorId.Trim();
        CommandId = new CommandId($"ribbon.separator.{normalizedId}");
        Kind = RibbonItemKind.Separator;
        Order = order;
        AutomationName = "Dấu phân cách";
        Measurement = static _ => 8d;
    }

    public CommandId CommandId { get; }

    public RibbonItemKind Kind { get; }

    public bool IsLarge { get; }

    public int Order { get; }

    public string? AutomationName { get; }

    public RibbonItemMeasurementCallback? Measurement { get; }

    internal bool UsesLegacyAutomaticToggle { get; }

    /// <summary>Creates a non-command separator with stable identity.</summary>
    public static RibbonItemDefinition Separator(string id, int order = 0) =>
        new(id, order);

    internal RibbonItemDefinition WithLayout(bool isLarge, int order) =>
        Kind == RibbonItemKind.Separator
            ? Separator(CommandId.Value["ribbon.separator.".Length..], order)
            : new RibbonItemDefinition(
                CommandId,
                Kind,
                isLarge,
                order,
                AutomationName,
                Measurement,
                UsesLegacyAutomaticToggle);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Structured activation payload for a selectable Ribbon item.</summary>
public sealed record RibbonItemActivation(
    string? SelectedValue,
    object? OriginalParameter = null);
