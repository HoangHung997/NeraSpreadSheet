using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class DataValidationRoundTripTests
{
    private const string OpaqueRelationshipId = "rDataValidationOpaque";
    private const string OpaqueRelationshipType =
        "urn:neraspreadsheet:test:data-validation-opaque";
    private const string OpaqueContentType =
        "application/vnd.neraspreadsheet.test.data-validation-opaque";
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly byte[] OpaqueBytes =
        [0x4E, 0x45, 0x52, 0x41, 0x00, 0x44, 0x56];

    [TestMethod]
    public async Task StandardRuleTypesMetadataAndMultipleRangesRoundTrip()
    {
        var workbook = CreateValidationWorkbook();
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
            var worksheetPart = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException("Worksheet part is missing.");
            var xml = LoadPartXml(worksheetPart);
            var container = xml.Root?
                .Element(SpreadsheetNamespace + "dataValidations")
                ?? throw new AssertFailedException(
                    "dataValidations was not written.");
            Assert.AreEqual("7", (string?)container.Attribute("count"));
            var elements = container
                .Elements(SpreadsheetNamespace + "dataValidation")
                .ToArray();
            Assert.AreEqual(7, elements.Length);
            var list = elements.Single(element =>
                (string?)element.Attribute("type") == "list");
            Assert.AreEqual("0", (string?)list.Attribute("showDropDown"));
            Assert.AreEqual("F1 F3", (string?)list.Attribute("sqref"));
            Assert.AreEqual(
                "\"Small,Medium,Large\"",
                list.Element(SpreadsheetNamespace + "formula1")?.Value);
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var rules = loaded.Worksheets[0].DataValidationRules;
        Assert.AreEqual(7, rules.Count);
        var whole = rules.Single(rule => rule.Type == DataValidationType.Whole);
        Assert.AreEqual(DataValidationOperator.Between, whole.Operator);
        Assert.AreEqual("=1", whole.Formula1);
        Assert.AreEqual("=10", whole.Formula2);
        Assert.IsFalse(whole.AllowBlank);
        Assert.IsTrue(whole.ShowInputMessage);
        Assert.AreEqual("Whole number", whole.PromptTitle);
        Assert.AreEqual("Enter 1 through 10.", whole.Prompt);
        Assert.AreEqual(DataValidationErrorStyle.Stop, whole.ErrorStyle);
        Assert.AreEqual("Invalid", whole.ErrorTitle);
        Assert.AreEqual("Value is outside the allowed range.", whole.Error);

        var listRule = rules.Single(rule => rule.Type == DataValidationType.List);
        Assert.IsTrue(listRule.ShowDropDown);
        CollectionAssert.AreEqual(
            new[]
            {
                new CellRange(
                    new CellAddress(0, 5),
                    new CellAddress(0, 5)),
                new CellRange(
                    new CellAddress(2, 5),
                    new CellAddress(2, 5)),
            },
            listRule.Ranges.ToArray());
        Assert.AreEqual("=\"Small,Medium,Large\"", listRule.Formula1);
        Assert.AreEqual(
            "=A2>0",
            rules.Single(rule => rule.Type == DataValidationType.Custom)
                .Formula1);
    }

    [TestMethod]
    public async Task MalformedCountTypeFormulaAndOverlapAreRejected()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();

        await using var countMismatch = await CreatePackageAsync(serializer);
        MutateFirstWorksheet(countMismatch, root =>
            root.Element(SpreadsheetNamespace + "dataValidations")!
                .SetAttributeValue("count", 99));
        countMismatch.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                countMismatch,
                new OpenXmlImportOptions()));

        await using var unsupportedType = await CreatePackageAsync(serializer);
        MutateFirstWorksheet(unsupportedType, root =>
            root.Descendants(SpreadsheetNamespace + "dataValidation")
                .First()
                .SetAttributeValue("type", "futureType"));
        unsupportedType.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                unsupportedType,
                new OpenXmlImportOptions()));

        await using var missingFormula = await CreatePackageAsync(serializer);
        MutateFirstWorksheet(missingFormula, root =>
            root.Descendants(SpreadsheetNamespace + "dataValidation")
                .First()
                .Element(SpreadsheetNamespace + "formula1")!
                .Remove());
        missingFormula.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                missingFormula,
                new OpenXmlImportOptions()));

        await using var overlapping = await CreatePackageAsync(serializer);
        MutateFirstWorksheet(overlapping, root =>
        {
            var rules = root.Descendants(
                    SpreadsheetNamespace + "dataValidation")
                .ToArray();
            rules[1].SetAttributeValue(
                "sqref",
                (string?)rules[0].Attribute("sqref"));
        });
        overlapping.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                overlapping,
                new OpenXmlImportOptions()));
    }

    [TestMethod]
    public async Task PreservationRepeatedSavesKeepOpaqueBytesAndRefreshRules()
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
        var worksheet = workbook.Worksheets[0];
        var removed = worksheet.DataValidationRules
            .Single(rule => rule.Type == DataValidationType.Custom);
        Assert.IsTrue(worksheet.RemoveDataValidationRule(removed.Id));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(4, 7),
                new CellAddress(4, 7))],
            DataValidationType.List,
            @operator: null,
            "=\"North,South\"",
            showErrorMessage: true,
            errorStyle: DataValidationErrorStyle.Warning));

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
        Assert.AreEqual(7, firstReload.Worksheets[0].DataValidationRuleCount);
        Assert.AreEqual(
            "=\"North,South\"",
            firstReload.Worksheets[0].DataValidationRules
                .Single(rule => rule.Ranges.Any(range =>
                    range.Contains(new CellAddress(4, 7))))
                .Formula1);

        worksheet.SetValue(new CellAddress(0, 9), "second save");
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
        Assert.AreEqual(7, secondReload.Worksheets[0].DataValidationRuleCount);
        Assert.AreEqual(
            "second save",
            secondReload.Worksheets[0]
                .GetValue(new CellAddress(0, 9)));
    }

    private static NeraWorkbook CreateValidationWorkbook()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Data");
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0))],
            DataValidationType.Whole,
            DataValidationOperator.Between,
            "=1",
            "=10",
            allowBlank: false,
            showInputMessage: true,
            promptTitle: "Whole number",
            prompt: "Enter 1 through 10.",
            showErrorMessage: true,
            errorStyle: DataValidationErrorStyle.Stop,
            errorTitle: "Invalid",
            error: "Value is outside the allowed range."));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 1),
                new CellAddress(0, 1))],
            DataValidationType.Decimal,
            DataValidationOperator.GreaterThan,
            "=0"));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 2),
                new CellAddress(0, 2))],
            DataValidationType.Date,
            DataValidationOperator.Between,
            "=45000",
            "=46000"));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 3),
                new CellAddress(0, 3))],
            DataValidationType.Time,
            DataValidationOperator.LessThan,
            "=0.5"));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 4),
                new CellAddress(0, 4))],
            DataValidationType.TextLength,
            DataValidationOperator.LessThanOrEqual,
            "=4"));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [
                new CellRange(
                    new CellAddress(0, 5),
                    new CellAddress(0, 5)),
                new CellRange(
                    new CellAddress(2, 5),
                    new CellAddress(2, 5)),
            ],
            DataValidationType.List,
            @operator: null,
            "=\"Small,Medium,Large\"",
            showDropDown: true));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 6),
                new CellAddress(2, 6))],
            DataValidationType.Custom,
            @operator: null,
            "=A2>0"));
        return workbook;
    }

    private static async Task<MemoryStream> CreatePackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            CreateValidationWorkbook(),
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

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}
