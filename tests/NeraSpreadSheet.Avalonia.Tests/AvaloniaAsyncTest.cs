namespace NeraSpreadSheet.Avalonia.Tests;

/// <summary>Starts asynchronous test bodies on the existing UI dispatcher without
/// blocking it. Exceptions remain observable by MSTest; final posted work is drained.</summary>
internal static class AvaloniaAsyncTest
{
    public static async Task Run(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Task? operation = null;
        await AvaloniaTestEnvironment.OnUiAsync(() => operation = action());
        await (operation ?? throw new InvalidOperationException("The UI test did not start."));
        await AvaloniaTestEnvironment.OnUiAsync(static () => { });
    }
}
