using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class Table007LibreOfficeCorpusTests
{
    private static readonly string CorpusPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TableNative");

    [TestMethod]
    public async Task CalcProducerShouldMatchVersionHashesPrivacyAndUnmodifiedNativePayloads()
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(CorpusPath, "libreoffice-provenance.json")));
        var root = manifest.RootElement;
        StringAssert.Contains(root.GetProperty("version").GetString()!, "LibreOffice 24.2.7.2");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, "libreoffice-table.xlsx"));
        Assert.AreEqual(root.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var property in root.GetProperty("unchangedPayloadSha256").EnumerateObject())
        {
            using var part = zip.GetEntry(property.Name)!.Open();
            Assert.AreEqual(property.Value.GetString(), Convert.ToHexString(await SHA256.HashDataAsync(part)));
        }
        foreach (var entry in zip.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal) || entry.FullName.EndsWith(".rels", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.Open());
            var xml = XDocument.Parse(await reader.ReadToEndAsync());
            Assert.IsFalse(xml.Descendants().Any(element => element.Name.LocalName is "creator" or "lastModifiedBy" or "MachineID" && !string.IsNullOrWhiteSpace(element.Value)));
            Assert.IsFalse(xml.Descendants().Any(element => element.Attribute("TargetMode")?.Value == "External"));
        }
        using var appReader = new StreamReader(zip.GetEntry("docProps/app.xml")!.Open());
        StringAssert.Contains(await appReader.ReadToEndAsync(), root.GetProperty("producer").GetString()!);
    }

    [TestMethod]
    [DataRow("libreoffice-table.xlsx", false)]
    [DataRow("libreoffice-table.xlsx", true)]
    [DataRow("excel-table.xlsx", true)]
    [DataRow("nera-table.xlsx", false)]
    [DataRow("nera-table.xlsx", true)]
    public async Task NativeProducersShouldRetainEditedValuesReferencesAndIdentitiesAcrossThreeReopens(string fixture, bool preserve)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, fixture));
        var session = await Load(source, preserve);
        var table = session.Workbook.Tables.Single();
        var id = table.Id;
        var columns = table.Columns.Select(column => column.Id).ToArray();
        AssertValues(session, 20d);
        if (fixture == "libreoffice-table.xlsx")
        {
            // Calc retains cell formulas and Table geometry but omits these
            // metadata fields. Do not fabricate producer metadata on import.
            Assert.IsNull(table.Columns[1].CalculatedColumnFormula);
            Assert.IsNull(table.Columns[0].TotalsRowLabel);
            Assert.IsNull(table.Columns[1].TotalsRowFormula);
        }
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var amount = 25d + cycle;
            session.SetValue(new CellAddress(2, 0), amount);
            AssertValues(session, amount);
            using var saved = new MemoryStream();
            await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, saved,
                new OpenXmlExportOptions { PreserveUnknownParts = preserve });
            var bytes = saved.ToArray();
            using (var schemaStream = new MemoryStream(bytes))
            using (var document = SpreadsheetDocument.Open(schemaStream, false))
            {
                var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document).ToArray();
                Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(error => error.Description)));
            }
            session = await Load(bytes, preserve);
            table = session.Workbook.Tables.Single();
            Assert.AreEqual(id, table.Id);
            CollectionAssert.AreEqual(columns, table.Columns.Select(column => column.Id).ToArray());
            AssertValues(session, amount);
            session.Recalculate();
            AssertValues(session, amount);
        }
        CollectionAssert.AreEqual(source, await File.ReadAllBytesAsync(Path.Combine(CorpusPath, fixture)));
    }

    private static void AssertValues(SpreadsheetSession session, double amount)
    {
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        Assert.AreEqual("Table1", table.Name);
        Assert.AreEqual(new CellRange(default, new CellAddress(4, 1)), table.Range);
        Assert.IsTrue(table.HasHeaders && table.HasTotalsRow);
        Assert.AreEqual(40d + amount, sheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(amount * 2d, sheet.GetValue(new CellAddress(2, 1)));
        Assert.AreEqual((40d + amount) * 2d, sheet.GetValue(new CellAddress(4, 1)));
        Assert.AreEqual("=SUM(Table1[Amount])", sheet.GetFormula(new CellAddress(0, 3)));
        Assert.AreEqual("=Table1[[#This Row],[Amount]]*2", sheet.GetFormula(new CellAddress(2, 1)));
    }

    [TestMethod]
    [DataRow("A1:B6", false)]
    [DataRow("A2:B5", false)]
    [DataRow("A1:C5", false)]
    [DataRow("A1:B5", true)]
    public async Task CalcRangeExceptionShouldRejectOversizedRangeAndNonemptyCriteria(string range, bool addCriterion)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, "libreoffice-table.xlsx"));
        using var mutated = new MemoryStream();
        mutated.Write(source);
        mutated.Position = 0;
        using (var zip = new ZipArchive(mutated, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("xl/tables/table1.xml")!;
            XDocument xml;
            using (var input = entry.Open()) xml = XDocument.Load(input);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var filter = xml.Root!.Element(ns + "autoFilter")!;
            filter.SetAttributeValue("ref", range);
            if (addCriterion) filter.Add(new XElement(ns + "filterColumn", new XAttribute("colId", 0),
                new XElement(ns + "filters", new XElement(ns + "filter", new XAttribute("val", "10")))));
            entry.Delete();
            using var output = zip.CreateEntry("xl/tables/table1.xml").Open();
            xml.Save(output);
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(mutated.ToArray(), false));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(mutated.ToArray(), true));
    }

    [TestMethod]
    [DataRow("extension")]
    [DataRow("attribute")]
    [DataRow("sort")]
    public async Task CalcRangeExceptionShouldRejectOpaquePayloadAndSort(string mutation)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, "libreoffice-table.xlsx"));
        using var mutated = new MemoryStream();
        mutated.Write(source);
        mutated.Position = 0;
        using (var zip = new ZipArchive(mutated, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("xl/tables/table1.xml")!;
            XDocument xml;
            using (var input = entry.Open()) xml = XDocument.Load(input);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var filter = xml.Root!.Element(ns + "autoFilter")!;
            if (mutation == "extension") filter.Add(new XElement(ns + "extLst"));
            else if (mutation == "attribute") filter.SetAttributeValue("unsupported", "1");
            else xml.Root.Add(new XElement(ns + "sortState", new XAttribute("ref", "A1:B4"),
                new XElement(ns + "sortCondition", new XAttribute("ref", "A2:A4"))));
            entry.Delete();
            using var output = zip.CreateEntry("xl/tables/table1.xml").Open();
            xml.Save(output);
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(mutated.ToArray(), false));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(mutated.ToArray(), true));
    }

    private static async Task<SpreadsheetSession> Load(byte[] bytes, bool preserve)
    {
        using var stream = new MemoryStream(bytes);
        return await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(stream,
            new OpenXmlImportOptions { PreserveUnknownParts = preserve });
    }
}
