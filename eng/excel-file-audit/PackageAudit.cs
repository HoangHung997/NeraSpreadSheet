using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace NeraSpreadSheet.FileAudit;

internal static class PackageAudit
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly string[] SheetFeatures = ["conditionalFormatting", "sheetViews", "mergeCells", "autoFilter", "dataValidations", "tableParts", "drawing", "extLst", "pageSetup", "definedNames"];
    internal static PackageSnapshot Inspect(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 20_000) throw new InvalidDataException("Audit ZIP exceeds 20,000 entries.");
        var total = zip.Entries.Sum(entry => entry.Length);
        if (total > 512L * 1024 * 1024) throw new InvalidDataException("Audit expanded ZIP exceeds 512 MiB.");
        var partHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var featureHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var richTextRuns = 0;
        foreach (var entry in zip.Entries)
        {
            using var content = entry.Open();
            if (!partHashes.TryAdd(entry.FullName, Convert.ToHexString(SHA256.HashData(content)))) throw new InvalidDataException("ZIP contains duplicate entry names.");
            if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
            using var xmlStream = entry.Open(); var xml = Read(xmlStream);
            var root = xml.Root ?? throw new InvalidDataException("An XML part has no root.");
            richTextRuns += root.Descendants(Main + "r").Count();
            if (root.Name == Main + "worksheet")
                foreach (var name in SheetFeatures)
                    featureHashes.Add(entry.FullName + "#" + name, Digest(new XElement("audit", root.Elements(Main + name).Select(element => new XElement(element)))));
            else if (root.Name == Main + "styleSheet")
            {
                foreach (var name in new[] { "dxfs", "numFmts", "fonts", "fills", "borders", "cellXfs", "extLst" })
                    featureHashes.Add(entry.FullName + "#" + name, Digest(new XElement("audit", root.Elements(Main + name).Select(element => new XElement(element)))));
            }
            else if (root.Name == Main + "workbook")
                featureHashes.Add(entry.FullName + "#definedNames", Digest(new XElement("audit", root.Elements(Main + "definedNames").Select(element => new XElement(element)))));
        }
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException("Office package has no workbook part.");
        var table = workbook.Workbook.GetFirstChild<S.Sheets>() ?? throw new InvalidDataException("Workbook has no sheets collection.");
        var sheets = new List<SheetInventory>();
        foreach (var element in table.Elements<S.Sheet>())
        {
            var id = element.Id?.Value ?? throw new InvalidDataException("Sheet has no relationship id.");
            var part = workbook.GetPartById(id);
            if (part is not WorksheetPart worksheetPart) continue;
            using var data = part.GetStream(FileMode.Open, FileAccess.Read); var xml = Read(data); var root = xml.Root!;
            var rules = root.Elements(Main + "conditionalFormatting").SelectMany(container => container.Elements(Main + "cfRule").Select(rule =>
                new RuleInventory((string?)container.Attribute("sqref"), (string?)rule.Attribute("type"),
                    (string?)rule.Attribute("priority"), (string?)rule.Attribute("dxfId"), (string?)rule.Attribute("stopIfTrue")))).ToArray();
            var cells = root.Element(Main + "sheetData")?.Descendants(Main + "c").ToArray() ?? [];
            sheets.Add(new SheetInventory(element.Name?.Value ?? "", part.Uri.ToString(),
                cells.Length, cells.Count(cell => cell.Element(Main + "f") is not null),
                cells.Count(cell => (string?)cell.Attribute("t") == "e"), worksheetPart.TableDefinitionParts.Count(), rules));
        }
        var dxfs = workbook.WorkbookStylesPart?.Stylesheet?.DifferentialFormats?.ChildElements.Count ?? 0;
        return new PackageSnapshot(zip.Entries.Count, total, sheets, dxfs, richTextRuns,
            zip.Entries.Count(entry => entry.FullName.StartsWith("xl/media/", StringComparison.Ordinal)),
            zip.Entries.Count(entry => entry.FullName.StartsWith("xl/charts/", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)),
            zip.Entries.Count(entry => entry.FullName.StartsWith("xl/pivot", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)),
            partHashes, featureHashes);
    }
    internal static object Compare(PackageSnapshot? before, PackageSnapshot after)
    {
        if (before is null) throw new InvalidOperationException("No original package inventory is available.");
        var lost = before.PartSha256.Keys.Except(after.PartSha256.Keys, StringComparer.Ordinal).ToArray();
        var changed = before.PartSha256.Keys.Intersect(after.PartSha256.Keys, StringComparer.Ordinal).Where(key => before.PartSha256[key] != after.PartSha256[key]).ToArray();
        var changedFeatures = before.FeatureSha256.Keys.Where(key => !after.FeatureSha256.TryGetValue(key, out var digest) || before.FeatureSha256[key] != digest).ToArray();
        return new
        {
            rulesBefore = before.Sheets.Sum(sheet => sheet.Rules.Count), rulesAfter = after.Sheets.Sum(sheet => sheet.Rules.Count),
            dxfsBefore = before.DifferentialStyles, dxfsAfter = after.DifferentialStyles,
            richTextRunsBefore = before.RichTextRuns, richTextRunsAfter = after.RichTextRuns,
            lostParts = lost, byteChangedParts = changed, changedXmlFeatureSnapshots = changedFeatures,
            conditionalAndDxfXmlUnchanged = !changedFeatures.Any(key => key.EndsWith("#conditionalFormatting", StringComparison.Ordinal) || key.EndsWith("#dxfs", StringComparison.Ordinal)),
            interpretation = "A changed XML hash requires semantic review; preserved XML does not prove valid reference rebasing after structural edits.",
        };
    }
    internal static async Task<byte[]> CreateSyntheticAsync()
    {
        var workbook = new Workbook(createDefaultWorksheet: false);
        for (var index = 0; index < 6; index++)
        {
            var sheet = workbook.AddWorksheet("Synthetic " + (index + 1).ToString(CultureInfo.InvariantCulture));
            sheet.SetValue(default, 7d); sheet.SetValue(new CellAddress(1, 0), 7d); sheet.SetFormula(new CellAddress(0, 1), "=SUM(A1:A2)");
        }
        new SpreadsheetSession(workbook).Recalculate();
        using var output = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, output, new OpenXmlExportOptions());
        output.Position = 0;
        using (var document = SpreadsheetDocument.Open(output, true))
        {
            var styles = document.WorkbookPart!.WorkbookStylesPart!;
            var xml = ReadPart(styles); var root = xml.Root!; root.Elements(Main + "dxfs").Remove();
            var dxfs = new XElement(Main + "dxfs", new XAttribute("count", 1),
                new XElement(Main + "dxf", new XElement(Main + "font", new XElement(Main + "color", new XAttribute("rgb", "FFCC1122")))));
            var next = root.Elements().FirstOrDefault(element => element.Name.LocalName is "tableStyles" or "colors" or "extLst");
            if (next is null) root.Add(dxfs); else next.AddBeforeSelf(dxfs); WritePart(styles, xml);
            var sheetElements = document.WorkbookPart.Workbook.GetFirstChild<S.Sheets>()!.Elements<S.Sheet>().ToArray();
            for (var index = 1; index < 6; index++)
            {
                var part = document.WorkbookPart.GetPartById(sheetElements[index].Id!.Value!); var sheetXml = ReadPart(part);
                for (var rule = 1; rule <= (index == 5 ? 5 : 6); rule++)
                    sheetXml.Root!.Add(new XElement(Main + "conditionalFormatting", new XAttribute("sqref", "A1:A1048576 C2:C17"),
                        new XElement(Main + "cfRule", new XAttribute("type", "duplicateValues"), new XAttribute("priority", rule), new XAttribute("dxfId", 0))));
                WritePart(part, sheetXml);
            }
        }
        return output.ToArray();
    }
    private static XDocument ReadPart(OpenXmlPart part) { using var stream = part.GetStream(FileMode.Open, FileAccess.Read); return Read(stream); }
    private static XDocument Read(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256L * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static void WritePart(OpenXmlPart part, XDocument xml)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }); xml.Save(writer);
    }
    private static string Digest(XElement xml) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting))));
}
internal sealed record RuleInventory(string? Sqref, string? Type, string? Priority, string? DxfId, string? StopIfTrue);
internal sealed record SheetInventory(string Name, string PartUri, int XmlCells, int FormulaCells, int CachedErrors, int Tables, IReadOnlyList<RuleInventory> Rules);
internal sealed record PackageSnapshot(int Parts, long ExpandedBytes, IReadOnlyList<SheetInventory> Sheets, int DifferentialStyles, int RichTextRuns,
    int MediaParts, int ChartParts, int PivotParts, Dictionary<string, string> PartSha256, Dictionary<string, string> FeatureSha256);
