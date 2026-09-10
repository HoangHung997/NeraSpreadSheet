using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.OpenXml;

if (args.Length != 2 || args[0].Length != 40 || !args[0].All(Uri.IsHexDigit))
    throw new ArgumentException("Pass an immutable source SHA and an output JSON path.");
var source = await CreateSyntheticInputAsync();
var samples = new List<Sample>();
var serializer = new NeraOpenXmlWorkbookSerializer();
for (var iteration = -1; iteration < 3; iteration++)
{
    using var input = new MemoryStream(source, writable: false);
    var allocated = GC.GetTotalAllocatedBytes(precise: true);
    var timer = Stopwatch.StartNew();
    var workbook = await serializer.LoadAsync(input, new OpenXmlImportOptions { PreserveUnknownParts = true });
    var load = timer.Elapsed.TotalMilliseconds;
    if (workbook.Worksheets.Count != 5 || workbook.Worksheets.Sum(sheet => sheet.UsedCellCount) != 100_000)
        throw new InvalidDataException("The benchmark did not load its full sparse dataset.");
    workbook.Worksheets[0].SetValue(new CellAddress(1, 1), 1234.5);
    using var output = new MemoryStream();
    timer.Restart();
    await serializer.SaveAsync(workbook, output, new OpenXmlExportOptions { PreserveUnknownParts = true });
    var save = timer.Elapsed.TotalMilliseconds;
    var bytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
    output.Position = 0;
    using (var document = SpreadsheetDocument.Open(output, false))
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rules = 0;
        foreach (var part in document.WorkbookPart!.WorksheetParts)
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            rules += XDocument.Load(stream).Descendants(ns + "cfRule").Count();
        }
        if (rules != 29) throw new InvalidDataException("Preserved rules were lost during benchmark export.");
    }
    if (iteration >= 0) samples.Add(new Sample(load, save, bytes));
}
var report = new
{
    schema = "nera.xlsx.compatibility.probe.v1", sourceSha = args[0], syntheticFixture = true,
    cells = 100_000, worksheets = 5, conditionalRules = 29, wholeColumnRanges = true,
    inputBytes = source.Length, warmupIterations = 1, measuredIterations = samples.Count,
    runtime = Environment.Version.ToString(), os = Environment.OSVersion.Platform.ToString(),
    sdkAssembly = typeof(NeraOpenXmlWorkbookSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    samples, medianLoadMilliseconds = samples.Select(sample => sample.LoadMilliseconds).Order().ElementAt(1),
    medianSaveMilliseconds = samples.Select(sample => sample.SaveMilliseconds).Order().ElementAt(1),
    medianAllocatedBytes = samples.Select(sample => sample.AllocatedBytes).Order().ElementAt(1),
    measuresScrolling = false, measuresH2Integration = false,
};
var destination = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report));

static async Task<byte[]> CreateSyntheticInputAsync()
{
    var workbook = new Workbook(createDefaultWorksheet: false);
    for (var index = 0; index < 5; index++)
    {
        var sheet = workbook.AddWorksheet("Synthetic " + index.ToString(CultureInfo.InvariantCulture));
        for (var row = 0; row < 20_000; row++) sheet.SetValue(new CellAddress(row, 0), row % 17 + 0.25);
    }
    using var output = new MemoryStream();
    await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, output, new OpenXmlExportOptions());
    output.Position = 0;
    using (var document = SpreadsheetDocument.Open(output, true))
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var styles = document.WorkbookPart!.WorkbookStylesPart!;
        XDocument xml;
        using (var stream = styles.GetStream(FileMode.Open, FileAccess.Read)) xml = XDocument.Load(stream);
        xml.Root!.Elements(ns + "dxfs").Remove();
        var dxfs = new XElement(ns + "dxfs", new XAttribute("count", 1),
            new XElement(ns + "dxf", new XElement(ns + "font", new XElement(ns + "color", new XAttribute("rgb", "FFCC1122")))));
        var tail = xml.Root.Elements().FirstOrDefault(element => element.Name.LocalName is "tableStyles" or "colors" or "extLst");
        if (tail is null) xml.Root.Add(dxfs); else tail.AddBeforeSelf(dxfs);
        using (var stream = styles.GetStream(FileMode.Create, FileAccess.Write)) xml.Save(stream);
        var index = 0;
        foreach (var part in document.WorkbookPart.WorksheetParts)
        {
            using (var stream = part.GetStream(FileMode.Open, FileAccess.Read)) xml = XDocument.Load(stream);
            var count = index++ == 4 ? 5 : 6;
            xml.Root!.Element(ns + "sheetData")!.AddAfterSelf(
                new XElement(ns + "conditionalFormatting", new XAttribute("sqref", "A1:A1048576"),
                    Enumerable.Range(1, count).Select(priority => new XElement(ns + "cfRule", new XAttribute("type", "duplicateValues"),
                        new XAttribute("dxfId", 0), new XAttribute("priority", priority)))));
            using (var stream = part.GetStream(FileMode.Create, FileAccess.Write)) xml.Save(stream);
        }
    }
    return output.ToArray();
}

internal sealed record Sample(double LoadMilliseconds, double SaveMilliseconds, long AllocatedBytes);
