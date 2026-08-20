using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraCellValue = NeraSpreadSheet.Core.CellValue;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;
using OpenXmlCell = DocumentFormat.OpenXml.Spreadsheet.Cell;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class SharedFormulaExportTests
{
    private const string AnchorFormula =
        "=A1+$A1+A$1+$A$1+'Other Sheet'!A1+\"A1\"";
    private const string OpaqueRelationshipId = "rSharedFormulaOpaque";
    private const string OpaqueRelationshipType =
        "urn:neraspreadsheet:test:shared-formula-opaque";
    private const string OpaqueContentType =
        "application/vnd.neraspreadsheet.test.shared-formula-opaque";
    private static readonly byte[] OpaqueBytes =
        [0x4E, 0x45, 0x52, 0x41, 0x00, 0x46, 0x58];

    [TestMethod]
    public async Task ExportGroupsContinuousRectangleAndRoundTripsFormulas()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var anchor = new CellAddress(1, 1);
        SetFormulaRectangle(
            worksheet,
            anchor,
            rowCount: 2,
            columnCount: 2,
            AnchorFormula);

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        AssertSchemaValid(stream);
        AssertSharedRectangle(
            stream,
            "Data",
            new CellRange(anchor, new CellAddress(2, 2)),
            expectedSharedIndex: 0U,
            AnchorFormula);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        AssertFormulaRectangleEqual(
            worksheet,
            loaded.Worksheets[0],
            new CellRange(anchor, new CellAddress(2, 2)),
            expectCachedValues: true);
    }

    [TestMethod]
    public async Task MultipleGroupsUseStableWorksheetOrderIndexes()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        SetFormulaRectangle(
            worksheet,
            new CellAddress(1, 0),
            rowCount: 1,
            columnCount: 2,
            "=A1+1");
        SetFormulaRectangle(
            worksheet,
            new CellAddress(4, 4),
            rowCount: 2,
            columnCount: 1,
            "=D4+1");

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        AssertSharedRectangle(
            stream,
            "Data",
            new CellRange(
                new CellAddress(1, 0),
                new CellAddress(1, 1)),
            expectedSharedIndex: 0U,
            "=A1+1");
        AssertSharedRectangle(
            stream,
            "Data",
            new CellRange(
                new CellAddress(4, 4),
                new CellAddress(5, 4)),
            expectedSharedIndex: 1U,
            "=D4+1");
    }

    [TestMethod]
    public async Task DiscontiguousAndUnsupportedFormulasFallBackToNormalCells()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var anchor = new CellAddress(1, 1);
        worksheet.SetFormula(anchor, "=A1+1");
        worksheet.SetFormula(
            new CellAddress(1, 3),
            FormulaReferenceTranslator.Translate(
                "=A1+1",
                anchor,
                new CellAddress(1, 3)));
        worksheet.SetFormula(new CellAddress(4, 1), "=Table1[Amount]");
        worksheet.SetFormula(new CellAddress(4, 2), "=Table1[Amount]");

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var cells = GetWorksheetPart(document, "Data")
            .Worksheet!
            .Descendants<OpenXmlCell>()
            .ToDictionary(
                static cell => cell.CellReference!.Value!,
                StringComparer.Ordinal);
        foreach (var reference in new[] { "B2", "D2", "B5", "C5" })
        {
            var formula = cells[reference].CellFormula
                ?? throw new AssertFailedException(
                    $"Cell {reference} has no formula.");
            Assert.AreNotEqual(
                CellFormulaValues.Shared,
                formula.FormulaType?.Value,
                $"Cell {reference} was grouped unsafely.");
            Assert.IsNull(formula.SharedIndex);
            Assert.IsFalse(string.IsNullOrWhiteSpace(formula.Text));
        }
    }

    [TestMethod]
    public async Task CachedFormulaValuesFollowExportAndImportOptions()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        var range = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(1, 2));
        SetFormulaRectangle(
            worksheet,
            range.TopLeft,
            rowCount: 1,
            columnCount: 2,
            "=A1+1");

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions
            {
                WriteCachedFormulaValues = false,
            });

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            foreach (var cell in GetWorksheetPart(document, "Data")
                         .Worksheet!
                         .Descendants<OpenXmlCell>())
            {
                if (range.Contains(ParseAddress(cell)))
                {
                    Assert.IsNull(cell.CellValue);
                }
            }
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        foreach (var address in Enumerate(range))
        {
            var cell = loaded.Worksheets[0].GetCell(address);
            Assert.IsNotNull(cell.Formula);
            Assert.AreEqual(CellValueKind.Blank, cell.Value.Kind);
        }
    }

    [TestMethod]
    public async Task StructuralInsertDeleteAndReorderRegroupCurrentLogicalFormulas()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        SetFormulaRectangle(
            worksheet,
            new CellAddress(1, 1),
            rowCount: 2,
            columnCount: 2,
            "=A1+1");
        var session = new SpreadsheetSession(workbook);
        var structure = new SpreadsheetStructureController(session);
        var reorder = new SpreadsheetAxisReorderController(session);

        structure.InsertRows(0);
        structure.InsertColumns(0);
        Assert.IsTrue(reorder.MoveRows(2, 2, 6));
        Assert.IsTrue(reorder.MoveColumns(2, 2, 6));
        structure.DeleteRows(0);
        structure.DeleteColumns(0);

        var formulaCells = worksheet.EnumerateUsedCells()
            .Where(static pair => pair.Value.Formula is not null)
            .OrderBy(static pair => pair.Key.RowIndex)
            .ThenBy(static pair => pair.Key.ColumnIndex)
            .ToArray();
        Assert.AreEqual(4, formulaCells.Length);
        var formulaRange = new CellRange(
            formulaCells[0].Key,
            formulaCells[^1].Key);
        Assert.AreEqual(2, formulaRange.RowCount);
        Assert.AreEqual(2, formulaRange.ColumnCount);
        var before = formulaCells.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Formula!,
            EqualityComparer<CellAddress>.Default);

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSharedRectangle(
            stream,
            "Data",
            formulaRange,
            expectedSharedIndex: 0U,
            before[formulaRange.TopLeft]);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        foreach (var (address, formula) in before)
        {
            Assert.AreEqual(
                formula,
                loaded.Worksheets[0].GetCell(address).Formula);
        }
    }

    [TestMethod]
    public async Task PreservationRepeatedSavesKeepOpaquePartAndSharedGroups()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        var workbook = CreateWorkbook();
        var range = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(2, 2));
        SetFormulaRectangle(
            workbook.Worksheets[0],
            range.TopLeft,
            rowCount: 2,
            columnCount: 2,
            AnchorFormula);

        await using var source = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            source,
            new OpenXmlExportOptions());
        AddOpaqueWorkbookPart(source);

        source.Position = 0L;
        var preserved = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        preserved.Worksheets[0].SetValue(new CellAddress(0, 5), "first");

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            preserved,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(first);
        AssertSharedRectangle(
            first,
            "Data",
            range,
            expectedSharedIndex: 0U,
            AnchorFormula);

        preserved.Worksheets[0].SetValue(new CellAddress(0, 6), "second");
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            preserved,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(second);
        AssertSharedRectangle(
            second,
            "Data",
            range,
            expectedSharedIndex: 0U,
            AnchorFormula);
        AssertSchemaValid(second);
    }

    private static NeraWorkbook CreateWorkbook()
    {
        var workbook = new NeraWorkbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "Data");
        workbook.AddWorksheet("Other Sheet");
        return workbook;
    }

    private static void SetFormulaRectangle(
        NeraWorksheet worksheet,
        CellAddress anchor,
        int rowCount,
        int columnCount,
        string anchorFormula)
    {
        var changes = new List<KeyValuePair<CellAddress, CellData>>();
        for (var rowOffset = 0; rowOffset < rowCount; rowOffset++)
        {
            for (var columnOffset = 0;
                 columnOffset < columnCount;
                 columnOffset++)
            {
                var address = new CellAddress(
                    anchor.RowIndex + rowOffset,
                    anchor.ColumnIndex + columnOffset);
                var formula = FormulaReferenceTranslator.Translate(
                    anchorFormula,
                    anchor,
                    address);
                var cachedValue = (rowOffset + 1) * 100d +
                                  columnOffset + 1d;
                changes.Add(new KeyValuePair<CellAddress, CellData>(
                    address,
                    new CellData(
                        NeraCellValue.FromNumber(cachedValue),
                        formula)));
            }
        }
        worksheet.SetCells(changes);
    }

    private static void AssertFormulaRectangleEqual(
        NeraWorksheet expected,
        NeraWorksheet actual,
        CellRange range,
        bool expectCachedValues)
    {
        foreach (var address in Enumerate(range))
        {
            var expectedCell = expected.GetCell(address);
            var actualCell = actual.GetCell(address);
            Assert.AreEqual(expectedCell.Formula, actualCell.Formula);
            if (expectCachedValues)
            {
                Assert.AreEqual(
                    expectedCell.Value.RawValue,
                    actualCell.Value.RawValue);
            }
        }
    }

    private static void AssertSharedRectangle(
        MemoryStream stream,
        string worksheetName,
        CellRange range,
        uint expectedSharedIndex,
        string anchorFormula)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var cells = GetWorksheetPart(document, worksheetName)
            .Worksheet!
            .Descendants<OpenXmlCell>()
            .ToDictionary(
                static cell => cell.CellReference!.Value!,
                StringComparer.Ordinal);
        foreach (var address in Enumerate(range))
        {
            var reference = address.ToA1();
            var formula = cells[reference].CellFormula
                ?? throw new AssertFailedException(
                    $"Cell {reference} has no formula.");
            Assert.AreEqual(
                CellFormulaValues.Shared,
                formula.FormulaType?.Value);
            Assert.AreEqual(
                expectedSharedIndex,
                formula.SharedIndex?.Value);
            if (address == range.TopLeft)
            {
                Assert.AreEqual(range.ToString(), formula.Reference?.Value);
                Assert.AreEqual(
                    anchorFormula.StartsWith('=')
                        ? anchorFormula[1..]
                        : anchorFormula,
                    formula.Text);
            }
            else
            {
                Assert.IsNull(formula.Reference);
                Assert.IsTrue(string.IsNullOrEmpty(formula.Text));
            }
        }
        stream.Position = 0L;
    }

    private static WorksheetPart GetWorksheetPart(
        SpreadsheetDocument document,
        string worksheetName)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var sheet = workbookPart.Workbook?
            .GetFirstChild<Sheets>()?
            .Elements<Sheet>()
            .Single(candidate => candidate.Name?.Value == worksheetName)
            ?? throw new AssertFailedException(
                $"Worksheet '{worksheetName}' is missing.");
        return (WorksheetPart)workbookPart.GetPartById(
            sheet.Id?.Value
            ?? throw new AssertFailedException(
                $"Worksheet '{worksheetName}' has no relationship ID."));
    }

    private static CellAddress ParseAddress(OpenXmlCell cell)
    {
        var reference = cell.CellReference?.Value;
        if (string.IsNullOrWhiteSpace(reference) ||
            !CellAddress.TryParseA1(reference, out var address))
        {
            throw new AssertFailedException("Cell reference is invalid.");
        }
        return address;
    }

    private static IEnumerable<CellAddress> Enumerate(CellRange range)
    {
        for (var row = range.Top; row <= range.Bottom; row++)
        {
            for (var column = range.Left;
                 column <= range.Right;
                 column++)
            {
                yield return new CellAddress(row, column);
            }
        }
    }

    private static void AddOpaqueWorkbookPart(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, true);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var opaque = workbookPart.AddExtendedPart(
            OpaqueRelationshipType,
            OpaqueContentType,
            ".bin",
            OpaqueRelationshipId);
        using var target = opaque.GetStream(FileMode.Create, FileAccess.Write);
        target.Write(OpaqueBytes);
        stream.Position = 0L;
    }

    private static void AssertOpaquePart(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var part = workbookPart.GetPartById(OpaqueRelationshipId);
        using var source = part.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        CollectionAssert.AreEqual(OpaqueBytes, buffer.ToArray());
        stream.Position = 0L;
    }

    private static void AssertSchemaValid(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(static error => error.Description)));
        stream.Position = 0L;
    }
}
