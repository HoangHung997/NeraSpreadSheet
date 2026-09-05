using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Packaged.Maui.Smoke;

internal static class CohortIdentity
{
    public const string SourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string Version = "0.1.0-ci.123.1.gaaaaaaaaaaaa";
    public const string FeedHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string Nonce = "cccccccccccccccccccccccccccccccc";
    public const string Platform = "ios";
}

internal static class Program
{
    private const string Prefix = "NERA_PACKAGED_MAUI_SMOKE:";
    private const string Protocol = "native-result-file-v1";
    private const string TransportNonce = "dddddddddddddddddddddddddddddddd";
    private const string ProtocolVariable = "NERA_MAUI_SMOKE_PROTOCOL";
    private const string NonceVariable = "NERA_MAUI_SMOKE_NONCE";
    private const string PathVariable = "NERA_MAUI_SMOKE_RESULT";
    private static readonly string Description = string.Concat(Enumerable.Repeat("Dữ liệu kiểm chứng 🧪 ", 300));
    private static readonly string[] AssemblyNames = ["Maui", "Core", "Editing", "Formulas", "Rendering.Skia", "Ribbon.Core"];

    private static int Main()
    {
        string[] variables = [ProtocolVariable, NonceVariable, PathVariable];
        var previous = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? string.Empty;
            Require(Environment.GetEnvironmentVariable("CI") == "true" && Path.IsPathFullyQualified(runnerTemp),
                "Emission fixtures require an isolated CI runner.");
            var root = Path.Combine(runnerTemp, "nera-maui-emission-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DefaultShouldRetainFullMarker(root);
            FileModeShouldBindCompletePayload(root);
            SharedParserShouldVerifyActualConsumerEmission(root);
            FileModeShouldRefuseExistingAndRepeatedEvidence(root);
            FileModeShouldRejectInvalidConfiguration(root);
            FileModeShouldPreserveFailureStatus(root);
            Console.WriteLine("Actual consumer emission fixtures passed.");
            return 0;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or JsonException or ArgumentException)
        {
            // The runner boundary reports the failure category without private fixture paths.
            Console.Error.WriteLine("Emission fixture rejected: " + error.GetType().Name);
            return 1;
        }
        finally
        {
            for (var index = 0; index < variables.Length; index++)
                Environment.SetEnvironmentVariable(variables[index], previous[index]);
        }
    }

    private static object Details() => new
    {
        description = Description, publicApiOnly = true, controllerEditUndo = true,
        actualResize = true, filterValues = 20,
        gpu = new { FramesFailed = 0, FramesCompleted = 3, HasActiveFrame = false },
        assemblies = AssemblyNames
            .Select(name => new { name = "NeraSpreadSheet." + name,
                informationalVersion = CohortIdentity.Version + "+" + CohortIdentity.SourceSha }).ToArray(),
    };

    private static void Configure(string? protocol, string? nonce, string? path)
    {
        Environment.SetEnvironmentVariable(ProtocolVariable, protocol);
        Environment.SetEnvironmentVariable(NonceVariable, nonce);
        Environment.SetEnvironmentVariable(PathVariable, path);
    }

    private static string Capture(Action action)
    {
        var original = Console.Out;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            action();
            return output.ToString();
        }
        finally { Console.SetOut(original); }
    }

    private static JsonDocument Marker(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Require(lines.Length == 1 && lines[0].StartsWith(Prefix, StringComparison.Ordinal),
            "Expected exactly one prefixed marker.");
        return JsonDocument.Parse(lines[0][Prefix.Length..]);
    }

    private static void DefaultShouldRetainFullMarker(string root)
    {
        Console.WriteLine(nameof(DefaultShouldRetainFullMarker));
        Configure(null, null, null);
        var text = Capture(() => PackageProvenance.Emit("success", 3, Details()));
        using var marker = Marker(text);
        CheckFullPayload(marker.RootElement, "success");
        Require(text.Length > 1024 && !marker.RootElement.TryGetProperty("transportNonce", out _),
            "Default transport changed its full marker.");
        var path = Path.Combine(root, "legacy.json");
        File.WriteAllText(path, "existing legacy output");
        Configure(null, null, path);
        var fileText = Capture(() => PackageProvenance.Emit("success", 3, Details()));
        Require(fileText == text && File.ReadAllText(path) == text.Trim()[Prefix.Length..],
            "Legacy optional output behavior changed.");
    }

    private static void FileModeShouldBindCompletePayload(string root)
    {
        Console.WriteLine(nameof(FileModeShouldBindCompletePayload));
        var path = Path.Combine(root, "complete.json");
        Configure(Protocol, TransportNonce, path);
        var text = Capture(() => PackageProvenance.Emit("success", 3, Details()));
        var bytes = File.ReadAllBytes(path);
        Require(bytes.Length > 4096 && bytes[0] == (byte)'{' && text.Length < 512,
            "Expected a full UTF8 file and a compact marker.");
        using var full = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(bytes));
        CheckFullPayload(full.RootElement, "success");
        using var marker = Marker(text);
        var envelope = marker.RootElement;
        string[] fields = ["frameCount", "schema", "sha256", "status", "transportNonce"];
        Require(envelope.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(fields), "Compact envelope fields differ from the released protocol.");
        Require(envelope.GetProperty("schema").GetString() == Protocol &&
            envelope.GetProperty("transportNonce").GetString() == TransportNonce &&
            envelope.GetProperty("status").GetString() == "success" &&
            envelope.GetProperty("frameCount").GetInt32() == 3 &&
            envelope.GetProperty("sha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(bytes)),
            "Envelope does not bind the complete file.");
        // Reopening exclusively proves the producer closed its file before returning the marker.
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Require(exclusive.Length == bytes.Length, "The completed file was not stable.");
    }

    private static void FileModeShouldRefuseExistingAndRepeatedEvidence(string root)
    {
        Console.WriteLine(nameof(FileModeShouldRefuseExistingAndRepeatedEvidence));
        var path = Path.Combine(root, "existing.json");
        File.WriteAllText(path, "previous evidence");
        Configure(Protocol, TransportNonce, path);
        var text = Capture(() => ExpectException<IOException>(() => PackageProvenance.Emit("success", 3, Details())));
        Require(text.Length == 0 && File.ReadAllText(path) == "previous evidence", "Existing evidence was reused.");
        path = Path.Combine(root, "repeated.json");
        Configure(Protocol, TransportNonce, path);
        Capture(() => PackageProvenance.Emit("success", 3, Details()));
        var original = File.ReadAllBytes(path);
        text = Capture(() => ExpectException<IOException>(() => PackageProvenance.Emit("failure", 3, Details())));
        Require(text.Length == 0 && File.ReadAllBytes(path).SequenceEqual(original), "Second emission overwrote evidence.");
    }

    private static void SharedParserShouldVerifyActualConsumerEmission(string root)
    {
        Console.WriteLine(nameof(SharedParserShouldVerifyActualConsumerEmission));
        var payloadPath = Path.Combine(root, "interop.json");
        Configure(Protocol, TransportNonce, payloadPath);
        var marker = Capture(() => PackageProvenance.Emit("success", 3, Details()));
        var context = Path.Combine(root, "context.json");
        File.WriteAllBytes(context, JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "native-result-file-context-v1", path = payloadPath, transportNonce = TransportNonce,
        }));
        var console = Path.Combine(root, "console.log");
        File.WriteAllText(console, marker);
        var unified = Path.Combine(root, "unified.json");
        File.WriteAllBytes(unified, JsonSerializer.SerializeToUtf8Bytes(new[] { new { eventMessage = marker.Trim() } }));
        var output = Path.Combine(root, "verified.json");
        var repository = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? string.Empty;
        Require(Path.IsPathFullyQualified(repository), "Missing isolated fixture repository.");
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        string[] arguments = ["-B", Path.Combine(repository, "scripts", "verify-native-smoke-result.py"),
            "--log", console, "--json-log", unified, "--prefix", Prefix, "--file-context", context,
            "--minimum-frames", "3", "--output", output];
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Shared verifier did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Shared verifier exceeded its fixture timeout.");
        }
        Task.WaitAll(standardOutput, standardError);
        Require(process.ExitCode == 0 && File.Exists(output), "Shared verifier rejected actual consumer emission.");
        using var verified = JsonDocument.Parse(File.ReadAllBytes(output));
        CheckFullPayload(verified.RootElement, "success");
    }

    private static void FileModeShouldRejectInvalidConfiguration(string root)
    {
        Console.WriteLine(nameof(FileModeShouldRejectInvalidConfiguration));
        var path = Path.Combine(root, "invalid.json");
        (string? Protocol, string? Nonce, string? Path)[] cases =
        [
            ("unknown-protocol", TransportNonce, path),
            (Protocol, null, path), (Protocol, "short", path),
            (Protocol, new string('D', 32), path), (Protocol, new string('g', 32), path),
            (Protocol, TransportNonce, null), (Protocol, TransportNonce, "relative.json"),
        ];
        foreach (var configuration in cases)
        {
            Configure(configuration.Protocol, configuration.Nonce, configuration.Path);
            var text = Capture(() => ExpectException<InvalidOperationException>(() => PackageProvenance.Emit("success", 3, Details())));
            Require(text.Length == 0 && !File.Exists(path), "Invalid transport configuration emitted evidence.");
        }
    }

    private static void FileModeShouldPreserveFailureStatus(string root)
    {
        Console.WriteLine(nameof(FileModeShouldPreserveFailureStatus));
        var path = Path.Combine(root, "failure.json");
        Configure(Protocol, TransportNonce, path);
        using var marker = Marker(Capture(() => PackageProvenance.Emit("failure", 3, Details())));
        using var full = JsonDocument.Parse(File.ReadAllBytes(path));
        CheckFullPayload(full.RootElement, "failure");
        Require(marker.RootElement.GetProperty("status").GetString() == "failure", "Failure marker was promoted.");
    }

    private static void CheckFullPayload(JsonElement full, string status)
    {
        Require(full.GetProperty("schema").GetString() == "release009-maui-consumer-v1" &&
            full.GetProperty("status").GetString() == status && full.GetProperty("frameCount").GetInt32() == 3 &&
            full.GetProperty("sourceSha").GetString() == CohortIdentity.SourceSha &&
            full.GetProperty("packageVersion").GetString() == CohortIdentity.Version &&
            full.GetProperty("feedHash").GetString() == CohortIdentity.FeedHash &&
            full.GetProperty("nonce").GetString() == CohortIdentity.Nonce &&
            full.GetProperty("target").GetString() == CohortIdentity.Platform &&
            full.GetProperty("details").GetProperty("description").GetString() == Description &&
            full.GetProperty("details").GetProperty("assemblies").GetArrayLength() == 6 &&
            full.GetProperty("nativeEditorCoverage").GetString()?.StartsWith("OPEN:", StringComparison.Ordinal) == true,
            "Full consumer payload lost cohort or public evidence.");
    }

    private static void ExpectException<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException("Expected rejection did not occur.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
