using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml.Tests;

/// <summary>Synthetic Nera packages edited with OpenXML; no native producer claim.</summary>
[TestClass]
public sealed class TableCompatibilityHardeningTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Vendor = "urn:nera:synthetic:table-006";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SessionMutationsHistoryAndSerializationShouldRetainSemanticState(bool preserve)
    {
        using var source = await CreatePackage();
        var session = await Load(source, preserve);
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        session.Workbook.TableStyles.AddOrReplaceCustom(new TableStyleDefinition("custom:Corpus", "Corpus",
            [new TableStyleElement(TableStyleElementType.HeaderRow,
                new TableStyleFormat { FontWeight = 700, FillColor = TableStyleColor.FromRgb(new ColorRgba(12, 34, 56)) })]));
        var amount = table.Columns[1].Id;
        Guid inserted = default;
        Action[] mutations =
        [
            () => session.Tables.SetStyle(table.Id, "Corpus"),
            () => session.Tables.SetTotalsRow(table.Id, true),
            () => session.Tables.SetTotalsRowFunction(table.Id, amount, SpreadsheetTableTotalsFunction.Sum),
            () => session.Tables.SetAutoFilter(table.Id, new TableAutoFilter(
                [new TableFilterColumn(amount, firstCondition: new TableFilterCondition(TableFilterComparisonOperator.GreaterThan, CellValue.FromNumber(10)))],
                new SpreadsheetFilterSortState([new SpreadsheetFilterSortCondition(1, descending: true)]))),
            () => session.Tables.SetFilterButtons(table.Id, false),
            () => session.Tables.SetFirstColumn(table.Id, true),
            () => session.Tables.SetLastColumn(table.Id, true),
            () => session.Tables.SetBandedRows(table.Id, false),
            () => session.Tables.SetBandedColumns(table.Id, true),
            () => session.Tables.SetCalculatedColumnFormula(table.Id, table.Columns[0].Id, "=[@Amount]*2"),
            () => session.Tables.RenameTable(table.Id, "Orders"),
            () => session.Tables.RenameColumn(table.Id, amount, "Net"),
            () => session.Tables.InsertRow(table.Id, 2),
            () => session.Tables.DeleteRow(table.Id, 2),
            () => inserted = session.Tables.InsertColumn(table.Id, 1, "Temporary").Id,
            () => session.Tables.DeleteColumn(table.Id, inserted),
            () => session.Tables.SetHeaderRow(table.Id, false),
            () => session.Tables.SetHeaderRow(table.Id, true),
            () => session.Tables.ClearAutoFilter(table.Id),
            () => session.Tables.ConvertToRange(table.Id),
        ];
        foreach (var mutation in mutations)
        {
            var beforeCells = sheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex).ToArray();
            var beforeCount = session.History.UndoCount;
            mutation();
            Assert.AreEqual(beforeCount + 1, session.History.UndoCount);
            var expectedCells = sheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex).ToArray();
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(beforeCells, sheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex).ToArray());
            Assert.IsTrue(session.Redo());
            CollectionAssert.AreEqual(expectedCells, sheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex).ToArray());
            using var first = await Save(session, preserve);
            AssertSchemaValid(first);
            var reloaded = await Load(first, preserve);
            using var second = await Save(reloaded, preserve);
            AssertSchemaValid(second);
            var repeated = await Load(second, preserve);
            CollectionAssert.AreEqual(expectedCells, repeated.ActiveWorksheet.EnumerateUsedCells().OrderBy(pair => pair.Key.RowIndex).ThenBy(pair => pair.Key.ColumnIndex).ToArray());
            Assert.AreEqual(sheet.TableCount, repeated.ActiveWorksheet.TableCount);
            if (sheet.TableCount == 0) continue;
            var expected = sheet.Tables.Single();
            var actual = repeated.ActiveWorksheet.Tables.Single();
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Range, actual.Range);
            Assert.AreEqual(expected.HasHeaders, actual.HasHeaders);
            Assert.AreEqual(expected.HasTotalsRow, actual.HasTotalsRow);
            Assert.AreEqual(expected.ShowFilterButtons, actual.ShowFilterButtons);
            Assert.AreEqual(expected.StyleName, actual.StyleName);
            Assert.AreEqual(expected.ShowFirstColumn, actual.ShowFirstColumn);
            Assert.AreEqual(expected.ShowLastColumn, actual.ShowLastColumn);
            Assert.AreEqual(expected.ShowRowStripes, actual.ShowRowStripes);
            Assert.AreEqual(expected.ShowColumnStripes, actual.ShowColumnStripes);
            CollectionAssert.AreEqual(
                expected.Columns.Select(column => (column.Id, column.Name, column.CalculatedColumnFormula, column.TotalsRowFormula, column.TotalsRowLabel)).ToArray(),
                actual.Columns.Select(column => (column.Id, column.Name, column.CalculatedColumnFormula, column.TotalsRowFormula, column.TotalsRowLabel)).ToArray());
            Assert.AreEqual(expected.AutoFilter?.Columns.Count ?? 0, actual.AutoFilter?.Columns.Count ?? 0);
            CollectionAssert.AreEqual(expected.AutoFilter?.SortState?.Conditions.ToArray() ?? [], actual.AutoFilter?.SortState?.Conditions.ToArray() ?? []);
            Assert.IsTrue(repeated.Workbook.TableStyles.TryGet("Corpus", out var style));
            Assert.AreEqual(700, style!.Elements.Single().Format.FontWeight);
        }
    }

    [TestMethod]
    [DataRow("duplicateNumericId")]
    [DataRow("duplicateName")]
    [DataRow("duplicateRelationship")]
    [DataRow("missingRelationship")]
    [DataRow("mergedRange")]
    public async Task BrokenWorkbookTableGraphShouldRejectWithoutChangingSource(string corruption)
    {
        using var source = await CreatePackage();
        if (corruption is "duplicateName" or "duplicateNumericId")
        {
            var session = await Load(source);
            session.Tables.Create(new CellRange(new CellAddress(0, 4), new CellAddress(3, 5)), "Other");
            using var expanded = await Save(session);
            source.SetLength(0);
            expanded.CopyTo(source);
        }
        source.Position = 0;
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var sheet = document.WorkbookPart!.WorksheetParts.Single();
            var container = sheet.Worksheet!.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.TableParts>()!;
            if (corruption == "duplicateRelationship")
            {
                container.Append(container.FirstChild!.CloneNode(true));
                container.Count = 2;
            }
            else if (corruption == "missingRelationship")
                container.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.TablePart>()!.Id = "missing";
            else if (corruption == "mergedRange")
                container.InsertBeforeSelf(new DocumentFormat.OpenXml.Spreadsheet.MergeCells(
                    new DocumentFormat.OpenXml.Spreadsheet.MergeCell { Reference = "A2:B2" }) { Count = 1 });
            else
            {
                var part = sheet.TableDefinitionParts.Last();
                XDocument xml;
                using (var input = part.GetStream()) xml = XDocument.Load(input);
                if (corruption == "duplicateNumericId") xml.Root!.SetAttributeValue("id", 1);
                else
                {
                    xml.Root!.SetAttributeValue("name", "Sales");
                    xml.Root.SetAttributeValue("displayName", "Sales");
                }
                using var output = part.GetStream(FileMode.Create, FileAccess.Write);
                xml.Save(output);
            }
            sheet.Worksheet.Save();
        }
        var bytes = source.ToArray();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(source));
        CollectionAssert.AreEqual(bytes, source.ToArray());
    }

    [TestMethod]
    public async Task DuplicateStableTableIdentityAcrossWorksheetsShouldRejectImport()
    {
        using var source = await CreatePackage();
        var session = await Load(source);
        var table = session.ActiveWorksheet.Tables.Single();
        session.Workbook.AddWorksheet("Other").AddTable(new SpreadsheetTable(table.Id, "OtherTable", table.Range, table.Columns));
        using var malformed = await Save(session);
        var bytes = malformed.ToArray();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(malformed));
        CollectionAssert.AreEqual(bytes, malformed.ToArray());
    }

    [TestMethod]
    public async Task UnsupportedSortShouldRejectGeometryRewriteBeforeWritingDestination()
    {
        using var source = await CreatePackage();
        Mutate(source, root => root.Element(S + "autoFilter")!.Add(new XElement(S + "sortState",
            new XAttribute("ref", "A2:B4"), new XAttribute("columnSort", 1),
            new XElement(S + "sortCondition", new XAttribute("ref", "A2:B2")))));
        var session = await Load(source, true);
        session.Tables.InsertColumn(session.ActiveWorksheet.Tables.Single().Id, 0, "New");
        using var destination = new MemoryStream();
        destination.Write([1, 2, 3, 4]);
        var bytes = destination.ToArray();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, destination,
                new OpenXmlExportOptions { PreserveUnknownParts = true }));
        CollectionAssert.AreEqual(bytes, destination.ToArray());
    }

    [TestMethod]
    public async Task StandardTableLevelSortStateShouldRoundTripAndClear()
    {
        using var source = await CreatePackage();
        Mutate(source, root => root.Element(S + "autoFilter")!.AddAfterSelf(new XElement(S + "sortState",
            new XAttribute("ref", "A2:B4"), new XElement(S + "sortCondition",
                new XAttribute("ref", "B2:B4"), new XAttribute("descending", 1)))));
        var session = await Load(source, true);
        var table = session.ActiveWorksheet.Tables.Single();
        Assert.IsTrue(table.AutoFilter!.SortState!.Conditions.Single().Descending);
        using var first = await Save(session, true);
        AssertSchemaValid(first);
        var loaded = await Load(first, true);
        Assert.AreEqual(1, loaded.ActiveWorksheet.Tables.Single().AutoFilter!.SortState!.Conditions.Single().ColumnOffset);
        session.Tables.ClearAutoFilter(table.Id);
        using var cleared = await Save(session, true);
        var reloaded = await Load(cleared, true);
        Assert.IsNull(reloaded.ActiveWorksheet.Tables.Single().AutoFilter?.SortState);
    }

    [TestMethod]
    public async Task UnsupportedCriterionShouldSurviveHiddenButtonsAndColumnInsertion()
    {
        using var source = await CreatePackage();
        Mutate(source, root => root.Element(S + "autoFilter")!.Add(new XElement(S + "filterColumn", new XAttribute("colId", 1),
            new XElement(S + "filters", new XAttribute("futureBehavior", "keep"),
                new XElement(S + "filter", new XAttribute("val", "20"))))));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(source));
        var session = await Load(source, true);
        var table = session.ActiveWorksheet.Tables.Single();
        session.Tables.SetFilterButtons(table.Id, false);
        session.Tables.InsertColumn(table.Id, 0, "New");
        for (var cycle = 0; cycle < 2; cycle++)
        {
            using var saved = await Save(session, true);
            using (var document = SpreadsheetDocument.Open(saved, false))
            {
                using var xml = document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single().GetStream();
                var criterion = XDocument.Load(xml).Descendants(S + "filters").Single();
                Assert.AreEqual("keep", (string?)criterion.Attribute("futureBehavior"));
                Assert.AreEqual("2", (string?)criterion.Parent!.Attribute("colId"));
            }
            session = await Load(saved, true);
            Assert.IsFalse(session.ActiveWorksheet.Tables.Single().ShowFilterButtons);
        }
    }

    [TestMethod]
    public async Task TotalsRowShownShouldNotTurnLastDataRowIntoTotals()
    {
        using var source = await CreatePackage();
        Mutate(source, root => root.SetAttributeValue("totalsRowShown", 1));
        var session = await Load(source);
        var table = session.ActiveWorksheet.Tables.Single();
        Assert.IsFalse(table.HasTotalsRow);
        Assert.AreEqual(3, table.DataRange!.Value.RowCount);
    }

    [TestMethod]
    [DataRow("sum", "109")]
    [DataRow("average", "101")]
    [DataRow("countNums", "102")]
    [DataRow("count", "103")]
    [DataRow("max", "104")]
    [DataRow("min", "105")]
    public async Task StandardTotalsFunctionShouldImportIntoSharedProjection(string function, string code)
    {
        using var source = await CreatePackage();
        Mutate(source, root =>
        {
            root.SetAttributeValue("totalsRowCount", 1);
            root.SetAttributeValue("ref", "A1:B5");
            root.Element(S + "tableColumns")!.Elements().Last()
                .SetAttributeValue("totalsRowFunction", function);
        });
        var session = await Load(source);
        var table = session.ActiveWorksheet.Tables.Single();
        Assert.AreEqual($"=SUBTOTAL({code},Sales[Amount])", table.Columns[1].TotalsRowFormula);
        session.Recalculate();
        Assert.AreEqual(table.Columns[1].TotalsRowFormula,
            session.ActiveWorksheet.GetFormula(new CellAddress(4, 1)));
        using var saved = await Save(session);
        AssertSchemaValid(saved);
        var reloaded = await Load(saved);
        Assert.AreEqual(table.Columns[1].TotalsRowFormula,
            reloaded.ActiveWorksheet.Tables.Single().Columns[1].TotalsRowFormula);
    }

    [TestMethod]
    [DataRow("idMissing")]
    [DataRow("idZero")]
    [DataRow("columnNameDuplicate")]
    [DataRow("columnGuidDuplicate")]
    [DataRow("columnGuidMalformed")]
    [DataRow("columnNumericDuplicate")]
    [DataRow("columnCount")]
    [DataRow("reversedRange")]
    [DataRow("outOfBounds")]
    [DataRow("formulaDuplicate")]
    [DataRow("arrayFormula")]
    [DataRow("unsupportedTotals")]
    [DataRow("customTotalsMissingFormula")]
    [DataRow("ambiguousTotals")]
    [DataRow("sortOutsideTable")]
    public async Task MalformedTableShouldRejectWithoutChangingSource(string corruption)
    {
        using var source = await CreatePackage();
        Mutate(source, root =>
        {
            var columns = root.Element(S + "tableColumns")!.Elements().ToArray();
            switch (corruption)
            {
                case "idMissing": root.Attribute("id")!.Remove(); break;
                case "idZero": root.SetAttributeValue("id", 0); break;
                case "columnNameDuplicate": columns[1].SetAttributeValue("name", "Item"); break;
                case "columnGuidDuplicate": columns[1].SetAttributeValue("uniqueName", (string?)columns[0].Attribute("uniqueName")); break;
                case "columnGuidMalformed": columns[1].SetAttributeValue("uniqueName", "nera:broken"); break;
                case "columnNumericDuplicate": columns[1].SetAttributeValue("id", 1); break;
                case "columnCount": root.Element(S + "tableColumns")!.SetAttributeValue("count", 9); break;
                case "reversedRange": root.SetAttributeValue("ref", "B4:A1"); break;
                case "outOfBounds": root.SetAttributeValue("ref", "A1:XFE4"); break;
                case "formulaDuplicate": columns[1].Add(new XElement(S + "calculatedColumnFormula", "1"), new XElement(S + "calculatedColumnFormula", "2")); break;
                case "arrayFormula": columns[1].Add(new XElement(S + "calculatedColumnFormula", new XAttribute("array", 1), "1")); break;
                case "unsupportedTotals": columns[1].SetAttributeValue("totalsRowFunction", "stdDev"); break;
                case "customTotalsMissingFormula": columns[1].SetAttributeValue("totalsRowFunction", "custom"); break;
                case "ambiguousTotals": columns[1].SetAttributeValue("totalsRowLabel", "Total"); columns[1].Add(new XElement(S + "totalsRowFormula", "1")); break;
                case "sortOutsideTable": root.Element(S + "autoFilter")!.Add(new XElement(S + "sortState", new XAttribute("ref", "A2:B999"), new XElement(S + "sortCondition", new XAttribute("ref", "B2:B999")))); break;
            }
        });
        var before = source.ToArray();
        foreach (var preserve in new[] { false, true })
        {
            source.Position = 0;
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => Load(source, preserve));
            CollectionAssert.AreEqual(before, source.ToArray());
        }
    }

    [TestMethod]
    public async Task ForeignRelationshipsAndColumnExtensionsShouldSurviveRenameAndRepeatedSave()
    {
        using var source = await CreatePackage();
        string partUri;
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var sheet = document.WorkbookPart!.WorksheetParts.Single();
            var part = sheet.TableDefinitionParts.Single();
            partUri = part.Uri.ToString();
            sheet.ChangeIdOfPart(part, "rIdForeignTable");
            sheet.Worksheet!.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.TableParts>()!
                .GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.TablePart>()!.Id = "rIdForeignTable";
            sheet.Worksheet!.Save();
        }
        Mutate(source, root =>
        {
            root.SetAttributeValue("id", 42);
            root.SetAttributeValue(Vendor + "marker", "root");
            root.Add(Extension("table"));
            foreach (var column in root.Element(S + "tableColumns")!.Elements())
            {
                column.Attribute("uniqueName")!.Remove();
                column.SetAttributeValue(Vendor + "marker", (string?)column.Attribute("name"));
                column.Add(Extension((string)column.Attribute("name")!));
            }
        });
        var session = await Load(source, true);
        var initial = session.ActiveWorksheet.Tables.Single();
        session.Tables.RenameTable(initial.Id, "Orders");
        session.Tables.RenameColumn(initial.Id, initial.Columns[1].Id, "Net Amount");
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Redo());
        for (var cycle = 0; cycle < 3; cycle++)
        {
            using var saved = await Save(session, true);
            using (var document = SpreadsheetDocument.Open(saved, false))
            {
                var sheet = document.WorkbookPart!.WorksheetParts.Single();
                var part = sheet.TableDefinitionParts.Single();
                Assert.AreEqual("rIdForeignTable", sheet.GetIdOfPart(part));
                Assert.AreEqual(partUri, part.Uri.ToString());
                using var xmlStream = part.GetStream();
                var root = XDocument.Load(xmlStream).Root!;
                Assert.AreEqual("42", (string?)root.Attribute("id"));
                Assert.AreEqual("root", (string?)root.Attribute(Vendor + "marker"));
                Assert.AreEqual(3, root.Descendants(Vendor + "payload").Count());
                Assert.AreEqual("Amount", (string?)root.Element(S + "tableColumns")!.Elements().Last().Attribute(Vendor + "marker"));
            }
            saved.Position = 0;
            session = await Load(saved, true);
            var current = session.ActiveWorksheet.Tables.Single();
            Assert.AreEqual(initial.Id, current.Id);
            CollectionAssert.AreEqual(initial.Columns.Select(column => column.Id).ToArray(), current.Columns.Select(column => column.Id).ToArray());
            Assert.AreEqual("Orders", current.Name);
            Assert.AreEqual("Net Amount", current.Columns[1].Name);
        }
    }

    private static XElement Extension(string marker) => new(S + "extLst",
        new XElement(S + "ext", new XAttribute("uri", "{76B64D37-33F0-4AD1-8819-CAFEFD6F0010}"),
            new XElement(Vendor + "payload", new XAttribute("marker", marker))));

    private static async Task<MemoryStream> CreatePackage()
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        sheet.SetValue(default, "Item");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), $"Item {row}");
            sheet.SetValue(new CellAddress(row, 1), row * 10d);
        }
        session.Tables.Create(new CellRange(default, new CellAddress(3, 1)), "Sales");
        return await Save(session);
    }

    private static async Task<MemoryStream> Save(SpreadsheetSession session, bool preserve = false)
    {
        var stream = new MemoryStream();
        await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, stream,
            new OpenXmlExportOptions { PreserveUnknownParts = preserve });
        stream.Position = 0;
        return stream;
    }

    private static Task<SpreadsheetSession> Load(Stream stream, bool preserve = false)
    {
        stream.Position = 0;
        return new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(stream,
            new OpenXmlImportOptions { PreserveUnknownParts = preserve });
    }

    private static void Mutate(MemoryStream stream, Action<XElement> mutation)
    {
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single();
            XDocument xml;
            using (var input = part.GetStream()) xml = XDocument.Load(input);
            mutation(xml.Root!);
            using var output = part.GetStream(FileMode.Create, FileAccess.Write);
            xml.Save(output);
        }
        stream.Position = 0;
    }

    private static void AssertSchemaValid(Stream stream)
    {
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013).Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(Environment.NewLine, errors.Select(error => error.Description)));
    }
}
