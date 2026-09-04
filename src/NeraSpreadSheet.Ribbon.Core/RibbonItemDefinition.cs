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
public sealed record RibbonItemDefinition(
    CommandId CommandId,
    bool IsLarge = false,
    int Order = 0)
{
    /// <summary>Creates an explicitly typed command-backed Ribbon item.</summary>
    public RibbonItemDefinition(
        CommandId commandId,
        RibbonItemKind kind,
        bool isLarge = false,
        int order = 0,
        string? automationName = null,
        RibbonItemMeasurementCallback? measurement = null)
        : this(commandId, isLarge, order)
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

        Kind = kind;
        AutomationName = NormalizeOptional(automationName);
        Measurement = measurement;
        UsesLegacyAutomaticToggle = false;
    }

    private RibbonItemDefinition(string separatorId, int order)
        : this(
            new CommandId($"ribbon.separator.{NormalizeSeparatorId(separatorId)}"),
            false,
            order)
    {
        Kind = RibbonItemKind.Separator;
        AutomationName = "Dấu phân cách";
        Measurement = static _ => 8d;
        UsesLegacyAutomaticToggle = false;
    }

    public RibbonItemKind Kind { get; private init; } = RibbonItemKind.Button;

    public string? AutomationName { get; private init; }

    public RibbonItemMeasurementCallback? Measurement { get; private init; }

    internal bool UsesLegacyAutomaticToggle { get; private init; } = true;

    /// <summary>Creates a non-command separator with stable identity.</summary>
    public static RibbonItemDefinition Separator(string id, int order = 0) =>
        new(id, order);

    internal RibbonItemDefinition WithLayout(bool isLarge, int order) =>
        this with { IsLarge = isLarge, Order = order };

    private static string NormalizeSeparatorId(string separatorId)
    {
        if (string.IsNullOrWhiteSpace(separatorId))
        {
            throw new ArgumentException(
                "A Ribbon separator id is required.",
                nameof(separatorId));
        }
        return separatorId.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Structured activation payload for a selectable Ribbon item.</summary>
public sealed record RibbonItemActivation(
    string? SelectedValue,
    object? OriginalParameter = null);
