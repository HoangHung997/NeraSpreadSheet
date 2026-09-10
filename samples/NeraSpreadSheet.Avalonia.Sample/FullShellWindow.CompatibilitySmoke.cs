using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using global::Avalonia.Controls.ApplicationLifetimes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly XNamespace CompatibilityXml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Exercises the actual shell import/export path with synthetic input only.</summary>
    internal async void StartCompatibilitySmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var checks = new List<string>();
        var captures = new List<RibbonCapture>();
        void Check(string id, bool condition)
        {
            if (!condition || checks.Contains(id)) throw new InvalidOperationException("XLSX compatibility smoke failed: " + id);
            checks.Add(id);
        }
        try
        {
            var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
            Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
            await SettleRibbonAsync();
            Check("native-start", IsVisible && _split.ActiveSpreadsheet.RenderedFrameCount > 0);
            using var source = await CreateCompatibilityFixtureAsync();
            var original = source.ToArray();
            var formatting = ReadCompatibilityFormatting(source);
            source.Position = 0;
            var strictFailed = false;
            try { await _serializer.LoadSessionAsync(source, new OpenXmlImportOptions()); }
            catch (InvalidDataException exception)
            {
                strictFailed = OpenXmlImportDiagnostics.TryGetFailure(exception, out var error) &&
                    error.Kind == OpenXmlImportProblemKind.UnsupportedFeature;
            }
            Check("strict-classifies-unsupported", strictFailed);
            Check("strict-source-unchanged", source.ToArray().AsSpan().SequenceEqual(original));
            source.Position = 0;
            var before = Session;
            await RunIo(() => OpenCompatibleStreamAsync(source, "Synthetic-inner-borders.dlda"));
            await SettleRibbonAsync();
            Check("replaced-sample-only-after-load", !ReferenceEquals(Session, before));
            Check("source-name-visible", Title?.Contains("Synthetic-inner-borders.dlda", StringComparison.Ordinal) == true);
            Check("loaded-sheet-name", Session.ActiveWorksheet.Name == "Compatibility fixture");
            Check("loaded-numeric-type", Session.ActiveWorksheet.GetCell(new CellAddress(2, 0)).Value.Kind == CellValueKind.Number);
            Check("loaded-numeric-value", Equals(Session.ActiveWorksheet.GetValue(new CellAddress(2, 0)), 1234.5));
            Check("loaded-formula", Session.ActiveWorksheet.GetFormula(new CellAddress(2, 1)) == "=A3*2");
            Check("loaded-cached-value", Equals(Session.ActiveWorksheet.GetValue(new CellAddress(2, 1)), 2469d));
            var report = OpenXmlImportDiagnostics.Get(Session.Workbook);
            Check("opaque-report", report.RequiresMetadataPreservation);
            Check("vertical-warning", report.Warnings.Any(warning => warning.Feature == "dxf/border/vertical"));
            Check("horizontal-warning", report.Warnings.Any(warning => warning.Feature == "dxf/border/horizontal"));
            Check("rule-location-warning", report.Warnings.Any(warning => warning.WorksheetName == "Compatibility fixture" && warning.Reference == "A3:A1048576"));
            Check("known-rule-retained-in-model", Session.ActiveWorksheet.ConditionalFormattingRuleCount == 1);
            Check("persistent-warning-visible", _compatibilityNotice.IsVisible && _compatibilityNotice.Bounds.Height > 0);
            Check("notice-does-not-claim-fidelity", _compatibilityText.Text?.Contains("chưa hiển thị đầy đủ", StringComparison.Ordinal) == true);
            Check("native-data-frame", _split.ActiveSpreadsheet.RenderedFrameCount > 0);
            Check("import-source-unchanged", source.ToArray().AsSpan().SequenceEqual(original));
            Check("metadata-command-disabled", _registry.TryResolve("Structure.Row.Insert", out _, out var structural) && structural is not null && !structural.CanExecute(default));
            var sheetVersion = Session.ActiveWorksheet.Version;
            Check("disabled-command-not-executed", !await _ribbon.ActivateCommandAsync("Structure.Row.Insert"));
            Check("disabled-command-no-mutation", Session.ActiveWorksheet.Version == sheetVersion);
            var directory = Path.Combine(Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia", "compatibility");
            Directory.CreateDirectory(directory);
            CaptureRibbonScene(this, "compatibility-open", 1, directory, captures);

            Session.SetValue(new CellAddress(2, 0), 2468.75);
            Session.Selection.SetActiveCell(new CellAddress(2, 0));
            Session.Styles.SetNumberFormat("#,##0.000");
            Check("normal-cell-edit", Equals(Session.ActiveWorksheet.GetValue(new CellAddress(2, 0)), 2468.75));
            Check("normal-format-edit", Session.Styles.ActiveCellStyle.NumberFormat.FormatCode == "#,##0.000");
            using var saved = new MemoryStream();
            await SaveCompatibleStreamAsync(Session, saved);
            Check("saved-protected-formatting", XNode.DeepEquals(formatting, ReadCompatibilityFormatting(saved)));
            saved.Position = 0;
            var reloaded = await _serializer.LoadSessionAsync(saved, OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility));
            Check("reloaded-numeric", Equals(reloaded.ActiveWorksheet.GetValue(new CellAddress(2, 0)), 2468.75));
            Check("reloaded-formula", reloaded.ActiveWorksheet.GetFormula(new CellAddress(2, 1)) == "=A3*2");
            Check("reloaded-format", reloaded.ActiveWorksheet.GetEffectiveStyle(new CellAddress(2, 0), reloaded.Workbook.Styles).NumberFormat.FormatCode == "#,##0.000");
            using var twice = new MemoryStream();
            await SaveCompatibleStreamAsync(reloaded, twice);
            Check("repeated-save-preservation", XNode.DeepEquals(formatting, ReadCompatibilityFormatting(twice)));
            Check("original-still-unchanged", source.ToArray().AsSpan().SequenceEqual(original));
            await SettleRibbonAsync();
            CaptureRibbonScene(this, "compatibility-edited", 1, directory, captures);

            using var broken = new MemoryStream(); broken.Write(original); broken.Position = 0;
            using (var doc = SpreadsheetDocument.Open(broken, true))
            {
                var part = doc.WorkbookPart!.WorkbookStylesPart!; var xml = ReadCompatibilityXml(part);
                xml.Descendants(CompatibilityXml + "vertical").Single().SetAttributeValue("style", "invalid-line-style");
                WriteCompatibilityXml(part, xml);
            }
            broken.Position = 0; var displayed = Session; var rejected = false;
            try { await RunIo(() => OpenCompatibleStreamAsync(broken, "Synthetic-corrupt.xlsx")); }
            catch (InvalidDataException) { rejected = true; }
            await SettleRibbonAsync();
            Check("corrupt-not-swallowed", rejected);
            Check("failed-load-keeps-session", ReferenceEquals(Session, displayed));
            Check("failed-load-keeps-cell", Equals(Session.ActiveWorksheet.GetValue(new CellAddress(2, 0)), 2468.75));
            Check("failed-load-identifies-displayed-file", _compatibilityText.Text?.Contains("Vẫn đang hiển thị: Synthetic-inner-borders.dlda", StringComparison.Ordinal) == true);
            Check("input-reenabled", _split.IsEnabled && _formula.IsEnabled && !_busy);
            CaptureRibbonScene(this, "compatibility-rejected", 1, directory, captures);
            Check("captures-complete", captures.Count == 3);
            var evidence = new
            {
                schema = "nera.xlsx.compatibility.native.v1", sha, nativeWindow = true, syntheticFixture = true,
                realWorkbookTested = false, excelOracleTested = false, innerBordersRendered = false,
                assertions = checks.Count, checks, captures,
                sourceBytes = original.Length,
                sourceSha256 = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(),
            };
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(evidence, RibbonVisualJson));
            Console.WriteLine("NERA_AVALONIA_COMPATIBILITY_SUCCESS " + JsonSerializer.Serialize(new
            {
                sha, nativeWindow = true, syntheticFixture = true, assertions = checks.Count, captures = captures.Count,
            }));
            lifetime.Shutdown(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("NERA_AVALONIA_COMPATIBILITY_FAILURE " + exception);
            lifetime.Shutdown(1);
        }
    }

    private static async Task<MemoryStream> CreateCompatibilityFixtureAsync()
    {
        var workbook = new Workbook(); var sheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(sheet, "Compatibility fixture");
        sheet.Dimensions.SetColumnWidth(0, 220); sheet.Dimensions.SetColumnWidth(1, 150); sheet.Dimensions.SetColumnWidth(2, 340);
        sheet.SetValue(default, "XLSX — dữ liệu tổng hợp kiểm thử");
        sheet.SetValue(new CellAddress(1, 0), "Giá trị số"); sheet.SetValue(new CellAddress(1, 1), "Công thức");
        sheet.SetValue(new CellAddress(1, 2), "Viền trong: bảo toàn, chưa vẽ đầy đủ");
        sheet.SetValue(new CellAddress(2, 0), 1234.5); sheet.SetFormula(new CellAddress(2, 1), "=A3*2");
        sheet.SetValue(new CellAddress(3, 0), 1234.5); sheet.SetValue(new CellAddress(4, 0), 42);
        new SpreadsheetSession(workbook).Recalculate();
        var bytes = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, bytes, new OpenXmlExportOptions());
        bytes.Position = 0;
        using (var doc = SpreadsheetDocument.Open(bytes, true))
        {
            var part = doc.WorkbookPart!.WorkbookStylesPart!; var styles = ReadCompatibilityXml(part);
            var dxfs = new XElement(CompatibilityXml + "dxfs", new XAttribute("count", 3),
                Inner("vertical"), Inner("horizontal"),
                new XElement(CompatibilityXml + "dxf", new XElement(CompatibilityXml + "font", new XElement(CompatibilityXml + "color", new XAttribute("rgb", "FF156082")))));
            styles.Root!.Elements(CompatibilityXml + "dxfs").Remove();
            var tail = styles.Root.Elements().FirstOrDefault(element => element.Name.LocalName is "tableStyles" or "colors" or "extLst");
            if (tail is null) styles.Root.Add(dxfs); else tail.AddBeforeSelf(dxfs);
            WriteCompatibilityXml(part, styles);
            var worksheet = doc.WorkbookPart.WorksheetParts.Single(); var xml = ReadCompatibilityXml(worksheet);
            var first = Rule("A3:A5", "expression", 0, 1); first.Element(CompatibilityXml + "cfRule")!.Add(new XElement(CompatibilityXml + "formula", "A3>0"));
            var second = Rule("A3:A1048576", "duplicateValues", 1, 2);
            var third = Rule("B3:B5", "cellIs", 2, 3); third.Element(CompatibilityXml + "cfRule")!.Add(new XAttribute("operator", "greaterThan"), new XElement(CompatibilityXml + "formula", 0));
            // A synthetic worksheet without filters/validation: insert CF after sheetData,
            // before any existing page/view-extension tail rather than appending blindly.
            xml.Root!.Element(CompatibilityXml + "sheetData")!.AddAfterSelf(first, second, third);
            WriteCompatibilityXml(worksheet, xml);
        }
        bytes.Position = 0; return bytes;

        static XElement Inner(string side) => new(CompatibilityXml + "dxf", new XElement(CompatibilityXml + "border",
            new XElement(CompatibilityXml + side, new XAttribute("style", "thin"), new XElement(CompatibilityXml + "color", new XAttribute("rgb", "FF217346")))));
        static XElement Rule(string reference, string type, int dxf, int priority) => new(CompatibilityXml + "conditionalFormatting", new XAttribute("sqref", reference),
            new XElement(CompatibilityXml + "cfRule", new XAttribute("type", type), new XAttribute("dxfId", dxf), new XAttribute("priority", priority)));
    }

    private static XElement ReadCompatibilityFormatting(Stream stream)
    {
        stream.Position = 0;
        using var doc = SpreadsheetDocument.Open(stream, false);
        return new XElement("formatting", new XElement(ReadCompatibilityXml(doc.WorkbookPart!.WorkbookStylesPart!).Root!.Element(CompatibilityXml + "dxfs")!),
            doc.WorkbookPart.WorksheetParts.Select(part => new XElement("sheet", ReadCompatibilityXml(part).Root!
                .Elements(CompatibilityXml + "conditionalFormatting").Select(element => new XElement(element)))));
    }
    private static XDocument ReadCompatibilityXml(OpenXmlPart part) { using var stream = part.GetStream(FileMode.Open, FileAccess.Read); return XDocument.Load(stream); }
    private static void WriteCompatibilityXml(OpenXmlPart part, XDocument xml) { using var stream = part.GetStream(FileMode.Create, FileAccess.Write); xml.Save(stream); }
}
