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
    private const string SmokeResultEnvironmentVariable = "NERA_MAUI_SMOKE_RESULT";
    private const string SmokeTraceFileName = "nera-maccatalyst-analytics-smoke.trace";
    private const int MaximumFailureTraceLength = 12_000;
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

        var detail = exception.ToString()
            .Replace('\r', ' ')
            .Replace('\n', '|');
        if (detail.Length > MaximumFailureTraceLength)
        {
            detail = detail[..MaximumFailureTraceLength];
        }
        TraceStage($"managed-failure-detail:{detail}");
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

    /// <summary>
    /// Appends a low-level renderer breadcrumb only for the loaded Mac Catalyst
    /// smoke. A native SIGSEGV can bypass all managed exception hooks, so these
    /// breadcrumbs identify the last native Metal call that returned. Normal
    /// application runs pay no file-I/O cost because the smoke result variable
    /// is absent.
    /// </summary>
    internal static void TraceStage(string stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(SmokeResultEnvironmentVariable)))
        {
            return;
        }

        try
        {
            var path = Path.Combine(Path.GetTempPath(), SmokeTraceFileName);
            File.AppendAllText(
                path,
                $"{DateTime.UtcNow:O}|pid={Environment.ProcessId}|thread={Environment.CurrentManagedThreadId}|metal:{stage}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never replace or perturb the renderer failure.
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
