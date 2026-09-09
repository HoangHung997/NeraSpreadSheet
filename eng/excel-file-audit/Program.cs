using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.FileAudit;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly NeraOpenXmlWorkbookSerializer Serializer = new();
    private static readonly NeraOpenXmlSpreadsheetSessionSerializer SessionSerializer = new();
    private static readonly OpenXmlImportOptions Compatibility = new() { PreserveUnknownParts = true };
    private static readonly OpenXmlExportOptions Preserve = new() { PreserveUnknownParts = true };
    private static readonly byte[] Sentinel = Encoding.UTF8.GetBytes("NERA-AUDIT-DESTINATION-SENTINEL");
    private static readonly string[] StructuralScenarios = ["insert-row", "delete-row", "insert-column", "delete-column", "rename-sheet"];
    private static readonly string[] Notes =
    [
        "This is an observation harness, not a compatibility certificate or an Excel oracle.",
        "Synthetic self-test results never stand in for the user's uploaded workbook.",
        "Cached formula values can be stale. Differences after recalculation require investigation, not an automatic verdict that Excel or Nera is wrong.",
        "Shared DisplayList composition is not a native WPF/WinForms/MAUI/Avalonia, GPU, IME or visual-equivalence test.",
        "Only visible/overscan viewports are composed. Whole-column conditional ranges are never expanded by the harness.",
        "All edit/structural probes load independent in-memory copies. The input path is never opened for writing.",
        "XML equality and retained parts do not prove opaque references remain semantically correct after structural edits.",
        "No public SDK source, H2, demo, shared status or another lane is modified by this audit.",
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: FileAudit <input-file|--self-test> <output-directory>"); return 2; }
        Directory.CreateDirectory(args[1]);
        var synthetic = args[0] == "--self-test";
        var report = new AuditReport(Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "LOCAL-UNVERIFIED", synthetic, Notes);
        byte[] input;
        if (synthetic) input = await PackageAudit.CreateSyntheticAsync();
        else if (!File.Exists(args[0]))
        {
            report.State = "BLOCKED_INPUT_MISSING";
            report.Results.Add(new Probe("input", "BLOCKED", 0, null, "FileNotFound", "Original workbook bytes have not been uploaded to this branch. No real-file tests ran."));
            await WriteReport(args[1], report); return 2;
        }
        else
        {
            if (new FileInfo(args[0]).Length > 64L * 1024 * 1024) throw new InvalidDataException("Audit input exceeds its 64 MiB limit.");
            input = await File.ReadAllBytesAsync(args[0]);
        }
        report.InputBytes = input.Length;
        report.InputSha256 = Hash(input);
        PackageSnapshot? raw = null;
        await Observe("raw-package-inventory", async () => { raw = PackageAudit.Inspect(input); await Task.CompletedTask; return raw; });
        foreach (var preserve in new[] { false, true })
        {
            var mode = preserve ? "compatibility" : "strict";
            await Observe("workbook-import-" + mode, async () =>
            {
                using var stream = new MemoryStream(input, writable: false);
                var workbook = await Serializer.LoadAsync(stream, new OpenXmlImportOptions { PreserveUnknownParts = preserve });
                return Summarize(workbook);
            });
            await Observe("session-import-" + mode, async () =>
            {
                using var stream = new MemoryStream(input, writable: false);
                var session = await SessionSerializer.LoadSessionAsync(stream, new OpenXmlImportOptions { PreserveUnknownParts = preserve });
                return Summarize(session.Workbook);
            });
        }
        await Observe("extension-independent-stream", async () =>
        {
            var first = Path.Combine(args[1], "extension-probe.dlda");
            var second = Path.Combine(args[1], "extension-probe.xlsx");
            await File.WriteAllBytesAsync(first, input); await File.WriteAllBytesAsync(second, input);
            try
            {
                await using var a = File.OpenRead(first); await using var b = File.OpenRead(second);
                var wa = await Serializer.LoadAsync(a, Compatibility); var wb = await Serializer.LoadAsync(b, Compatibility);
                return new { identicalInputBytes = Hash(await File.ReadAllBytesAsync(first)) == Hash(await File.ReadAllBytesAsync(second)), identicalImportedModel = Fingerprint(wa) == Fingerprint(wb) };
            }
            finally { File.Delete(first); File.Delete(second); }
        });
        await Observe("workbook-no-edit-three-save-cycles", async () =>
        {
            var workbook = await LoadWorkbook(input);
            var before = Fingerprint(workbook);
            var comparisons = new List<object>();
            for (var index = 0; index < 3; index++)
            {
                using var output = new MemoryStream(); await Serializer.SaveAsync(workbook, output, Preserve);
                var bytes = output.ToArray(); var reloaded = await LoadWorkbook(bytes);
                comparisons.Add(new { cycle = index + 1, modelUnchanged = before == Fingerprint(reloaded), package = PackageAudit.Compare(raw, PackageAudit.Inspect(bytes)) });
                if (index == 1) workbook = reloaded;
            }
            return comparisons;
        });
        foreach (var edit in new[] { "value", "formula", "base-style" })
            await Observe("edit-save-reload-" + edit, async () =>
            {
                var workbook = await LoadWorkbook(input); var sheet = workbook.Worksheets[0];
                var address = PickCell(sheet); var original = sheet.GetCell(address);
                if (edit == "value") sheet.SetValue(address, "NERA_AUDIT_EDIT");
                else if (edit == "formula") sheet.SetFormula(address, "=1+2");
                else
                {
                    var style = sheet.GetEffectiveStyle(address, workbook.Styles);
                    sheet.SetStyle(address, workbook.Styles.Intern(style with { Font = style.Font with { Weight = style.Font.Weight == 700 ? 400 : 700 } }));
                }
                var expected = Fingerprint(workbook);
                using var output = new MemoryStream(); await Serializer.SaveAsync(workbook, output, Preserve);
                var bytes = output.ToArray(); var loaded = await LoadWorkbook(bytes);
                return new { cell = address.ToString(), replacedKind = original.Value.Kind.ToString(), modelRoundTripEqual = expected == Fingerprint(loaded), package = PackageAudit.Compare(raw, PackageAudit.Inspect(bytes)) };
            });
        await Observe("session-save-and-reload", async () =>
        {
            var session = await LoadSession(input); var before = Fingerprint(session.Workbook);
            using var output = new MemoryStream(); await SessionSerializer.SaveSessionAsync(session, output, Preserve);
            var bytes = output.ToArray(); var reload = await LoadSession(bytes);
            return new { modelRoundTripEqual = before == Fingerprint(reload.Workbook), package = PackageAudit.Compare(raw, PackageAudit.Inspect(bytes)) };
        });
        await Observe("shared-viewport-every-sheet", async () =>
        {
            var session = await LoadSession(input); var viewport = new SpreadsheetViewportEngine(session);
            var frames = new List<object>(); var before = Fingerprint(session.Workbook);
            foreach (var sheet in session.Workbook.Worksheets)
            {
                session.ActivateWorksheet(sheet);
                for (var index = 0; index < 3; index++)
                {
                    var clock = Stopwatch.StartNew();
                    var frame = viewport.Compose(index * 13.25, index * 41.75, 960, 540, 64);
                    frames.Add(new { sheet = sheet.Name, frame = index, composeMs = clock.Elapsed.TotalMilliseconds, counts = CountCommands(frame.DisplayList, 0) });
                }
            }
            return new { workbookUnchanged = before == Fingerprint(session.Workbook), frames, nativeBackendTested = false };
        });
        await Observe("recalculate-cache-comparison", async () =>
        {
            var session = await LoadSession(input); var before = FormulaValues(session.Workbook);
            var watch = Stopwatch.StartNew(); session.Recalculate(); var elapsed = watch.Elapsed.TotalMilliseconds;
            var after = FormulaValues(session.Workbook);
            var differences = before.Keys.Union(after.Keys).Where(key => !before.TryGetValue(key, out var x) || !after.TryGetValue(key, out var y) || x != y).ToArray();
            return new { elapsedMs = elapsed, formulasBefore = before.Count, formulasAfter = after.Count,
                changedCachedValues = differences.Length, changedLocationsFirst100 = differences.Take(100).ToArray(),
                cachedErrorsBefore = before.Count(pair => pair.Value.Kind == CellValueKind.Error), errorsAfter = after.Count(pair => pair.Value.Kind == CellValueKind.Error), excelOracle = false };
        });
        await Observe("incremental-edit-five-samples", async () =>
        {
            var session = await LoadSession(input); var sheet = session.ActiveWorksheet; var address = PickCell(sheet);
            var samples = new List<double>();
            for (var index = 0; index < 5; index++)
            {
                var timer = Stopwatch.StartNew(); session.SetValue(address, 701 + index); samples.Add(timer.Elapsed.TotalMilliseconds);
            }
            return new { cell = address.ToString(), milliseconds = samples, medianMs = samples.Order().ElementAt(2), includesSdkDependencyRecalculation = true, includesH2OrNativeUi = false };
        });
        foreach (var scenario in StructuralScenarios)
            await Observe("structural-" + scenario, async () =>
            {
                var session = await LoadSession(input); var workbook = session.Workbook;
                var before = Fingerprint(workbook); var accepted = false; string? operationError = null;
                try
                {
                    switch (scenario)
                    {
                        case "insert-row": session.Structure.InsertRows(1); break;
                        case "delete-row": session.Structure.DeleteRows(1); break;
                        case "insert-column": session.Structure.InsertColumns(1); break;
                        case "delete-column": session.Structure.DeleteColumns(1); break;
                        default: workbook.RenameWorksheet(workbook.Worksheets[0], "NERA_AUDIT_RENAMED"); break;
                    }
                    accepted = true;
                }
                catch (Exception exception) { operationError = exception.GetType().Name + ": " + exception.Message; }
                var unchangedAfterOperation = before == Fingerprint(workbook);
                using var output = new MemoryStream(); output.Write(Sentinel); output.Position = 0;
                var saved = false; object? comparison = null; string? saveError = null;
                try
                {
                    await Serializer.SaveAsync(workbook, output, Preserve); saved = true;
                    comparison = PackageAudit.Compare(raw, PackageAudit.Inspect(output.ToArray()));
                }
                catch (Exception exception) { saveError = exception.GetType().Name + ": " + exception.Message; }
                bool? undoRestoredModel = null;
                if (accepted && scenario != "rename-sheet") { session.Undo(); undoRestoredModel = before == Fingerprint(workbook); }
                return new { accepted, operationError, unchangedAfterOperation, saved, saveError,
                    destinationUnchangedOnRejection = !saved && output.ToArray().AsSpan().SequenceEqual(Sentinel),
                    undoRestoredModel, comparison,
                    opaqueSemantics = accepted && saved ? "NOT_PROVEN: unchanged opaque sqref/formulas after a structural operation may be stale." : "Review atomic refusal and destination integrity separately." };
            });
        var originalIntact = synthetic || report.InputSha256 == Hash(await File.ReadAllBytesAsync(args[0]));
        report.State = originalIntact ? "AUDIT_COMPLETED_NOT_EXCEL_CERTIFIED" : "INPUT_INTEGRITY_FAILURE";
        report.Results.Add(new Probe("input-unchanged", originalIntact ? "EXECUTED" : "FAILED", 0, new { originalIntact }, null, null));
        await WriteReport(args[1], report);
        if (!originalIntact) return 1;
        if (synthetic)
        {
            var inventoryValid = raw?.Sheets.Count == 6 && raw.Sheets.Sum(sheet => sheet.Rules.Count) == 29;
            var strictFailure = report.Results.Single(result => result.Id == "workbook-import-strict").Status == "FAILED";
            var compatibilitySuccess = report.Results.Single(result => result.Id == "workbook-import-compatibility").Status == "EXECUTED";
            if (!inventoryValid || !strictFailure || !compatibilitySuccess) return 1;
            Console.WriteLine("NERA_AUDIT_HARNESS_SELFTEST_SUCCESS synthetic=true realWorkbookTested=false"); return 0;
        }
        Console.WriteLine("NERA_FILE_AUDIT_COMPLETED " + report.InputSha256);
        return report.Results.Any(result => result.Status == "FAILED") ? 3 : 0;

        async Task Observe(string id, Func<Task<object?>> action)
        {
            var clock = Stopwatch.StartNew();
            try { report.Results.Add(new Probe(id, "EXECUTED", clock.Elapsed.TotalMilliseconds, await action(), null, null)); }
            catch (Exception exception)
            {
                report.Results.Add(new Probe(id, "FAILED", clock.Elapsed.TotalMilliseconds, null, exception.GetType().FullName, exception.Message));
            }
            // Flush after each probe so later process timeouts do not erase earlier observations.
            await WriteReport(args[1], report);
        }
    }

    private static async Task<Workbook> LoadWorkbook(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false); return await Serializer.LoadAsync(stream, Compatibility);
    }
    private static async Task<SpreadsheetSession> LoadSession(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false); return await SessionSerializer.LoadSessionAsync(stream, Compatibility);
    }
    private static CellAddress PickCell(Worksheet sheet) => sheet.EnumerateUsedCells()
        .Where(pair => pair.Value.Formula is null && !sheet.MergedCells.TryGetContaining(pair.Key, out _))
        .Select(static pair => pair.Key).FirstOrDefault();
    private static object Summarize(Workbook workbook) => new
    {
        sheets = workbook.Worksheets.Select(sheet => new { sheet.Name, sheet.UsedCellCount, sheet.ConditionalFormattingRuleCount,
            formulas = sheet.EnumerateUsedCells().Count(pair => pair.Value.Formula is not null),
            cachedErrors = sheet.EnumerateUsedCells().Count(pair => pair.Value.Value.Kind == CellValueKind.Error),
            mergedRanges = sheet.MergedCells.Ranges.Count, tableCount = sheet.TableCount }).ToArray(),
        dateSystem = workbook.DateSystem.ToString(), fingerprint = Fingerprint(workbook),
        modeledConditionalRuleKinds = Enum.GetNames<ConditionalFormattingRuleType>(),
    };
    private static string Fingerprint(Workbook workbook)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var sheet in workbook.Worksheets)
        {
            Append(sheet.Name);
            foreach (var pair in sheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex))
            {
                Append(pair.Key.ToString()); Append(pair.Value.Value.Kind.ToString()); Append(pair.Value.Value.ToString());
                Append(pair.Value.Formula ?? string.Empty); Append(JsonSerializer.Serialize(sheet.GetEffectiveStyle(pair.Key, workbook.Styles)));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
        void Append(string text) { hash.AppendData(Encoding.UTF8.GetBytes(text)); hash.AppendData(new byte[] { 0 }); }
    }
    private static Dictionary<string, CellValue> FormulaValues(Workbook workbook) => workbook.Worksheets
        .SelectMany(sheet => sheet.EnumerateUsedCells().Where(pair => pair.Value.Formula is not null)
            .Select(pair => new KeyValuePair<string, CellValue>(sheet.Name + "!" + pair.Key, pair.Value.Value)))
        .ToDictionary();
    private static object CountCommands(DisplayList list, int depth)
    {
        if (depth > 64) throw new InvalidDataException("Nested display list exceeds audit depth limit.");
        var nested = list.Commands.OfType<DrawDisplayListCommand>().Select(command => CountCommands(command.DisplayList, depth + 1)).ToArray();
        return new { direct = list.Commands.Count, text = list.Commands.OfType<DrawTextCommand>().Count(), nested };
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static async Task WriteReport(string directory, AuditReport report)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(report, Json));
        var builder = new StringBuilder("# Báo cáo audit khả năng đọc/lưu XLSX của NeraSpreadSheet\n\n");
        builder.AppendLine($"- HEAD kiểm thử: `{report.SourceSha}`\n- Loại dữ liệu: **{(report.Synthetic ? "FIXTURE TỔNG HỢP — KHÔNG PHẢI FILE NGƯỜI DÙNG" : "FILE ĐẦU VÀO") }**\n- Trạng thái: **{report.State}**\n- SHA256 đầu vào: `{report.InputSha256 ?? "CHƯA ĐỌC ĐƯỢC BYTE"}`\n- Dung lượng: {report.InputBytes} byte\n");
        builder.AppendLine("`EXECUTED` chỉ có nghĩa thao tác chạy xong, không tự chứng minh Excel tương thích. Đọc các giá trị false, khác biệt XML và giới hạn trong report.json.\n");
        builder.AppendLine("| Probe | Kết quả | Thời gian ms |\n|---|---|---:|");
        foreach (var probe in report.Results) builder.AppendLine(CultureInfo.InvariantCulture, $"| {probe.Id} | {probe.Status} | {probe.ElapsedMs:F3} |");
        builder.AppendLine("\n## Chi tiết\n\n```json"); builder.AppendLine(JsonSerializer.Serialize(report, Json)); builder.AppendLine("```\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "REPORT.md"), builder.ToString());
    }
    private sealed record Probe(string Id, string Status, double ElapsedMs, object? Detail, string? ErrorType, string? Error);
    private sealed class AuditReport(string sourceSha, bool synthetic, IReadOnlyList<string> limitations)
    {
        public string SourceSha { get; } = sourceSha;
        public bool Synthetic { get; } = synthetic;
        public string State { get; set; } = "RUNNING";
        public string? InputSha256 { get; set; }
        public long InputBytes { get; set; }
        public IReadOnlyList<string> Limitations { get; } = limitations;
        public List<Probe> Results { get; } = [];
    }
}
