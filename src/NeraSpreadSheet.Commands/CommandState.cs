namespace NeraSpreadSheet.Commands;

public readonly record struct CommandState(
    bool IsEnabled,
    bool? IsChecked = null,
    string? DisplayText = null)
{
    public static CommandState Enabled { get; } = new(true);

    public static CommandState Disabled { get; } = new(false);
}

public interface IStatefulCommandHandler : ICommandHandler
{
    CommandState GetState(CommandContext context);
}
