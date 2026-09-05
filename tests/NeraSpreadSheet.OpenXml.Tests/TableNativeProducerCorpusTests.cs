using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed partial class TableNativeProducerCorpusTests
{
    private static readonly string CorpusPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TableNative");
    private static readonly string[] ExpectedColumns = ["Amount", "Double"];
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    [DataRow("excel-table.xlsx", true)]
    [DataRow("nera-table.xlsx", false)]
    [DataRow("nera-table.xlsx", true)]
    public async Task NativeProducerShouldPreserveTableSemanticsAcrossRepeatedSessionRoundTrips(string fixture, bool preserve)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, fixture));
        AssertSchemaValid(source);
        var session = await Load(source, preserve);
        AssertExpectedSemantics(session);
        var original = session.Workbook.Tables.Single();
        var originalColumns = original.Columns.Select(column => column.Id).ToArray();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var bytes = await Save(session, preserve);
            AssertSchemaValid(bytes);
            AssertPrivacy(bytes);
            if (preserve) AssertTableGraphPreserved(source, bytes);
            session = await Load(bytes, preserve);
            AssertExpectedSemantics(session);
            Assert.AreEqual(original.Id, session.Workbook.Tables.Single().Id);
            CollectionAssert.AreEqual(originalColumns, session.Workbook.Tables.Single().Columns.Select(column => column.Id).ToArray());
            session.Recalculate();
            AssertExpectedSemantics(session);
        }
        CollectionAssert.AreEqual(source, await File.ReadAllBytesAsync(Path.Combine(CorpusPath, fixture)));
    }

    [TestMethod]
    [DataRow("excel-table.xlsx")]
    [DataRow("nera-table.xlsx")]
    public async Task NativeProducerShouldRetainValuesWhenConvertedAndUndone(string fixture)
    {
        var session = await Load(await File.ReadAllBytesAsync(Path.Combine(CorpusPath, fixture)), true);
        var id = session.Workbook.Tables.Single().Id;
        Assert.IsTrue(session.Tables.ConvertToRange(id));
        Assert.AreEqual(0, session.ActiveWorksheet.TableCount);
        Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(120d, session.ActiveWorksheet.GetValue(new CellAddress(4, 1)));
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        AssertExpectedSemantics(session);
        Assert.AreEqual(id, session.Workbook.Tables.Single().Id);
        Assert.IsTrue(session.Redo());
        session.SetValue(new CellAddress(2, 0), 25d);
        Assert.AreEqual(65d, session.ActiveWorksheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(50d, session.ActiveWorksheet.GetValue(new CellAddress(2, 1)));
        Assert.AreEqual(130d, session.ActiveWorksheet.GetValue(new CellAddress(4, 1)));
    }

    [TestMethod]
    public async Task NativeExcelStrictImportShouldRejectPreserveOnlyTableStyleReferenceWithoutChangingBytes()
    {
        var path = Path.Combine(CorpusPath, "excel-table.xlsx");
        var source = await File.ReadAllBytesAsync(path);
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(source, false));
        StringAssert.Contains(exception.Message, "dataDxfId");
        CollectionAssert.AreEqual(source, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    [DataRow("999")]
    [DataRow("-1")]
    [DataRow("missing")]
    public async Task NativeTableShouldRejectUnavailableProducerDxfWithoutChangingSource(string reference)
    {
        var original = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, "excel-table.xlsx"));
        var source = MutatePackage(original, document =>
        {
            if (reference == "missing")
                MutateXml(document.WorkbookPart!.WorkbookStylesPart!, root => root.Element(S + "dxfs")!.Remove());
            else
                MutateXml(document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single(),
                    root => root.Descendants(S + "tableColumn").Last().SetAttributeValue("dataDxfId", reference));
        });
        var before = source.ToArray();
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(source, true));
        StringAssert.Contains(exception.Message, "differential-style table");
        CollectionAssert.AreEqual(before, source);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeTableDxfShouldKeepBindingsAlongsideEditedConditionalRulesFiltersAndCustomStyles(bool opaque)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, "excel-table.xlsx"));
        if (opaque)
            source = MutatePackage(source, document => MutateXml(document.WorkbookPart!.WorksheetParts.Single(), root =>
                root.Element(S + "pageMargins")!.AddBeforeSelf(new XElement(S + "conditionalFormatting",
                    new XAttribute("sqref", "A2:A4"), new XElement(S + "cfRule", new XAttribute("type", "duplicateValues"),
                        new XAttribute("priority", 9), new XAttribute("dxfId", 0))))));
        var session = await Load(source, true);
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        var styleId = sheet.DifferentialStyles.Intern(new CellStylePatch { FontWeight = 700 });
        Assert.AreEqual(0, styleId);
        sheet.AddConditionalFormattingRule(new ConditionalFormattingRule(Guid.NewGuid(),
            [new CellRange(new CellAddress(1, 0), new CellAddress(3, 0))],
            ConditionalFormattingRuleType.Expression, ConditionalFormattingOperator.Equal, "=A2>5", null, styleId, 1));
        var blue = new ColorRgba(30, 120, 210);
        session.Tables.SetAutoFilter(table.Id, new TableAutoFilter(
            [new TableFilterColumn(table.Columns[0].Id, colorFilter: new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, blue))],
            new SpreadsheetFilterSortState([new SpreadsheetFilterSortCondition(0, descending: true,
                sortBy: SpreadsheetFilterSortBy.CellColor, color: new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, blue))])));
        session.Workbook.TableStyles.AddOrReplaceCustom(new TableStyleDefinition("custom:Native", "Native",
            [new TableStyleElement(TableStyleElementType.HeaderRow, new TableStyleFormat { FontWeight = 700, FillColor = TableStyleColor.FromRgb(blue) })]));
        session.Tables.SetStyle(table.Id, "Native");
        int? previousCount = null;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var bytes = await Save(session, true);
            AssertSchemaValid(bytes);
            using var stream = new MemoryStream(bytes);
            using var document = SpreadsheetDocument.Open(stream, false);
            var styles = ReadXml(document.WorkbookPart!.WorkbookStylesPart!).Root!;
            var dxfs = styles.Element(S + "dxfs")!.Elements(S + "dxf").ToArray();
            if (previousCount is { } count) Assert.AreEqual(count, dxfs.Length);
            previousCount = dxfs.Length;
            Assert.AreEqual("General", (string?)dxfs[0].Element(S + "numFmt")?.Attribute("formatCode"));
            var sheetXml = ReadXml(document.WorkbookPart.WorksheetParts.Single());
            if (opaque)
            {
                // Existing preservation contract freezes the entire CF set when
                // opaque rules are present. The attempted managed addition above
                // must not alter that set or its original indices/priorities.
                var originalRule = sheetXml.Descendants(S + "cfRule").Single();
                Assert.AreEqual("duplicateValues", (string?)originalRule.Attribute("type"));
                Assert.AreEqual("0", (string?)originalRule.Attribute("dxfId"));
                Assert.AreEqual("9", (string?)originalRule.Attribute("priority"));
            }
            else
            {
                var managedRule = sheetXml.Descendants(S + "cfRule").Single(rule => (string?)rule.Attribute("type") == "expression");
                var managedId = (int)managedRule.Attribute("dxfId")!;
                Assert.AreNotEqual(0, managedId, "Generated CF index zero must not bind to native General dxf zero.");
                Assert.IsNotNull(dxfs[managedId].Descendants(S + "b").SingleOrDefault());
                Assert.AreEqual(cycle == 0 ? "A2>5" : "A2>15", managedRule.Element(S + "formula")!.Value);
            }
            var tableXml = ReadXml(document.WorkbookPart.WorksheetParts.Single().TableDefinitionParts.Single());
            Assert.AreEqual("0", (string?)tableXml.Descendants(S + "tableColumn").Last().Attribute("dataDxfId"));
            var colorId = (int)tableXml.Descendants(S + "colorFilter").Single().Attribute("dxfId")!;
            Assert.AreEqual("FF1E78D2", (string?)dxfs[colorId].Descendants(S + "fgColor").Single().Attribute("rgb"));
            Assert.AreEqual(colorId, (int)tableXml.Descendants(S + "sortCondition").Single().Attribute("dxfId")!);
            var customStyleId = (int)styles.Descendants(S + "tableStyleElement").Single().Attribute("dxfId")!;
            Assert.IsNotNull(dxfs[customStyleId].Descendants(S + "b").SingleOrDefault());
            session = await Load(bytes, true);
            sheet = session.ActiveWorksheet;
            Assert.IsTrue(sheet.Tables.Single().AutoFilter!.SortState!.Conditions.Single().Descending);
            if (cycle == 0 && !opaque)
            {
                var rule = sheet.ConditionalFormattingRules.Single();
                sheet.RemoveConditionalFormattingRule(rule.Id);
                sheet.AddConditionalFormattingRule(new ConditionalFormattingRule(rule.Id, rule.Ranges,
                    rule.Type, rule.Operator, "=A2>15", null, rule.DifferentialStyleId, rule.Priority));
            }
        }
    }

    private static byte[] MutatePackage(byte[] source, Action<SpreadsheetDocument> mutation)
    {
        using var stream = new MemoryStream();
        stream.Write(source);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true)) mutation(document);
        return stream.ToArray();
    }

    private static XDocument ReadXml(OpenXmlPart part)
    {
        using var input = part.GetStream();
        return XDocument.Load(input);
    }

    private static void MutateXml(OpenXmlPart part, Action<XElement> mutation)
    {
        var xml = ReadXml(part);
        mutation(xml.Root!);
        using var output = part.GetStream(FileMode.Create, FileAccess.Write);
        xml.Save(output);
    }

    [TestMethod]
    public async Task CheckedInCorpusShouldMatchProvenanceHashesAndContainNoPrivateMetadata()
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(CorpusPath, "provenance.json")));
        foreach (var entry in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(CorpusPath, entry.GetProperty("file").GetString()!));
            Assert.AreEqual(entry.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
            AssertPrivacy(bytes);
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("producer").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("version").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("license").GetString()));
        }
    }

    private static void AssertExpectedSemantics(SpreadsheetSession session)
    {
        var sheet = session.ActiveWorksheet;
        var table = session.Workbook.Tables.Single();
        Assert.AreEqual("Table1", table.Name);
        Assert.AreEqual(new CellRange(default, new CellAddress(4, 1)), table.Range);
        Assert.IsTrue(table.HasHeaders);
        Assert.IsTrue(table.HasTotalsRow);
        CollectionAssert.AreEqual(ExpectedColumns, table.Columns.Select(column => column.Name).ToArray());
        Assert.AreEqual("=Table1[[#This Row],[Amount]]*2", table.Columns[1].CalculatedColumnFormula);
        Assert.AreEqual("Total", table.Columns[0].TotalsRowLabel);
        Assert.AreEqual("TableStyleMedium2", table.StyleName);
        Assert.IsTrue(table.ShowRowStripes);
        Assert.AreEqual("=SUM(Table1[Amount])", sheet.GetFormula(new CellAddress(0, 3)));
        Assert.AreEqual(60d, sheet.GetValue(new CellAddress(0, 3)));
        for (var row = 1; row <= 3; row++)
        {
            Assert.AreEqual(row * 10d, sheet.GetValue(new CellAddress(row, 0)));
            Assert.AreEqual(row * 20d, sheet.GetValue(new CellAddress(row, 1)));
            Assert.AreEqual("=Table1[[#This Row],[Amount]]*2", sheet.GetFormula(new CellAddress(row, 1)));
        }
        Assert.AreEqual(120d, sheet.GetValue(new CellAddress(4, 1)));
    }

    private static void AssertTableGraphPreserved(byte[] source, byte[] saved)
    {
        using var originalStream = new MemoryStream(source);
        using var savedStream = new MemoryStream(saved);
        using var originalDocument = SpreadsheetDocument.Open(originalStream, false);
        using var savedDocument = SpreadsheetDocument.Open(savedStream, false);
        var originalSheet = originalDocument.WorkbookPart!.WorksheetParts.Single();
        var savedSheet = savedDocument.WorkbookPart!.WorksheetParts.Single();
        var originalTable = originalSheet.TableDefinitionParts.Single();
        var savedTable = savedSheet.TableDefinitionParts.Single();
        Assert.AreEqual(originalTable.Uri, savedTable.Uri);
        Assert.AreEqual(originalSheet.GetIdOfPart(originalTable), savedSheet.GetIdOfPart(savedTable));
        Assert.AreEqual(originalTable.Table!.Id!.Value, savedTable.Table!.Id!.Value);
        CollectionAssert.AreEqual(originalTable.Table.TableColumns!.Elements<DocumentFormat.OpenXml.Spreadsheet.TableColumn>().Select(column => column.Id!.Value).ToArray(),
            savedTable.Table.TableColumns!.Elements<DocumentFormat.OpenXml.Spreadsheet.TableColumn>().Select(column => column.Id!.Value).ToArray());
    }

    private static void AssertPrivacy(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var package = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in package.Entries)
        {
            Assert.IsFalse(entry.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase));
            if (!entry.FullName.EndsWith(".xml", StringComparison.Ordinal) && !entry.FullName.EndsWith(".rels", StringComparison.Ordinal)) continue;
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Assert.IsFalse(PrivatePathPattern().IsMatch(text), $"Private path/identifier in {entry.FullName}.");
            var xml = XDocument.Parse(text);
            Assert.IsFalse(xml.Descendants().Any(element => element.Name.LocalName is "absPath" or "revisionPtr" or "MachineID"));
            Assert.IsFalse(xml.Descendants().Any(element => element.Name.LocalName is "creator" or "lastModifiedBy" or "Company" && !string.IsNullOrWhiteSpace(element.Value)));
            Assert.IsFalse(xml.Descendants().Any(element => element.Attribute("TargetMode")?.Value == "External"));
        }
    }

    [GeneratedRegex(@"(?i)(?:(?<![a-z0-9])[a-z]:[\\/]|file:/{2}|\\\\[^\\\s]+\\|(?:machineid|token|password)\s*=)")]
    private static partial Regex PrivatePathPattern();

    private static void AssertSchemaValid(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(Environment.NewLine, errors.Select(error => error.Description)));
    }

    private static async Task<SpreadsheetSession> Load(byte[] bytes, bool preserve)
    {
        using var stream = new MemoryStream(bytes);
        return await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(stream, new OpenXmlImportOptions { PreserveUnknownParts = preserve });
    }

    private static async Task<byte[]> Save(SpreadsheetSession session, bool preserve)
    {
        using var stream = new MemoryStream();
        await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, stream, new OpenXmlExportOptions { PreserveUnknownParts = preserve });
        return stream.ToArray();
    }
}
