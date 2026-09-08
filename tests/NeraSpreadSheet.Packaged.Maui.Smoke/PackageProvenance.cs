using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Packaged.Maui.Smoke;

internal static class PackageProvenance
{
#if MACCATALYST
    private static int _macDiagnosticCount;
#endif

    // The same finite formatter is linked into the hosted fixture; it never accepts free-form data.
    internal static string? FormatMacDiagnostic(string stage, string? protocol, string? transportNonce,
        bool pathIsAbsolute, bool parentExists)
    {
        if (protocol != "native-result-file-v1" || transportNonce is null || transportNonce.Length != 32 ||
            !transportNonce.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) return null;
        if (stage is not ("constructorEntered" or "constructorCompleted" or "loadedEntered" or
            "dispatchAccepted" or "dispatchRejected" or "dispatchCallbackEntered" or "runEntered" or
            "nativeFramesCompleted" or "controllerCompleted" or "filterCompleted" or "resizeCompleted" or
            "gpuCompleted" or "provenanceCompleted" or "disposeCompleted" or "failureCaught" or
            "emitEntered" or "contextValidated" or "payloadClosed" or "envelopePublished")) return null;
        return "NERA_PACKAGED_MAUI_DIAGNOSTIC:" + JsonSerializer.Serialize(new
        {
            schema = "native-package-diagnostic-v1", stage, transportNonce, pathIsAbsolute, parentExists,
        });
    }

    [System.Diagnostics.Conditional("MACCATALYST")]
    internal static void TraceMac(string stage)
    {
#if MACCATALYST
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT") ?? string.Empty;
        var absolute = Path.IsPathFullyQualified(path);
        var message = FormatMacDiagnostic(stage, Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_PROTOCOL"),
            Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_NONCE"), absolute,
            absolute && Directory.Exists(Path.GetDirectoryName(path)));
        if (message is not null && Interlocked.Increment(ref _macDiagnosticCount) <= 64)
            CoreFoundation.OSLog.Default.Log(CoreFoundation.OSLogLevel.Default, message);
#endif
    }

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
        TraceMac("emitEntered");
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
            TraceMac("contextValidated");
            // The launcher owns the fresh container path. Publish the envelope only after durable close.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            TraceMac("payloadClosed");
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
#if MACCATALYST
        if (protocol == "native-result-file-v1")
            CoreFoundation.OSLog.Default.Log(CoreFoundation.OSLogLevel.Default, "NERA_PACKAGED_MAUI_SMOKE:" + json);
#endif
        Console.Out.Flush();
        TraceMac("envelopePublished");
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
