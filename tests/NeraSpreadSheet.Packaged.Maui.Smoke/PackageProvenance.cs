using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "release009-maui-consumer-v1", status, sourceSha = CohortIdentity.SourceSha,
            packageVersion = CohortIdentity.Version, feedHash = CohortIdentity.FeedHash,
            nonce = CohortIdentity.Nonce, target = CohortIdentity.Platform, frameCount, details,
            nativeEditorCoverage = "OPEN: requires the released TABLE-007 public editor bridge",
        });
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT") ?? string.Empty;
        var protocol = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_PROTOCOL");
        string json;
        if (protocol is not null)
        {
            Require(protocol == "native-result-file-v1", "Unknown native result protocol.");
            var transportNonce = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_NONCE") ?? string.Empty;
            Require(transportNonce.Length == 32 && transportNonce.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'), "Invalid native transport nonce.");
            Require(Path.IsPathFullyQualified(path), "Native result file requires an absolute path.");
            // The launcher owns the fresh container path. Publish the envelope only after durable close.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            json = JsonSerializer.Serialize(new
            {
                schema = protocol, status, frameCount, transportNonce,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            });
        }
        else
        {
            json = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, json);
        }
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
