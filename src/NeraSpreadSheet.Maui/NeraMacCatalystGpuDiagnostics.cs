#if MACCATALYST
using System.Runtime.CompilerServices;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Keeps the last Mac Catalyst native-Metal boundary failure attached to the
/// owning Nera view. The state is weakly keyed so diagnostics never extend a
/// view lifetime. Loaded runtime smokes can inspect this without exposing a new
/// public application API.
/// </summary>
internal static class NeraMacCatalystGpuDiagnostics
{
    private static readonly ConditionalWeakTable<NeraSpreadsheetView, FailureState> States = new();

    internal static void Clear(NeraSpreadsheetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        States.Remove(view);
    }

    internal static void RecordFailure(NeraSpreadsheetView view, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(exception);
        var state = States.GetValue(view, static _ => new FailureState());
        lock (state.Sync)
        {
            state.Exception = exception;
            state.Sequence = checked(state.Sequence + 1L);
        }
    }

    internal static Exception? GetLastFailure(NeraSpreadsheetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!States.TryGetValue(view, out var state))
        {
            return null;
        }

        lock (state.Sync)
        {
            return state.Exception;
        }
    }

    private sealed class FailureState
    {
        internal object Sync { get; } = new();
        internal Exception? Exception { get; set; }
        internal long Sequence { get; set; }
    }
}
#endif
