using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class SharedFormulaImportTests
{
    private const uint SharedIndex = 7U;
    private const string AnchorFormula =
        "A1+$A1+A$1+$A$1+'Other Sheet'!A1+\"A1\"";

    [TestMethod]
    public async Task SharedFormulaImportExpandsRelativeReferencesAndKeepsCachedValues()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = await CreateSharedFormulaPackageAsync(
            serializer,
            new SharedFixtureOptions());
        AssertSchemaValid(stream);

        stream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var worksheet = workbook.Worksheets[0];

        Assert.AreEqual(
            "=A1+$A1+A$1+$A$1+'Other Sheet'!A1+\"A1\"",
            worksheet.GetCell(new CellAddress(1, 1)).Formula);
        Assert.AreEqual(
            "=B1+$A1+B$1+$A$1+'Other Sheet'!B1+\"A1\"",
            worksheet.GetCell(new CellAddress(1, 2)).Formula);
        Assert.AreEqual(
            "=A2+$A2+A$1+$A$1+'Other Sheet'!A2+\"A1\"",
            worksheet.GetCell(new CellAddress(2, 1)).Formula);
        Assert.AreEqual(
            "=B2+$A2+B$1+$A$1+'Other Sheet'!B2+\"A1\"",
            worksheet.GetCell(new CellAddress(2, 2)).Formula);

        Assert.AreEqual(
            11d,
            worksheet.GetCell(new CellAddress(1, 1)).Value.RawValue);
        Assert.AreEqual(
            12d,
            worksheet.GetCell(new CellAddress(1, 2)).Value.RawValue);
        Assert.AreEqual(
            21d,
            worksheet.GetCell(new CellAddress(2, 1)).Value.RawValue);
        Assert.AreEqual(
            22d,
            worksheet.GetCell(new CellAddress(2, 2)).Value.RawValue);
        Assert.AreEqual(4, worksheet.UsedCellCount);
    }

    [TestMethod]
    public async Task SharedFormulaImportCanDropCachedValuesWithoutDroppingFormulas()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = await CreateSharedFormulaPackageAsync(
            serializer,
            new SharedFixtureOptions());

        stream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions
            {
                LoadCachedFormulaValues = false,
            });
        var worksheet = workbook.Worksheets[0];

        foreach (var address in new[]
                 {
                     new CellAddress(1, 1),
                     new CellAddress(1, 2),
                     new CellAddress(2, 1),
                     new CellAddress(2, 2),
                 })
        {
            var cell = worksheet.GetCell(address);
            Assert.IsNotNull(cell.Formula);
            Assert.AreEqual(CellValueKind.Blank, cell.Value.Kind);
        }
    }

    [TestMethod]
    public async Task SharedFormulaFollowersMayAppearBeforeAnchorInWorksheetXml()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = await CreateSharedFormulaPackageAsync(
            serializer,
            new SharedFixtureOptions
            {
                FollowersFirst = true,
            });

        stream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());

        Assert.AreEqual(
            "=B2+$A2+B$1+$A$1+'Other Sheet'!B2+\"A1\"",
            workbook.Worksheets[0]
                .GetCell(new CellAddress(2, 2))
                .Formula);
    }

    [TestMethod]
    public async Task MissingSharedFormulaAnchorIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                IncludeAnchor = false,
            });
    }

    [TestMethod]
    public async Task DuplicateSharedFormulaAnchorIndexIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                DuplicateAnchor = true,
            });
    }

    [TestMethod]
    public async Task SharedFormulaFollowerOutsideDeclaredRangeIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                AnchorRange = "B2:B3",
            });
    }

    [TestMethod]
    public async Task MissingSharedFormulaIndexIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                OmitAnchorIndex = true,
            });
    }

    [TestMethod]
    public async Task ReversedSharedFormulaRangeIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                AnchorRange = "C3:B2",
            });
    }

    [TestMethod]
    public async Task SharedFormulaFollowerWithOwnRangeIsRejected()
    {
        await AssertImportFailsAsync(
            new SharedFixtureOptions
            {
                FollowerDeclaresRange = true,
            });
    }

    private static async Task AssertImportFailsAsync(
        SharedFixtureOptions options)
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = await CreateSharedFormulaPackageAsync(
            serializer,
            options);
        stream.Position = 0L;

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                stream,
                new OpenXmlImportOptions()));
    }

    private static async Task<MemoryStream> CreateSharedFormulaPackageAsync(
        NeraOpenXmlWorkbookSerializer serializer,
        SharedFixtureOptions options)
    {
        var workbook = new NeraWorkbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "Data");
        workbook.AddWorksheet("Other Sheet");
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException(
                    "The shared-formula fixture is missing its workbook part.");
            var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()
                ?? throw new AssertFailedException(
                    "The shared-formula fixture is missing its sheets collection.");
            var dataSheet = sheets.Elements<Sheet>()
                .Single(sheet => sheet.Name?.Value == "Data");
            var relationshipId = dataSheet.Id?.Value
                ?? throw new AssertFailedException(
                    "The shared-formula fixture sheet has no relationship identifier.");
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(
                relationshipId);
            var openXmlWorksheet = worksheetPart.Worksheet
                ?? throw new AssertFailedException(
                    "The shared-formula fixture is missing worksheet markup.");
            var sheetData = openXmlWorksheet.GetFirstChild<SheetData>()
                ?? openXmlWorksheet.AppendChild(new SheetData());
            sheetData.RemoveAllChildren<Row>();

            var row2 = new Row
            {
                RowIndex = 2U,
            };
            if (options.IncludeAnchor)
            {
                row2.Append(CreateSharedCell(
                    "B2",
                    AnchorFormula,
                    options.OmitAnchorIndex ? null : SharedIndex,
                    options.AnchorRange,
                    11d));
            }
            row2.Append(CreateSharedCell(
                "C2",
                formulaText: null,
                SharedIndex,
                options.FollowerDeclaresRange ? "C2:C2" : null,
                12d));
            if (options.DuplicateAnchor)
            {
                row2.Append(CreateSharedCell(
                    "D2",
                    "A1",
                    SharedIndex,
                    "D2:D2",
                    13d));
            }

            var row3 = new Row
            {
                RowIndex = 3U,
            };
            row3.Append(CreateSharedCell(
                "B3",
                formulaText: null,
                SharedIndex,
                range: null,
                21d));
            row3.Append(CreateSharedCell(
                "C3",
                formulaText: null,
                SharedIndex,
                range: null,
                22d));

            if (options.FollowersFirst)
            {
                sheetData.Append(row3);
                sheetData.Append(row2);
            }
            else
            {
                sheetData.Append(row2);
                sheetData.Append(row3);
            }
            openXmlWorksheet.Save();
        }

        stream.Position = 0L;
        return stream;
    }

    private static Cell CreateSharedCell(
        string reference,
        string? formulaText,
        uint? sharedIndex,
        string? range,
        double cachedValue)
    {
        var formula = new CellFormula(formulaText ?? string.Empty);
        SetAttribute(formula, "t", "shared");
        if (sharedIndex is uint index)
        {
            SetAttribute(
                formula,
                "si",
                index.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(range))
        {
            SetAttribute(formula, "ref", range);
        }

        return new Cell
        {
            CellReference = reference,
            CellFormula = formula,
            CellValue = new CellValue(
                cachedValue.ToString(
                    "R",
                    CultureInfo.InvariantCulture)),
        };
    }

    private static void SetAttribute(
        OpenXmlElement element,
        string localName,
        string value) =>
        element.SetAttribute(new OpenXmlAttribute(
            string.Empty,
            localName,
            string.Empty,
            value));

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

    private sealed class SharedFixtureOptions
    {
        public bool FollowersFirst { get; init; }

        public bool IncludeAnchor { get; init; } = true;

        public bool DuplicateAnchor { get; init; }

        public bool OmitAnchorIndex { get; init; }

        public string AnchorRange { get; init; } = "B2:C3";

        public bool FollowerDeclaresRange { get; init; }
    }
}
