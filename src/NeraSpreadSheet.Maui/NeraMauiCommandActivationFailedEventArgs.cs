using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Maui;

public sealed class NeraMauiCommandActivationFailedEventArgs : EventArgs
{
    public NeraMauiCommandActivationFailedEventArgs(
        CommandId commandId,
        Exception exception)
    {
        CommandId = commandId;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public CommandId CommandId { get; }

    public Exception Exception { get; }
}
