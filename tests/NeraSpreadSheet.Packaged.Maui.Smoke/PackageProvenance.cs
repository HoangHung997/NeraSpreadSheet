using System.Reflection;
using System.Text.Json;

namespace Packaged.Maui.Smoke;

internal static class PackageProvenance
{
    public static object[] VerifyLoadedAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly != typeof(PackageProvenance).Assembly &&
                assembly.GetName().Name?.StartsWith("NeraSpreadSheet.", StringComparison.Ordinal) == true)
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).ToArray();
        string[] required = ["NeraSpreadSheet.Maui", "NeraSpreadSheet.Core", "NeraSpreadSheet.Editing",
            "NeraSpreadSheet.Formulas", "NeraSpreadSheet.Rendering.Skia", "NeraSpreadSheet.Ribbon.Core"];
        foreach (var name in required)
            Require(assemblies.Any(assembly => assembly.GetName().Name == name), "Required packaged SDK assembly was not loaded.");
        return assemblies.Select(assembly =>
        {
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Require(version == CohortIdentity.Version + "+" + CohortIdentity.SourceSha, "Loaded SDK provenance mismatch.");
            return (object)new { name = assembly.GetName().Name, informationalVersion = version };
        }).ToArray();
    }

    public static void Emit(string status, int frameCount, object details)
    {
        var json = JsonSerializer.Serialize(new
        {
            schema = "release009-maui-consumer-v1", status, sourceSha = CohortIdentity.SourceSha,
            packageVersion = CohortIdentity.Version, feedHash = CohortIdentity.FeedHash,
            nonce = CohortIdentity.Nonce, target = CohortIdentity.Platform, frameCount, details,
            nativeEditorCoverage = "OPEN: requires the released TABLE-007 public editor bridge",
        });
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, json);
        Console.WriteLine("NERA_PACKAGED_MAUI_SMOKE:" + json);
#if ANDROID
        Android.Util.Log.Info("NeraPackagedMauiSmoke", "NERA_PACKAGED_MAUI_SMOKE:" + json);
#endif
        Console.Out.Flush();
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
