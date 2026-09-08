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
    private const string ChildModeVariable = "NERA_MAUI_LAUNCHER_FIXTURE";
    private static readonly string Description = string.Concat(Enumerable.Repeat("Dữ liệu kiểm chứng 🧪 ", 300));
    private static readonly string[] AssemblyNames = ["Maui", "Core", "Editing", "Formulas", "Rendering.Skia", "Ribbon.Core"];
    private static readonly byte[] UnsignedFixtureBytes = [0, 1, 2];

    private static int Main()
    {
        string[] variables = [ProtocolVariable, NonceVariable, PathVariable];
        var previous = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? string.Empty;
            Require(Environment.GetEnvironmentVariable("CI") == "true" && Path.IsPathFullyQualified(runnerTemp),
                "Emission fixtures require an isolated CI runner.");
            var childMode = Environment.GetEnvironmentVariable(ChildModeVariable);
            if (childMode is not null) return RunSyntheticChild(childMode);
            var root = Path.Combine(runnerTemp, "nera-maui-emission-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DefaultShouldRetainFullMarker(root);
            FileModeShouldBindCompletePayload(root);
            SharedParserShouldVerifyActualConsumerEmission(root);
            FileModeShouldRefuseExistingAndRepeatedEvidence(root);
            FileModeShouldRejectInvalidConfiguration(root);
            FileModeShouldPreserveFailureStatus(root);
            MacDiagnosticsShouldBeFinitePrivateAndNeverAuthorizeResults(root);
            WindowsLauncherShouldEnforceCompleteSingleAttemptEvidence(root);
            MacLauncherShouldRejectInvalidIdentityAndUnsignedPayload(root);
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

    private static int RunSyntheticChild(string mode)
    {
        if (mode == "missing") return 0;
        if (mode == "timeout") { Thread.Sleep(20_000); return 0; }
        if (mode == "oversized") Console.Write(new string('x', 2 * 1024 * 1024 + 1));
        if (mode == "pipe-pressure")
        {
            Console.WriteLine(new string('o', 96 * 1024));
            Console.Error.WriteLine(new string('e', 96 * 1024));
        }
        if (mode == "stale-nonce") Environment.SetEnvironmentVariable(NonceVariable, new string('e', 32));
        PackageProvenance.Emit(mode == "failure" ? "failure" : "success", 3, Details());
        if (mode == "bad-marker") Console.WriteLine(Prefix + "{");
        if (mode == "changed-file") File.AppendAllText(Environment.GetEnvironmentVariable(PathVariable)!, " ");
        return mode is "nonzero" or "failure" ? 17 : 0;
    }

    private static int RunTool(string executable, IEnumerable<string> arguments, string? childMode = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (childMode is not null) start.Environment[ChildModeVariable] = childMode;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Fixture tool did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(45_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Fixture tool exceeded its bound.");
        }
        Require(Task.WaitAll([standardOutput, standardError], 5_000), "Fixture tool pipes did not complete.");
        Require(standardOutput.Result.Length + standardError.Result.Length < 32 * 1024,
            "Fixture launcher exposed unbounded diagnostic output.");
        return process.ExitCode;
    }

    private static void MacDiagnosticsShouldBeFinitePrivateAndNeverAuthorizeResults(string root)
    {
        Console.WriteLine(nameof(MacDiagnosticsShouldBeFinitePrivateAndNeverAuthorizeResults));
        string[] stages = ["constructorEntered", "constructorCompleted", "loadedEntered", "dispatchAccepted",
            "dispatchRejected", "dispatchCallbackEntered", "runEntered", "nativeFramesCompleted", "controllerCompleted",
            "filterCompleted", "resizeCompleted", "gpuCompleted", "provenanceCompleted", "disposeCompleted",
            "failureCaught", "emitEntered", "contextValidated", "payloadClosed", "envelopePublished"];
        var messages = stages.Select(stage => PackageProvenance.FormatMacDiagnostic(stage, Protocol,
            TransportNonce, pathIsAbsolute: true, parentExists: false)).ToArray();
        Require(messages.All(message => message is not null && message.Length < 512 &&
            !message.Contains(Description, StringComparison.Ordinal)), "Diagnostic formatter exceeded its finite scope.");
        foreach (var stage in new[] { "", "unknown", "runEntered/private/secret", new string('x', 1024) })
            Require(PackageProvenance.FormatMacDiagnostic(stage, Protocol, TransportNonce, true, true) is null,
                "Diagnostic formatter accepted a free-form stage.");
        foreach (var nonce in new[] { null, "", "short", new string('D', 32), new string('g', 32) })
            Require(PackageProvenance.FormatMacDiagnostic(stages[0], Protocol, nonce, true, true) is null,
                "Diagnostic formatter accepted an invalid nonce.");
        Require(PackageProvenance.FormatMacDiagnostic(stages[0], null, TransportNonce, true, true) is null &&
            PackageProvenance.FormatMacDiagnostic(stages[0], "unknown", TransportNonce, true, true) is null,
            "Diagnostic formatter changed default or unknown protocols.");

        var repository = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!;
        var source = File.ReadAllText(Path.Combine(repository, "scripts", "run-maui-maccatalyst-smoke.sh"));
        var start = source.IndexOf("# BEGIN PACKAGE DIAGNOSTIC SUMMARY", StringComparison.Ordinal);
        var end = source.IndexOf("# END PACKAGE DIAGNOSTIC SUMMARY", StringComparison.Ordinal);
        Require(start >= 0 && end > start, "Missing actual diagnostic summary implementation.");
        var observerStart = source.IndexOf("# BEGIN PACKAGE PROCESS OBSERVER", StringComparison.Ordinal);
        var observerEnd = source.IndexOf("# END PACKAGE PROCESS OBSERVER", StringComparison.Ordinal);
        Require(observerStart >= 0 && observerEnd > observerStart, "Missing actual process lifetime observer.");
        var script = Path.Combine(root, "diagnostic-fixture.py");
        File.WriteAllText(script, "import json, os, sys\n" + source[start..end] + source[observerStart..observerEnd] +
            DiagnosticSummaryFixture + ProcessObserverFixture);
        var inputs = Path.Combine(root, "diagnostic-inputs.json");
        File.WriteAllBytes(inputs, JsonSerializer.SerializeToUtf8Bytes(messages));
        var observerSummary = Path.Combine(root, "observer-summary.json");
        string[] arguments = ["-B", script, inputs, TransportNonce, observerSummary];
        Require(RunTool("python", arguments) == 0, "Actual diagnostic summary failed its privacy or bounds fixture.");

        var payload = Path.Combine(root, "diagnostic-only-payload.json");
        Configure(Protocol, TransportNonce, payload);
        Capture(() => PackageProvenance.Emit("success", 3, Details()));
        var context = Path.Combine(root, "diagnostic-only-context.json");
        File.WriteAllBytes(context, JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "native-result-file-context-v1", path = payload, transportNonce = TransportNonce,
        }));
        var console = Path.Combine(root, "diagnostic-only-console.log");
        File.WriteAllText(console, File.ReadAllText(observerSummary));
        var unified = Path.Combine(root, "diagnostic-only-unified.json");
        File.WriteAllBytes(unified, JsonSerializer.SerializeToUtf8Bytes(messages.Select(message => new { eventMessage = message })));
        var output = Path.Combine(root, "diagnostic-only-verified.json");
        string[] verify = ["-B", Path.Combine(repository, "scripts", "verify-native-smoke-result.py"),
            "--log", console, "--json-log", unified, "--prefix", Prefix, "--file-context", context,
            "--minimum-frames", "3", "--output", output];
        Require(RunTool("python", verify) == 2 && !File.Exists(output),
            "Diagnostic-only log and full file incorrectly authorized a native result.");
    }

    private const string DiagnosticSummaryFixture = """

with open(sys.argv[1], encoding='utf-8') as stream:
    messages = json.load(stream)
nonce = sys.argv[2]
prefix = 'NERA_PACKAGED_MAUI_DIAGNOSTIC:'
def summarize(items):
    data = json.dumps([{'eventMessage': item} for item in items]).encode()
    result = summarize_package_diagnostics(data, nonce)
    encoded = json.dumps(result, separators=(',', ':'))
    assert len(encoded.encode()) <= 2048 and nonce not in encoded and 'private' not in encoded
    assert set(result) == {'schema', 'stages', 'matchingDiagnostics', 'absolutePathObserved',
        'missingParentObserved', 'rejectedDiagnostics', 'invalidLogData', 'inputClipped'}
    assert all(type(count) is int and 0 <= count <= 64 for count in result['stages'].values())
    return result
positive = summarize(messages)
assert positive['matchingDiagnostics'] and positive['absolutePathObserved'] and positive['missingParentObserved']
assert all(count == 1 for count in positive['stages'].values())
assert not positive['rejectedDiagnostics'] and not positive['inputClipped']
original = json.loads(messages[0][len(prefix):])
for key, value in [('stage', 'private-unknown'), ('transportNonce', 'e' * 32), ('pathIsAbsolute', 1),
                   ('parentExists', 0), ('schema', 'unknown'), ('stage', []), ('extra', '/private/secret')]:
    record = dict(original)
    record[key] = value
    rejected = summarize([prefix + json.dumps(record)])
    assert rejected['rejectedDiagnostics'] and not rejected['matchingDiagnostics']
for message in [prefix + '{}', prefix + 'null', prefix + '{', 'private ' + messages[0],
                messages[0] + ' private', prefix + '{"stage":"private","stage":"runEntered"}',
                prefix + 'x' * 1024]:
    rejected = summarize([message])
    assert rejected['rejectedDiagnostics'] and not rejected['matchingDiagnostics']
assert summarize([messages[0]] * 129)['inputClipped']
assert summarize([messages[0]] * 129)['stages']['constructorEntered'] == 64
assert summarize(['NERA_PACKAGED_MAUI_SMOKE:{"status":"success"}'])['matchingDiagnostics'] is False
assert summarize_package_diagnostics(b'x' * (2 * 1024 * 1024 + 1), nonce)['inputClipped']
for data in [b'{', b'{}', b'null', b'\xff', b'[' * 1100 + b']' * 1100]:
    assert summarize_package_diagnostics(data, nonce)['invalidLogData']
""";

    private const string ProcessObserverFixture = """

from types import SimpleNamespace
observer_cases = 0
target_pid = 987654321
class Event:
    def __init__(self, ident=target_pid, filter=-5, flags=0, fflags=0x80000000, error=None):
        self.ident, self.filter, self.flags, self.fflags = ident, filter, flags, fflags
        self.error = error
    @property
    def data(self):
        assert self.flags & 0x4000, 'Normal exit data must never be inspected.'
        return self.error
class Queue:
    def __init__(self, results, timeline):
        self.results, self.timeline, self.calls = list(results), timeline, []
        self.closed = False
    def control(self, changes, maximum, timeout):
        assert not self.closed and maximum == 1 and timeout == 0
        if not self.calls:
            assert len(changes) == 1 and changes[0].ident == target_pid
            assert changes[0].filter == -5 and changes[0].flags == 0x11 and changes[0].fflags == 0x80000000
        else:
            assert changes is None
        self.calls.append(changes)
        self.timeline.append('poll')
        result = self.results.pop(0) if self.results else []
        if isinstance(result, Exception): raise result
        return result
    def close(self):
        assert not self.closed
        self.closed = True
        self.timeline.append('close')
class Backend:
    KQ_FILTER_PROC, KQ_NOTE_EXIT = -5, 0x80000000
    KQ_EV_ADD, KQ_EV_ONESHOT, KQ_EV_ERROR, KQ_EV_EOF = 1, 0x10, 0x4000, 0x8000
    def __init__(self, queue): self.queue = queue
    def kqueue(self): return self.queue
    def kevent(self, ident, **kwargs): return Event(ident=ident, **kwargs)
class Probe:
    def __init__(self, results, timeline): self.results, self.timeline, self.calls = list(results), timeline, []
    def __call__(self, ident, signal):
        assert ident == target_pid and signal == 0
        self.calls.append((ident, signal))
        self.timeline.append('probe')
        result = self.results.pop(0) if self.results else None
        if isinstance(result, Exception): raise result
def create(events, probes=()):
    timeline = []
    queue, probe = Queue(events, timeline), Probe(probes, timeline)
    return PackageProcessLifetimeObserver(target_pid, Backend(queue), probe), queue, probe, timeline
expected_keys = {'schema', 'watchRegistered', 'registrationAbsent', 'watchDenied', 'watchUnavailable',
    'observerError', 'exitEventObserved', 'pidAbsentObserved', 'livenessDenied', 'finalLivenessChecked',
    'pidPresentAtFinalCheck', 'finalObservationBeforeCleanup', 'pollLimitReached', 'pollCount'}
def finish(observer):
    global observer_cases
    observer_cases += 1
    result = observer.finish()
    encoded = json.dumps(result, separators=(',', ':'))
    largest_stages = dict(positive, stages=dict.fromkeys(positive['stages'], 64))
    combined = encoded + '\n' + json.dumps(largest_stages, separators=(',', ':')) + '\n'
    assert len(combined.encode()) <= 2048 and str(target_pid) not in encoded and 'private' not in encoded
    assert nonce not in encoded and set(result) == expected_keys
    assert result['schema'] == 'nativePackageProcessLifetimeV1'
    assert type(result['pollCount']) is int and 0 <= result['pollCount'] <= 128
    assert all(type(value) is bool for key, value in result.items() if key not in ('schema', 'pollCount'))
    assert observer.finish() == result
    observer.observe()
    assert observer.finish() == result
    return result

observer, queue, probe, timeline = create([[Event()]])
immediate = finish(observer)
assert immediate['watchRegistered'] and immediate['exitEventObserved'] and queue.closed
observer, queue, probe, timeline = create([[], [], [Event(flags=0x8010)]])
observer.observe()
later = finish(observer)
assert later['exitEventObserved'] and later['finalObservationBeforeCleanup'] and queue.closed
snapshot = dict(later)
timeline.append('cleanup')
assert timeline[-2:] == ['close', 'cleanup']
observer.observe()
assert observer.finish() == snapshot and len(queue.calls) == 3

for code, field in [(errno.ESRCH, 'registrationAbsent'), (errno.EACCES, 'watchDenied'),
                    (errno.EPERM, 'watchDenied'), (errno.ENOSYS, 'watchUnavailable'),
                    (errno.ENOTSUP, 'watchUnavailable'), (errno.EINVAL, 'observerError'), (0, 'observerError')]:
    for failure in [OSError(code, '/private/secret'), [Event(flags=0x4000, error=code)]]:
        observer, queue, probe, timeline = create([failure])
        rejected = finish(observer)
        assert rejected[field] and not rejected['watchRegistered'] and not rejected['exitEventObserved'] and queue.closed
observer, queue, probe, timeline = create([[], [Event(flags=0x4000, error=errno.ESRCH)]])
observer.observe()
assert finish(observer)['observerError'] and not observer.finish()['registrationAbsent']
for events in [None, [Event(), Event()], [object()], [Event(ident=1)], [Event(ident=True)],
               [Event(filter=1)], [Event(flags=-1)], [Event(flags=1 << 28)], [Event(fflags=0)],
               [Event(fflags=0xC0000000)], [Event(flags=0x4000, error='/private/secret')]]:
    observer, queue, probe, timeline = create([events])
    rejected = finish(observer)
    assert rejected['observerError'] and not rejected['exitEventObserved'] and not rejected['watchRegistered']
for failure in [OSError(errno.EINVAL, '/private/secret'), [Event(ident=1)]]:
    observer, queue, probe, timeline = create([[], failure])
    observer.observe()
    assert finish(observer)['observerError'] and not observer.finish()['exitEventObserved']

probe = Probe([], [])
unavailable = finish(PackageProcessLifetimeObserver(target_pid, SimpleNamespace(), probe))
assert unavailable['watchUnavailable'] and not unavailable['watchRegistered']
for code, field in [(errno.ESRCH, 'pidAbsentObserved'), (errno.EACCES, 'livenessDenied'),
                    (errno.EPERM, 'livenessDenied'), (errno.EINVAL, 'observerError')]:
    observer, queue, probe, timeline = create([[]], [OSError(code, '/private/secret')])
    result = finish(observer)
    assert result[field] and not result['pidPresentAtFinalCheck']
    assert result['finalLivenessChecked'] == (code == errno.ESRCH)
observer, queue, probe, timeline = create([[]])
present = finish(observer)
assert present['watchRegistered'] and present['pidPresentAtFinalCheck'] and not present['exitEventObserved']
assert not present['pidAbsentObserved']
frozen_present = dict(present)
queue.results.append([Event()])
probe.results.append(OSError(errno.ESRCH, '/private/cleanup'))
timeline.append('cleanup')
observer.observe()
assert observer.finish() == frozen_present and timeline[-2:] == ['close', 'cleanup']
present['exitEventObserved'] = True
assert observer.finish() == frozen_present
observer, queue, probe, timeline = create([[]])
ordinary_close = queue.close
def failed_close():
    ordinary_close()
    raise OSError(errno.EIO, '/private/close')
queue.close = failed_close
assert finish(observer)['observerError'] and queue.closed
observer, queue, probe, timeline = create([[]])
backend = Backend(queue)
def unavailable_queue(): raise NotImplementedError('/private/unavailable')
backend.kqueue = unavailable_queue
assert finish(PackageProcessLifetimeObserver(target_pid, backend, probe))['watchUnavailable']
observer, queue, probe, timeline = create([[]])
for _ in range(1000): observer.observe()
bounded = finish(observer)
assert bounded['pollCount'] == 128 and bounded['pollLimitReached']
assert len(queue.calls) == 129 and len(probe.calls) == 128 and queue.closed
for invalid_pid in [0, -1, True, '987654321']:
    probe = Probe([], [])
    invalid = finish(PackageProcessLifetimeObserver(invalid_pid, SimpleNamespace(), probe))
    assert invalid['observerError'] and not probe.calls and not invalid['finalObservationBeforeCleanup']
with open(sys.argv[3], 'w', encoding='utf-8') as stream:
    json.dump(immediate, stream, separators=(',', ':'))
print('Actual process observer fixture cases passed:', observer_cases)
""";

    private static void WindowsLauncherShouldEnforceCompleteSingleAttemptEvidence(string root)
    {
        if (!OperatingSystem.IsWindows()) return;
        Console.WriteLine(nameof(WindowsLauncherShouldEnforceCompleteSingleAttemptEvidence));
        var repository = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!;
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing fixture executable.");
        var launcher = Path.Combine(repository, "scripts", "run-maui-windows-smoke.ps1");
        string[] modes = ["success", "pipe-pressure", "missing", "failure", "nonzero", "timeout",
            "oversized", "bad-marker", "stale-nonce", "changed-file", "existing-output", "multiple-attempts"];
        foreach (var mode in modes)
        {
            Console.WriteLine("Windows launcher case: " + mode);
            var output = Path.Combine(root, "windows-" + mode + ".json");
            if (mode == "existing-output") File.WriteAllText(output, "previous evidence");
            string[] arguments = ["-NoProfile", "-NonInteractive", "-File", launcher,
                "-ExecutablePath", executable, "-ResultPath", output, "-MarkerPrefix", Prefix,
                "-ResultProtocol", "app-file-v1", "-MaximumAttempts", mode == "multiple-attempts" ? "2" : "1",
                "-TimeoutSeconds", "10"];
            var code = RunTool("pwsh", arguments, mode);
            if (mode is "success" or "pipe-pressure")
            {
                Require(code == 0 && File.Exists(output), "Valid synthetic Windows transport failed.");
                using var document = JsonDocument.Parse(File.ReadAllBytes(output));
                CheckFullPayload(document.RootElement, "success");
            }
            else
            {
                Require(code != 0, "Invalid synthetic Windows transport passed.");
                if (mode == "existing-output")
                    Require(File.ReadAllText(output) == "previous evidence", "Existing Windows output was replaced.");
                else Require(!File.Exists(output), "Rejected Windows transport published evidence.");
            }
        }
    }

    private static void MacLauncherShouldRejectInvalidIdentityAndUnsignedPayload(string root)
    {
        if (!OperatingSystem.IsMacOS()) return;
        Console.WriteLine(nameof(MacLauncherShouldRejectInvalidIdentityAndUnsignedPayload));
        var repository = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!;
        var launcherSource = File.ReadAllText(Path.Combine(repository, "scripts", "run-maui-maccatalyst-smoke.sh"));
        var cleanupStart = launcherSource.IndexOf("cleanup() {", StringComparison.Ordinal);
        var cleanupEnd = launcherSource.IndexOf("\n}\ntrap cleanup EXIT", cleanupStart, StringComparison.Ordinal) + 2;
        Require(cleanupStart >= 0 && cleanupEnd > cleanupStart, "Missing actual Mac cleanup function.");
        var cleanup = launcherSource[cleanupStart..cleanupEnd];
        var cleanupHarness = "PACKAGE_MODE=app-file-v1\nAPP_PID=\"$1\"\ncalls=0\n" +
            "kill() { calls=$((calls+1)); return 0; }\n" + cleanup + "\ncleanup\ntest \"$calls\" -eq \"$2\"";
        string[] processIds = ["", "0", "-1", "00123", "12345"];
        foreach (var processId in processIds)
        {
            string[] check = ["-c", cleanupHarness, "fixture", processId, processId == "12345" ? "2" : "0"];
            Require(RunTool("bash", check) == 0, "Mac package cleanup did not restrict the process identity.");
        }
        var app = Path.Combine(root, "Unsigned.app");
        var contents = Path.Combine(app, "Contents");
        var binaries = Path.Combine(contents, "MacOS");
        Directory.CreateDirectory(binaries);
        File.WriteAllText(Path.Combine(contents, "Info.plist"),
            "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>CFBundleIdentifier</key>" +
            "<string>com.neraspreadsheet.transportfixture</string><key>CFBundleExecutable</key><string>Unsigned</string></dict></plist>");
        var executable = Path.Combine(binaries, "Unsigned");
        File.WriteAllBytes(executable, UnsignedFixtureBytes);
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        string[] modes = ["wrong-bundle", "unsigned", "existing-output", "unknown-protocol"];
        foreach (var mode in modes)
        {
            Console.WriteLine("Mac launcher case: " + mode);
            var output = Path.Combine(root, "mac-" + mode + ".json");
            if (mode == "existing-output") File.WriteAllText(output, "previous evidence");
            string[] arguments = [Path.Combine(repository, "scripts", "run-maui-maccatalyst-smoke.sh"), app, output,
                mode == "wrong-bundle" ? "com.neraspreadsheet.different" : "com.neraspreadsheet.transportfixture", Prefix,
                mode == "unknown-protocol" ? "unknown" : "app-file-v1"];
            Require(RunTool("bash", arguments) != 0, "Invalid synthetic Mac transport passed.");
            if (mode == "existing-output")
                Require(File.ReadAllText(output) == "previous evidence", "Existing Mac output was replaced.");
            else Require(!File.Exists(output), "Rejected Mac transport published evidence.");
        }
    }

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
