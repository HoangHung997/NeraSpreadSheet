using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class ConditionalFormattingRoundTripTests
{
    private const string OpaqueRelationshipId = "rConditionalOpaque";
    private const string OpaqueRelationshipType =
        "urn:neraspreadsheet:test:conditional-opaque";
    private const string OpaqueContentType =
        "application/vnd.neraspreadsheet.test.conditional-opaque";
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly byte[] OpaqueBytes =
        [0x4E, 0x45, 0x52, 0x41, 0x00, 0x43, 0x46];

    [TestMethod]
    public async Task CellIsAndExpressionRulesRoundTripWithSchemaValidDxfs()
    {
        var workbook = CreateConditionalWorkbook();
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        AssertSchemaValid(stream);
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part is missing.");
            var styleXml = LoadPartXml(
                workbookPart.WorkbookStylesPart
                ?? throw new AssertFailedException("Style part is missing."));
            var dxfs = styleXml.Root?
                .Element(SpreadsheetNamespace + "dxfs")
                ?? throw new AssertFailedException("dxfs was not written.");
            Assert.AreEqual("2", (string?)dxfs.Attribute("count"));
            Assert.AreEqual(
                2,
                dxfs.Elements(SpreadsheetNamespace + "dxf").Count());

            var worksheetXml = LoadPartXml(
                workbookPart.WorksheetParts.Single());
            var rules = worksheetXml
                .Descendants(SpreadsheetNamespace + "cfRule")
                .ToArray();
            Assert.AreEqual(2, rules.Length);
            Assert.AreEqual("expression", (string?)rules[0].Attribute("type"));
            Assert.AreEqual("1", (string?)rules[0].Attribute("priority"));
            Assert.AreEqual("1", (string?)rules[0].Attribute("stopIfTrue"));
            Assert.AreEqual("cellIs", (string?)rules[1].Attribute("type"));
            Assert.AreEqual(
                "greaterThan",
                (string?)rules[1].Attribute("operator"));
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var worksheet = loaded.Worksheets[0];
        Assert.AreEqual(2, worksheet.ConditionalFormattingRuleCount);
        var rulesAfter = worksheet.ConditionalFormattingRules
            .OrderBy(static rule => rule.Priority)
            .ToArray();
        Assert.AreEqual(
            ConditionalFormattingRuleType.Expression,
            rulesAfter[0].Type);
        Assert.AreEqual("=A2>0", rulesAfter[0].Formula1);
        Assert.IsTrue(rulesAfter[0].StopIfTrue);
        Assert.AreEqual(
            ConditionalFormattingRuleType.CellIs,
            rulesAfter[1].Type);
        Assert.AreEqual(
            ConditionalFormattingOperator.GreaterThan,
            rulesAfter[1].Operator);
        Assert.AreEqual("=0", rulesAfter[1].Formula1);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 1)),
            rulesAfter[0].Ranges.Single());
        Assert.AreEqual(
            CreateHighPriorityPatch(),
            worksheet.DifferentialStyles.Get(
                rulesAfter[0].DifferentialStyleId));
        Assert.AreEqual(
            CreateLowPriorityPatch(),
            worksheet.DifferentialStyles.Get(
                rulesAfter[1].DifferentialStyleId));
    }

    [TestMethod]
    public async Task BetweenRuleWithMultipleRangesRoundTrips()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Data");
        var patch = new CellStylePatch
        {
            NumberFormatCode = "0.00",
            Border = new CellBorderStyle
            {
                Left = CreateBorderSide(),
                Top = CreateBorderSide(),
                Right = CreateBorderSide(),
                Bottom = CreateBorderSide(),
            },
        };
        var styleId = worksheet.DifferentialStyles.Intern(patch);
        var ranges = new[]
        {
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 1)),
            new CellRange(
                new CellAddress(1, 3),
                new CellAddress(2, 3)),
        };
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                ranges,
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.Between,
                "=1",
                "=10",
                styleId,
                priority: 1));

        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSchemaValid(stream);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var rule = loaded.Worksheets[0]
            .ConditionalFormattingRules.Single();
        Assert.AreEqual(
            ConditionalFormattingOperator.Between,
            rule.Operator);
        Assert.AreEqual("=1", rule.Formula1);
        Assert.AreEqual("=10", rule.Formula2);
        CollectionAssert.AreEqual(
            ranges,
            rule.Ranges.ToArray());
        Assert.AreEqual(
            patch,
            loaded.Worksheets[0].DifferentialStyles.Get(
                rule.DifferentialStyleId));
    }

    [TestMethod]
    public async Task ExcelBackgroundOnlyDifferentialFillsImportAsSolidFills()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        foreach (var patternType in new string?[] { null, "solid" })
        {
            await using var source = await CreatePackageAsync(serializer);
            MutateStyleTable(source, root =>
            {
                var pattern = root
                    .Element(SpreadsheetNamespace + "dxfs")?
                    .Descendants(SpreadsheetNamespace + "patternFill")
                    .First()
                    ?? throw new AssertFailedException(
                        "The generated differential fill is missing patternFill.");
                pattern.SetAttributeValue("patternType", patternType);
                var foreground = pattern.Element(
                    SpreadsheetNamespace + "fgColor")
                    ?? throw new AssertFailedException(
                        "The generated differential fill is missing fgColor.");
                pattern.Element(SpreadsheetNamespace + "bgColor")?.Remove();
                foreground.Name = SpreadsheetNamespace + "bgColor";
            });

            source.Position = 0L;
            var loaded = await serializer.LoadAsync(
                source,
                new OpenXmlImportOptions());
            var worksheet = loaded.Worksheets[0];
            var rule = worksheet.ConditionalFormattingRules
                .OrderBy(static item => item.Priority)
                .First();
            var fill = worksheet.DifferentialStyles
                .Get(rule.DifferentialStyleId)
                .Fill;

            Assert.IsNotNull(fill);
            Assert.IsTrue(fill.IsVisible);
            Assert.AreEqual(new ColorRgba(245, 210, 70), fill.Color);
        }
    }

    [TestMethod]
    public async Task DuplicatePriorityAndOutOfRangeDxfAreRejected()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var duplicatePriority =
            await CreatePackageAsync(serializer);
        MutateFirstWorksheet(duplicatePriority, root =>
        {
            var rules = root
                .Descendants(SpreadsheetNamespace + "cfRule")
                .ToArray();
            rules[1].SetAttributeValue("priority", 1);
        });
        duplicatePriority.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                duplicatePriority,
                new OpenXmlImportOptions()));

        await using var invalidDxf =
            await CreatePackageAsync(serializer);
        MutateFirstWorksheet(invalidDxf, root =>
        {
            root.Descendants(SpreadsheetNamespace + "cfRule")
                .First()
                .SetAttributeValue("dxfId", 999);
        });
        invalidDxf.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                invalidDxf,
                new OpenXmlImportOptions()));
    }

    [TestMethod]
    public async Task PreservationRepeatedSavesKeepOpaquePartAndConditionalRules()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = await CreatePackageAsync(serializer);
        AddOpaqueWorkbookPart(source);
        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.Worksheets[0].SetValue(
            new CellAddress(0, 5),
            "first");

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(first);
        AssertSchemaValid(first);
        first.Position = 0L;
        var firstReload = await serializer.LoadAsync(
            first,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            2,
            firstReload.Worksheets[0].ConditionalFormattingRuleCount);

        workbook.Worksheets[0].SetValue(
            new CellAddress(0, 6),
            "second");
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(second);
        AssertSchemaValid(second);
        second.Position = 0L;
        var secondReload = await serializer.LoadAsync(
            second,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            2,
            secondReload.Worksheets[0].ConditionalFormattingRuleCount);
        Assert.AreEqual(
            "second",
            secondReload.Worksheets[0]
                .GetCell(new CellAddress(0, 6))
                .Value.RawValue);
    }

    [TestMethod]
    public async Task PreserveUnknownPartsKeepsUnsupportedConditionalRulesOpaque()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = await CreatePackageAsync(serializer);
        MutateFirstWorksheet(source, root =>
        {
            var rule = root
                .Descendants(SpreadsheetNamespace + "cfRule")
                .First();
            rule.SetAttributeValue("type", "duplicateValues");
            rule.Elements(SpreadsheetNamespace + "formula").Remove();
        });

        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.Worksheets[0].SetValue(new CellAddress(0, 6), "edited");

        await using var saved = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            saved,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });

        saved.Position = 0L;
        using var document = SpreadsheetDocument.Open(saved, false);
        var worksheetPart = document.WorkbookPart?
            .WorksheetParts.Single()
            ?? throw new AssertFailedException("Worksheet part is missing.");
        var worksheetXml = LoadPartXml(worksheetPart);
        Assert.IsTrue(worksheetXml
            .Descendants(SpreadsheetNamespace + "cfRule")
            .Any(static rule =>
                (string?)rule.Attribute("type") == "duplicateValues"));
    }

    private static NeraWorkbook CreateConditionalWorkbook()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Data");
        worksheet.SetValue(new CellAddress(1, 0), 5d);
        worksheet.SetValue(new CellAddress(2, 0), -1d);
        worksheet.SetValue(new CellAddress(1, 1), 2d);
        worksheet.SetValue(new CellAddress(2, 1), 2d);
        var range = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(2, 1));
        var highStyleId = worksheet.DifferentialStyles.Intern(
            CreateHighPriorityPatch());
        var lowStyleId = worksheet.DifferentialStyles.Intern(
            CreateLowPriorityPatch());
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [range],
                ConditionalFormattingRuleType.Expression,
                ConditionalFormattingOperator.Equal,
                "=A2>0",
                formula2: null,
                highStyleId,
                priority: 1,
                stopIfTrue: true));
        worksheet.AddConditionalFormattingRule(
            new ConditionalFormattingRule(
                Guid.NewGuid(),
                [range],
                ConditionalFormattingRuleType.CellIs,
                ConditionalFormattingOperator.GreaterThan,
                "=0",
                formula2: null,
                lowStyleId,
                priority: 2));
        return workbook;
    }

    private static CellStylePatch CreateHighPriorityPatch() => new()
    {
        Fill = new CellFillStyle
        {
            IsVisible = true,
            Color = new ColorRgba(245, 210, 70),
        },
        FontWeight = 700,
    };

    private static CellStylePatch CreateLowPriorityPatch() => new()
    {
        FontColor = new ColorRgba(20, 80, 180),
        FontItalic = true,
    };

    private static CellBorderSide CreateBorderSide() => new()
    {
        Style = CellBorderLineStyle.Medium,
        Color = new ColorRgba(30, 90, 160),
        Width = 2d,
    };

    private static async Task<MemoryStream> CreatePackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            CreateConditionalWorkbook(),
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        return stream;
    }

    private static void MutateFirstWorksheet(
        MemoryStream stream,
        Action<XElement> mutation)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException("Worksheet part is missing.");
            var xml = LoadPartXml(part);
            mutation(xml.Root
                ?? throw new AssertFailedException(
                    "Worksheet XML root is missing."));
            SavePartXml(part, xml);
        }
        stream.Position = 0L;
    }

    private static void MutateStyleTable(
        MemoryStream stream,
        Action<XElement> mutation)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart?.WorkbookStylesPart
                ?? throw new AssertFailedException(
                    "Workbook style part is missing.");
            var xml = LoadPartXml(part);
            mutation(xml.Root
                ?? throw new AssertFailedException(
                    "Workbook style XML root is missing."));
            SavePartXml(part, xml);
        }
        stream.Position = 0L;
    }

    private static void AddOpaqueWorkbookPart(MemoryStream stream)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part is missing.");
            var opaque = workbookPart.AddExtendedPart(
                OpaqueRelationshipType,
                OpaqueContentType,
                ".bin",
                OpaqueRelationshipId);
            using var target = opaque.GetStream(
                FileMode.Create,
                FileAccess.Write);
            target.Write(OpaqueBytes);
        }
        stream.Position = 0L;
    }

    private static void AssertOpaquePart(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var part = workbookPart.GetPartById(OpaqueRelationshipId);
        using var source = part.GetStream(
            FileMode.Open,
            FileAccess.Read);
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

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(
            FileMode.Open,
            FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(
            FileMode.Create,
            FileAccess.Write);
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}
