using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class GroupedConditionalCompatibilityTests
{
    private const int RuleCount = 1024;
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    public async Task OneLargeRuleContainerShouldStaySparseAndBoundDiagnosticDetails()
    {
        using var input = await CreateInput();
        var serializer = new NeraOpenXmlWorkbookSerializer();
        var workbook = await serializer.LoadAsync(input, OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility));
        var report = OpenXmlImportDiagnostics.Get(workbook);
        Assert.AreEqual(1, workbook.Worksheets[0].UsedCellCount);
        Assert.AreEqual(0, workbook.Worksheets[0].ConditionalFormattingRuleCount);
        Assert.IsTrue(report.RequiresMetadataPreservation);
        Assert.AreEqual(512, report.Warnings.Count);
        Assert.AreEqual(RuleCount - 512, report.OmittedWarningCount);
        Assert.AreEqual(1234.5, workbook.Worksheets[0].GetValue(default));
        using var output = new MemoryStream();
        await serializer.SaveAsync(workbook, output, new OpenXmlExportOptions { PreserveUnknownParts = true });
        output.Position = 0;
        using var document = SpreadsheetDocument.Open(output, false);
        using var stream = document.WorkbookPart!.WorksheetParts.Single().GetStream(FileMode.Open, FileAccess.Read);
        var xml = XDocument.Load(stream);
        var container = xml.Root!.Element(S + "conditionalFormatting")!;
        Assert.AreEqual("A1:A1048576", (string?)container.Attribute("sqref"));
        Assert.AreEqual(RuleCount, container.Elements(S + "cfRule").Count());
        Assert.AreEqual(RuleCount.ToString(System.Globalization.CultureInfo.InvariantCulture), (string?)container.Elements().Last().Attribute("priority"));
    }

    [TestMethod]
    public async Task MetadataOnTheLastRuleShouldStillBeValidatedAfterTheContainerWasChecked()
    {
        using var input = await CreateInput(last => last.SetAttributeValue("dxfId", 99));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new NeraOpenXmlWorkbookSerializer().LoadAsync(input,
            OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility)));
    }

    [TestMethod]
    public async Task InvalidSchemaInTheLastRuleShouldNotHideBehindEarlierOpaqueRules()
    {
        using var input = await CreateInput(last => last.SetAttributeValue("type", "invalid-rule-type"));
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new NeraOpenXmlWorkbookSerializer().LoadAsync(input,
            OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility)));
        Assert.IsTrue(OpenXmlImportDiagnostics.TryGetFailure(exception, out var problem));
        Assert.AreEqual(OpenXmlImportProblemKind.InvalidMarkup, problem.Kind);
    }

    private static async Task<MemoryStream> CreateInput(Action<XElement>? changeLast = null)
    {
        var workbook = new Workbook(); workbook.Worksheets[0].SetValue(default, 1234.5);
        var stream = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart!.WorksheetParts.Single();
            XDocument xml;
            using (var input = part.GetStream(FileMode.Open, FileAccess.Read)) xml = XDocument.Load(input);
            var rules = Enumerable.Range(1, RuleCount).Select(priority => new XElement(S + "cfRule",
                new XAttribute("type", "duplicateValues"), new XAttribute("priority", priority))).ToArray();
            changeLast?.Invoke(rules[^1]);
            xml.Root!.Element(S + "sheetData")!.AddAfterSelf(new XElement(S + "conditionalFormatting",
                new XAttribute("sqref", "A1:A1048576"), rules));
            using var output = part.GetStream(FileMode.Create, FileAccess.Write); xml.Save(output);
        }
        stream.Position = 0;
        return stream;
    }
}
