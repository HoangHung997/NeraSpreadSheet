using System.Text.Json;
using ObjCRuntime;
using UIKit;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

public static class Program
{
    private const string ResultArgument = "--nera-smoke-result";
    private const string ResultEnvironmentVariable = "NERA_MAUI_SMOKE_RESULT";
    private const string DefaultResultFileName = "nera-maccatalyst-analytics-smoke.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
    private static string? _emergencyResultPath;
    private static int _emergencyResultWritten;

    public static void Main(string[] args)
    {
        _emergencyResultPath = ResolveResultPath(args);
        InstallEmergencyDiagnostics();

        try
        {
            UIApplication.Main(args, null, typeof(SmokeApplicationHost));
        }
        catch (Exception exception)
        {
            WriteEmergencyResult("UIApplication.Main", exception.ToString());
            throw;
        }
    }

    private static void InstallEmergencyDiagnostics()
    {
        AppDomain.CurrentDomain.UnhandledException += static (_, eventArgs) =>
            WriteEmergencyResult(
                "AppDomain.UnhandledException",
                eventArgs.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");

        TaskScheduler.UnobservedTaskException += static (_, eventArgs) =>
            WriteEmergencyResult(
                "TaskScheduler.UnobservedTaskException",
                eventArgs.Exception.ToString());

        Runtime.MarshalObjectiveCException += static (_, eventArgs) =>
            WriteEmergencyResult(
                $"ObjCRuntime.MarshalObjectiveCException:{eventArgs.ExceptionMode}",
                eventArgs.Exception.ToString());

        Runtime.MarshalManagedException += static (_, eventArgs) =>
            WriteEmergencyResult(
                $"ObjCRuntime.MarshalManagedException:{eventArgs.ExceptionMode}",
                eventArgs.Exception.ToString());
    }

    private static void WriteEmergencyResult(string stage, string error)
    {
        if (Interlocked.CompareExchange(ref _emergencyResultWritten, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var path = _emergencyResultPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Path.GetTempPath(), DefaultResultFileName);
            }

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException(
                    "The Mac Catalyst emergency result file has no parent directory."));
            File.WriteAllText(
                fullPath,
                JsonSerializer.Serialize(
                    new
                    {
                        status = "failure",
                        emergency = true,
                        stage,
                        error,
                    },
                    JsonOptions));
        }
        catch
        {
            // The smoke process may already be unwinding through an unsafe native
            // boundary. Never replace the original failure with diagnostic I/O.
        }
    }

    private static string ResolveResultPath(string[] args)
    {
        var environmentPath = Environment.GetEnvironmentVariable(ResultEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return environmentPath;
        }

        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], ResultArgument, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return args[index + 1];
            }
        }

        return Path.Combine(Path.GetTempPath(), DefaultResultFileName);
    }
}
