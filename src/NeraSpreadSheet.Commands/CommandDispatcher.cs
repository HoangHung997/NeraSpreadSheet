namespace NeraSpreadSheet.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry _registry;

    public CommandDispatcher(CommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public CommandState QueryState(CommandId id, CommandContext context = default)
    {
        if (!_registry.TryResolve(id, out _, out var handler) || handler is null)
        {
            return CommandState.Disabled;
        }

        if (handler is IStatefulCommandHandler stateful)
        {
            return stateful.GetState(context);
        }

        return new CommandState(handler.CanExecute(context));
    }

    public async ValueTask<bool> TryExecuteAsync(CommandId id, CommandContext context = default)
    {
        if (!_registry.TryResolve(id, out _, out var handler) || handler is null || !handler.CanExecute(context))
        {
            return false;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        await handler.ExecuteAsync(context).ConfigureAwait(false);
        return true;
    }
}
