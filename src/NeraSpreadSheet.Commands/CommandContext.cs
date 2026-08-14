namespace NeraSpreadSheet.Commands;

public readonly record struct CommandContext(
    IServiceProvider? Services = null,
    object? Parameter = null,
    CancellationToken CancellationToken = default);
