namespace NeraSpreadSheet.Commands;

public interface ICommandHandler
{
    bool CanExecute(CommandContext context);

    ValueTask ExecuteAsync(CommandContext context);
}
