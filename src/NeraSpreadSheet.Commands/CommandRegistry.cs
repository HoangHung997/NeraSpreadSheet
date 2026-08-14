namespace NeraSpreadSheet.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<CommandId, Entry> _entries = new();

    public int Count => _entries.Count;

    public void Register(CommandDescriptor descriptor, ICommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_entries.TryAdd(descriptor.Id, new Entry(descriptor, handler)))
        {
            throw new InvalidOperationException($"Command '{descriptor.Id}' is already registered.");
        }
    }

    public bool TryResolve(
        CommandId id,
        out CommandDescriptor? descriptor,
        out ICommandHandler? handler)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            descriptor = entry.Descriptor;
            handler = entry.Handler;
            return true;
        }

        descriptor = null;
        handler = null;
        return false;
    }

    public bool Unregister(CommandId id) => _entries.Remove(id);

    private sealed record Entry(CommandDescriptor Descriptor, ICommandHandler Handler);
}
