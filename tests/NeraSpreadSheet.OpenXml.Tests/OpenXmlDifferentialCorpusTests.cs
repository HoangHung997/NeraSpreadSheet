using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using CellValue = NeraSpreadSheet.Core.CellValue;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using OpenXmlCell = DocumentFormat.OpenXml.Spreadsheet.Cell;
using OpenXmlCellValue = DocumentFormat.OpenXml.Spreadsheet.CellValue;
using OpenXmlWorkbook = DocumentFormat.OpenXml.Spreadsheet.Workbook;
using OpenXmlWorksheet = DocumentFormat.OpenXml.Spreadsheet.Worksheet;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class OpenXmlDifferentialCorpusTests
{
    private static readonly int[] Seeds =
    [
        17,
        101,
        313,
        997,
        4093,
        8191,
        12289,
        16381,
        24571,
        32749,
        49157,
        65521,
    ];

    [TestMethod]
    public async Task SeededSparseWorkbookCorpusIsSemanticallyStableAcrossTwoRoundTrips()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();

        foreach (var seed in Seeds)
        {
            var source = BuildWorkbook(seed);
            var expected = Capture(source);

            var first = await RoundTripAsync(serializer, source);
            AssertEquivalent(expected, Capture(first), $"seed={seed}, pass=1");

            var second = await RoundTripAsync(serializer, first);
            AssertEquivalent(expected, Capture(second), $"seed={seed}, pass=2");
        }
    }

    [TestMethod]
    public async Task SharedStringInputCanonicalizesWithoutSemanticLossAcrossSaveLoadSave()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = CreateSharedStringWorkbook();

        var imported = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions());
        var importedSheet = imported.Worksheets.Single();
        Assert.AreEqual("  repeated Ω Việt Nam  ", importedSheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("  repeated Ω Việt Nam  ", importedSheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual("<xml>&\"quoted\"", importedSheet.GetValue(new CellAddress(0, 2)));

        var expected = Capture(imported);
        var normalized = await RoundTripAsync(serializer, imported);
        AssertEquivalent(expected, Capture(normalized), "shared-string normalization pass=1");

        var normalizedAgain = await RoundTripAsync(serializer, normalized);
        AssertEquivalent(expected, Capture(normalizedAgain), "shared-string normalization pass=2");
    }

    private static NeraWorkbook BuildWorkbook(int seed)
    {
        var workbook = new NeraWorkbook();
        var styleIds = new[]
        {
            CellStyleCatalog.DefaultStyleId,
            workbook.Styles.Intern(new CellStyle
            {
                NumberFormat = new CellNumberFormatStyle
                {
                    FormatCode = "0.0000",
                },
            }),
            workbook.Styles.Intern(new CellStyle
            {
                Font = new CellFontStyle
                {
                    Family = "Segoe UI",
                    Size = 11.5d,
                    Weight = 700,
                    Italic = true,
                },
            }),
        };

        var sheetCount = 2 + (seed % 2);
        for (var sheetIndex = 0; sheetIndex < sheetCount; sheetIndex++)
        {
            var worksheet = sheetIndex == 0
                ? workbook.Worksheets[0]
                : workbook.AddWorksheet($"Corpus_{seed}_{sheetIndex}");
            if (sheetIndex == 0)
            {
                worksheet.Rename($"Seed_{seed}");
            }

            var addresses = BuildAddresses(seed, sheetIndex);
            for (var index = 0; index < addresses.Length; index++)
            {
                var address = addresses[index];
                var styleId = styleIds[(seed + sheetIndex + index) % styleIds.Length];
                if ((index + seed + sheetIndex) % 9 == 0)
                {
                    worksheet.SetCell(
                        address,
                        new CellData(
                            CellValue.FromNumber(2d),
                            "=1+1",
                            styleId));
                    continue;
                }

                worksheet.SetCell(
                    address,
                    new CellData(
                        CreateValue(seed, sheetIndex, index),
                        styleId: styleId));
            }

            var mergeTopLeft = new CellAddress(40 + (sheetIndex * 3), 8);
            var mergeRange = new CellRange(
                mergeTopLeft,
                new CellAddress(mergeTopLeft.RowIndex + 1, mergeTopLeft.ColumnIndex + 1));
            worksheet.SetValue(mergeTopLeft, $"merged-{seed}-{sheetIndex}");
            worksheet.MergeCells(mergeRange);
        }

        return workbook;
    }

    private static CellAddress[] BuildAddresses(int seed, int sheetIndex)
    {
        var addresses = new HashSet<CellAddress>
        {
            new CellAddress(0, 0),
            new CellAddress(0, SpreadsheetLimits.MaxColumns - 1),
            new CellAddress(SpreadsheetLimits.MaxRows - 1, 0),
            new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1),
            new CellAddress(1024 + (seed % 257), 64 + sheetIndex),
            new CellAddress(65_535 - sheetIndex, 255 - sheetIndex),
        };
        var random = new DeterministicRandom(
            ((uint)seed * 0x9E3779B9u) ^
            ((uint)(sheetIndex + 1) * 0x85EBCA6Bu));
        while (addresses.Count < 30)
        {
            addresses.Add(new CellAddress(
                random.Next(4096),
                random.Next(256)));
        }

        return addresses
            .OrderBy(static address => address.RowIndex)
            .ThenBy(static address => address.ColumnIndex)
            .ToArray();
    }

    private static CellValue CreateValue(int seed, int sheetIndex, int index)
    {
        return (seed + (sheetIndex * 7) + index) % 9 switch
        {
            0 => CellValue.FromNumber(0d),
            1 => CellValue.FromNumber(-987654.25d + seed),
            2 => CellValue.FromNumber(1e-12 * (seed + 1)),
            3 => CellValue.FromBoolean((seed + index) % 2 == 0),
            4 => CellValue.FromText($"shared-token-{seed % 3}"),
            5 => CellValue.FromText($"Việt Nam Ω seed={seed} sheet={sheetIndex}"),
            6 => CellValue.FromText("  <xml>&\"quoted\" giữ khoảng trắng  "),
            7 => CellValue.FromDateTime(new DateTime(
                2020 + (seed % 6),
                1 + (index % 12),
                1 + (index % 27),
                index % 24,
                (seed + index) % 60,
                0,
                DateTimeKind.Utc)),
            _ => CellValue.FromError(index % 2 == 0 ? "#N/A" : "#DIV/0!"),
        };
    }

    private static async Task<NeraWorkbook> RoundTripAsync(
        NeraOpenXmlWorkbookSerializer serializer,
        NeraWorkbook workbook)
    {
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        return await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
    }

    private static WorkbookSnapshot Capture(NeraWorkbook workbook)
    {
        var sheets = workbook.Worksheets
            .Select(worksheet => new WorksheetSnapshot(
                worksheet.Name,
                worksheet.EnumerateUsedCells()
                    .OrderBy(static pair => pair.Key.RowIndex)
                    .ThenBy(static pair => pair.Key.ColumnIndex)
                    .Select(pair => new CellSnapshot(
                        pair.Key,
                        pair.Value.Value.Kind,
                        pair.Value.Value.RawValue,
                        pair.Value.Formula,
                        workbook.Styles.Get(pair.Value.StyleId)))
                    .ToArray(),
                worksheet.MergedCells.Ranges
                    .OrderBy(static range => range.TopLeft.RowIndex)
                    .ThenBy(static range => range.TopLeft.ColumnIndex)
                    .ThenBy(static range => range.BottomRight.RowIndex)
                    .ThenBy(static range => range.BottomRight.ColumnIndex)
                    .ToArray()))
            .ToArray();
        return new WorkbookSnapshot(sheets);
    }

    private static void AssertEquivalent(
        WorkbookSnapshot expected,
        WorkbookSnapshot actual,
        string context)
    {
        Assert.AreEqual(expected.Worksheets.Length, actual.Worksheets.Length, context);
        for (var sheetIndex = 0; sheetIndex < expected.Worksheets.Length; sheetIndex++)
        {
            var expectedSheet = expected.Worksheets[sheetIndex];
            var actualSheet = actual.Worksheets[sheetIndex];
            var sheetContext = $"{context}, sheet={sheetIndex}";
            Assert.AreEqual(expectedSheet.Name, actualSheet.Name, sheetContext);
            Assert.AreEqual(expectedSheet.Cells.Length, actualSheet.Cells.Length, sheetContext);
            Assert.AreEqual(expectedSheet.MergedRanges.Length, actualSheet.MergedRanges.Length, sheetContext);

            for (var cellIndex = 0; cellIndex < expectedSheet.Cells.Length; cellIndex++)
            {
                var expectedCell = expectedSheet.Cells[cellIndex];
                var actualCell = actualSheet.Cells[cellIndex];
                var cellContext = $"{sheetContext}, cell={expectedCell.Address.ToA1()}";
                Assert.AreEqual(expectedCell.Address, actualCell.Address, cellContext);
                Assert.AreEqual(expectedCell.Kind, actualCell.Kind, cellContext);
                Assert.AreEqual(expectedCell.RawValue, actualCell.RawValue, cellContext);
                Assert.AreEqual(expectedCell.Formula, actualCell.Formula, cellContext);
                Assert.AreEqual(expectedCell.Style, actualCell.Style, cellContext);
            }

            for (var mergeIndex = 0; mergeIndex < expectedSheet.MergedRanges.Length; mergeIndex++)
            {
                Assert.AreEqual(
                    expectedSheet.MergedRanges[mergeIndex],
                    actualSheet.MergedRanges[mergeIndex],
                    $"{sheetContext}, merge={mergeIndex}");
            }
        }
    }

    private static MemoryStream CreateSharedStringWorkbook()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(
                   stream,
                   SpreadsheetDocumentType.Workbook,
                   true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new OpenXmlWorkbook();

            var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringPart.SharedStringTable = new SharedStringTable();
            sharedStringPart.SharedStringTable.Append(
                new SharedStringItem(new Text("  repeated Ω Việt Nam  ")
                {
                    Space = SpaceProcessingModeValues.Preserve,
                }),
                new SharedStringItem(new Text("<xml>&\"quoted\"")));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1U };
            row.Append(
                CreateSharedStringCell("A1", 0),
                CreateSharedStringCell("B1", 0),
                CreateSharedStringCell("C1", 1));
            sheetData.Append(row);
            worksheetPart.Worksheet = new OpenXmlWorksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = "SharedStrings",
            });

            sharedStringPart.SharedStringTable.Save();
            worksheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }

        stream.Position = 0L;
        return stream;
    }

    private static OpenXmlCell CreateSharedStringCell(
        string reference,
        int sharedStringIndex) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.SharedString,
            CellValue = new OpenXmlCellValue(sharedStringIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

    private sealed record WorkbookSnapshot(WorksheetSnapshot[] Worksheets);

    private sealed record WorksheetSnapshot(
        string Name,
        CellSnapshot[] Cells,
        CellRange[] MergedRanges);

    private sealed record CellSnapshot(
        CellAddress Address,
        CellValueKind Kind,
        object? RawValue,
        string? Formula,
        CellStyle Style);

    private sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public int Next(int exclusiveMaximum)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
            var value = NextUInt32();
            return (int)(value % (uint)exclusiveMaximum);
        }

        private uint NextUInt32()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
