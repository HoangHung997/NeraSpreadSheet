using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Bars.Core;

public sealed record BarItemDefinition
{
    private BarItemDefinition(
        BarItemKind kind,
        CommandId? commandId,
        string? caption,
        IReadOnlyList<BarItemDefinition> children)
    {
        Kind = kind;
        CommandId = commandId;
        Caption = caption;
        Children = children;
    }

    public BarItemKind Kind { get; }

    public CommandId? CommandId { get; }

    public string? Caption { get; }

    public IReadOnlyList<BarItemDefinition> Children { get; }

    public static BarItemDefinition Command(CommandId commandId) =>
        new(BarItemKind.Command, commandId, null, Array.Empty<BarItemDefinition>());

    public static BarItemDefinition Separator() =>
        new(BarItemKind.Separator, null, null, Array.Empty<BarItemDefinition>());

    public static BarItemDefinition Submenu(string caption, IEnumerable<BarItemDefinition> children)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A submenu caption is required.", nameof(caption));
        }

        return new BarItemDefinition(
            BarItemKind.Submenu,
            null,
            caption.Trim(),
            (children ?? throw new ArgumentNullException(nameof(children))).ToArray());
    }
}
