namespace NeraSpreadSheet.Commands;

public sealed record CommandDescriptor
{
    public CommandDescriptor(
        CommandId id,
        string caption,
        string? tooltip = null,
        string? iconKey = null,
        string? shortcut = null)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentException("A command caption is required.", nameof(caption));
        }

        Id = id;
        Caption = caption.Trim();
        Tooltip = tooltip;
        IconKey = iconKey;
        Shortcut = shortcut;
    }

    public CommandId Id { get; }

    public string Caption { get; }

    public string? Tooltip { get; }

    public string? IconKey { get; }

    public string? Shortcut { get; }
}
