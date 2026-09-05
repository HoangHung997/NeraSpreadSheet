using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Bars.Core;

public sealed record BarItemDefinition
{
    private BarItemDefinition(
        BarItemKind kind,
        CommandId? commandId,
        string? caption,
        IReadOnlyList<BarItemDefinition> children,
        string? id,
        int order)
    {
        Kind = kind;
        CommandId = commandId;
        Caption = caption;
        Children = children;
        Id = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        Order = order;
    }

    public BarItemKind Kind { get; }

    public CommandId? CommandId { get; }

    public string? Caption { get; }

    public IReadOnlyList<BarItemDefinition> Children { get; }

    /// <summary>
    /// Gets the stable customization id. Commands default to their command id.
    /// Separators and submenus require an explicit id to be customizable.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the stable sort order used within the containing bar or submenu.
    /// </summary>
    public int Order { get; }

    public static BarItemDefinition Command(
        CommandId commandId,
        string? id = null,
        int order = 0) =>
        new(
            BarItemKind.Command,
            commandId,
            null,
            Array.Empty<BarItemDefinition>(),
            string.IsNullOrWhiteSpace(id) ? commandId.Value : id,
            order);

    public static BarItemDefinition Separator(string? id = null, int order = 0) =>
        new(
            BarItemKind.Separator,
            null,
            null,
            Array.Empty<BarItemDefinition>(),
            id,
            order);

    public static BarItemDefinition Submenu(
        string caption,
        IEnumerable<BarItemDefinition> children,
        string? id = null,
        int order = 0)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A submenu caption is required.", nameof(caption));
        }

        return new BarItemDefinition(
            BarItemKind.Submenu,
            null,
            caption.Trim(),
            (children ?? throw new ArgumentNullException(nameof(children)))
                .OrderBy(item => item.Order)
                .ToArray(),
            id,
            order);
    }
}
