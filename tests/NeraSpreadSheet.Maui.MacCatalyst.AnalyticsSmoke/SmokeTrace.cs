using System.Globalization;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

internal static class SmokeTrace
{
    internal const string FileName = "nera-maccatalyst-analytics-smoke.trace";

    internal static string PathName => Path.Combine(Path.GetTempPath(), FileName);

    internal static void Reset()
    {
        var path = PathName;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        Append("trace-reset");
    }

    internal static void Append(string stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        var path = PathName;
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "The Mac Catalyst smoke trace file has no parent directory."));
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.UtcNow:O}|pid={Environment.ProcessId}|thread={Environment.CurrentManagedThreadId}|{stage}{Environment.NewLine}");
        File.AppendAllText(path, line);
    }
}
