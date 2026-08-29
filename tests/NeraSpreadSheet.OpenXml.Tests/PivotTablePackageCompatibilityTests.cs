using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class PivotTablePackageCompatibilityTests
{
    private const string WorkbookCacheRelationshipId = "rPivotCacheExternal";
    private const string WorksheetPivotRelationshipId = "rPivotTableExternal";
    private const string PivotCacheRelationshipId = "rPivotCacheDefinitionExternal";
    private const string SourceSheetName = "Data";
    private const string SourceReference = "A1:B4";
    private const string PivotName = "ExternalPivot";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [TestMethod]
    public async Task StandardPivotCacheAndTableGraphSurviveRepeatedSessionSaves()
    {
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var source = await CreateStandardPivotPackageAsync(serializer);
        var baseline = InspectPivotPackage(source);

        source.Position = 0L;
        var session = await serializer.LoadSessionAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });

        var worksheet = session.Workbook.Worksheets.Single();
        worksheet.SetValue(new CellAddress(4, 0), "West");
        worksheet.SetValue(new CellAddress(4, 1), 40d);
        var managedPivot = session.Analytics.InsertPivot(
            new CellRange(new CellAddress(0, 0), new CellAddress(4, 1)),
            rowFieldColumnIndex: 0,
            valueFieldColumnIndex: 1,
            SpreadsheetPivotAggregation.Sum,
            requestedName: "ManagedPivot");
        Assert.AreEqual("ManagedPivot", managedPivot.Name);

        await using var first = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var firstSnapshot = InspectPivotPackage(first);
        AssertPivotSnapshot(baseline, firstSnapshot);

        first.Position = 0L;
        var reloaded = await serializer.LoadSessionAsync(
            first,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        Assert.AreEqual(
            "ManagedPivot",
            reloaded.Analytics.GetPivots(reloaded.ActiveWorksheet).Single().Name);
        reloaded.ActiveWorksheet.SetValue(new CellAddress(5, 0), "North");
        reloaded.ActiveWorksheet.SetValue(new CellAddress(5, 1), 50d);

        await using var second = new MemoryStream();
        await serializer.SaveSessionAsync(
            reloaded,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var secondSnapshot = InspectPivotPackage(second);
        AssertPivotSnapshot(firstSnapshot, secondSnapshot);
    }

    [TestMethod]
    public async Task StandardPivotGraphIsOptInPreservedRatherThanSilentlyClaimedAsManaged()
    {
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var source = await CreateStandardPivotPackageAsync(serializer);

        source.Position = 0L;
        var session = await serializer.LoadSessionAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });

        Assert.AreEqual(
            0,
            session.Analytics.GetPivots(session.ActiveWorksheet).Count);

        await using var output = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            output,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });

        var snapshot = InspectPivotPackage(output);
        Assert.AreEqual(PivotName, snapshot.PivotName);
        Assert.AreEqual(SourceSheetName, snapshot.SourceSheet);
        Assert.AreEqual(SourceReference, snapshot.SourceReference);
    }

    private static async Task<MemoryStream> CreateStandardPivotPackageAsync(
        NeraOpenXmlSpreadsheetSessionSerializer serializer)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, SourceSheetName);
        worksheet.SetValue(new CellAddress(0, 0), "Region");
        worksheet.SetValue(new CellAddress(0, 1), "Sales");
        worksheet.SetValue(new CellAddress(1, 0), "North");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "South");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "North");
        worksheet.SetValue(new CellAddress(3, 1), 30d);

        var stream = new MemoryStream();
        await serializer.SaveSessionAsync(
            new SpreadsheetSession(workbook),
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException(
                    "The pivot fixture is missing its workbook part.");
            var worksheetPart = workbookPart.WorksheetParts.Single();

            var cachePart =
                workbookPart.AddNewPart<PivotTableCacheDefinitionPart>(
                    WorkbookCacheRelationshipId);
            WriteXmlPart(
                cachePart,
                CreatePivotCacheDefinitionXml());

            var pivotPart = worksheetPart.AddNewPart<PivotTablePart>(
                WorksheetPivotRelationshipId);
            pivotPart.AddPart(
                cachePart,
                PivotCacheRelationshipId);
            WriteXmlPart(
                pivotPart,
                CreatePivotTableDefinitionXml());

            var workbookXml = LoadXmlPart(workbookPart);
            workbookXml.Root?.Add(
                new XElement(
                    SpreadsheetNamespace + "pivotCaches",
                    new XElement(
                        SpreadsheetNamespace + "pivotCache",
                        new XAttribute("cacheId", "1"),
                        new XAttribute(
                            OfficeRelationshipNamespace + "id",
                            WorkbookCacheRelationshipId))));
            WriteXmlPart(workbookPart, workbookXml);
        }

        stream.Position = 0L;
        return stream;
    }

    private static PivotPackageSnapshot InspectPivotPackage(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        AssertSchemaValid(document);

        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException(
                "The pivot package is missing its workbook part.");
        var worksheetPart = workbookPart.WorksheetParts.Single();
        var workbookXml = LoadXmlPart(workbookPart);
        var pivotCache = workbookXml.Root?
            .Element(SpreadsheetNamespace + "pivotCaches")?
            .Elements(SpreadsheetNamespace + "pivotCache")
            .SingleOrDefault()
            ?? throw new AssertFailedException(
                "The workbook pivotCaches entry was not preserved.");
        var workbookCacheRelationshipId =
            (string?)pivotCache.Attribute(
                OfficeRelationshipNamespace + "id")
            ?? throw new AssertFailedException(
                "The workbook pivot cache relationship id is missing.");
        var cachePart = workbookPart.GetPartById(
            workbookCacheRelationshipId) as PivotTableCacheDefinitionPart
            ?? throw new AssertFailedException(
                "The workbook pivot cache definition part was not preserved.");

        var pivotPart = worksheetPart.PivotTableParts.SingleOrDefault()
            ?? throw new AssertFailedException(
                "The worksheet pivot table part was not preserved.");
        var worksheetPivotRelationshipId = worksheetPart.GetIdOfPart(pivotPart);
        var linkedCachePart = pivotPart.PivotTableCacheDefinitionPart
            ?? throw new AssertFailedException(
                "The pivot table no longer points at its cache definition.");
        var pivotCacheRelationshipId = pivotPart.GetIdOfPart(linkedCachePart);

        Assert.AreEqual(cachePart.Uri, linkedCachePart.Uri);

        var cacheXml = LoadXmlPart(cachePart);
        var worksheetSource = cacheXml.Root?
            .Element(SpreadsheetNamespace + "cacheSource")?
            .Element(SpreadsheetNamespace + "worksheetSource")
            ?? throw new AssertFailedException(
                "The pivot cache worksheet source was not preserved.");
        var pivotXml = LoadXmlPart(pivotPart);

        return new PivotPackageSnapshot(
            cachePart.Uri.OriginalString,
            pivotPart.Uri.OriginalString,
            workbookCacheRelationshipId,
            worksheetPivotRelationshipId,
            pivotCacheRelationshipId,
            (string?)worksheetSource.Attribute("sheet") ?? string.Empty,
            (string?)worksheetSource.Attribute("ref") ?? string.Empty,
            (string?)pivotXml.Root?.Attribute("name") ?? string.Empty,
            (string?)pivotXml.Root?.Attribute("cacheId") ?? string.Empty);
    }

    private static void AssertPivotSnapshot(
        PivotPackageSnapshot expected,
        PivotPackageSnapshot actual)
    {
        Assert.AreEqual(expected.CachePartUri, actual.CachePartUri);
        Assert.AreEqual(expected.PivotPartUri, actual.PivotPartUri);
        Assert.AreEqual(
            expected.WorkbookCacheRelationshipId,
            actual.WorkbookCacheRelationshipId);
        Assert.AreEqual(
            expected.WorksheetPivotRelationshipId,
            actual.WorksheetPivotRelationshipId);
        Assert.AreEqual(
            expected.PivotCacheRelationshipId,
            actual.PivotCacheRelationshipId);
        Assert.AreEqual(expected.SourceSheet, actual.SourceSheet);
        Assert.AreEqual(expected.SourceReference, actual.SourceReference);
        Assert.AreEqual(expected.PivotName, actual.PivotName);
        Assert.AreEqual(expected.CacheId, actual.CacheId);
    }

    private static XDocument CreatePivotCacheDefinitionXml() =>
        XDocument.Parse(
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <pivotCacheDefinition xmlns="{SpreadsheetNamespace}"
                                  saveData="0"
                                  refreshOnLoad="1"
                                  createdVersion="3"
                                  refreshedVersion="3"
                                  minRefreshableVersion="3"
                                  recordCount="0">
              <cacheSource type="worksheet">
                <worksheetSource ref="{SourceReference}" sheet="{SourceSheetName}" />
              </cacheSource>
              <cacheFields count="2">
                <cacheField name="Region" numFmtId="0">
                  <sharedItems containsString="1" />
                </cacheField>
                <cacheField name="Sales" numFmtId="0">
                  <sharedItems containsNumber="1" />
                </cacheField>
              </cacheFields>
            </pivotCacheDefinition>
            """,
            LoadOptions.PreserveWhitespace);

    private static XDocument CreatePivotTableDefinitionXml() =>
        XDocument.Parse(
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <pivotTableDefinition xmlns="{SpreadsheetNamespace}"
                                  name="{PivotName}"
                                  cacheId="1"
                                  dataCaption="Values"
                                  createdVersion="3"
                                  updatedVersion="3"
                                  minRefreshableVersion="3"
                                  useAutoFormatting="1">
              <location ref="D1:E4" firstHeaderRow="1" firstDataRow="1" firstDataCol="1" />
              <pivotFields count="2">
                <pivotField axis="axisRow" showAll="0">
                  <items count="1">
                    <item t="default" />
                  </items>
                </pivotField>
                <pivotField dataField="1" showAll="0" />
              </pivotFields>
              <rowFields count="1">
                <field x="0" />
              </rowFields>
              <dataFields count="1">
                <dataField name="Sum of Sales" fld="1" subtotal="sum" />
              </dataFields>
              <pivotTableStyleInfo name="PivotStyleLight16"
                                   showRowHeaders="1"
                                   showColHeaders="1"
                                   showRowStripes="0"
                                   showColStripes="0"
                                   showLastColumn="0" />
            </pivotTableDefinition>
            """,
            LoadOptions.PreserveWhitespace);

    private static XDocument LoadXmlPart(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void WriteXmlPart(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private static void AssertSchemaValid(SpreadsheetDocument document)
    {
        var validationErrors = new OpenXmlValidator()
            .Validate(document)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();
        Assert.AreEqual(
            0,
            validationErrors.Length,
            string.Join(Environment.NewLine, validationErrors));
    }

    private sealed record PivotPackageSnapshot(
        string CachePartUri,
        string PivotPartUri,
        string WorkbookCacheRelationshipId,
        string WorksheetPivotRelationshipId,
        string PivotCacheRelationshipId,
        string SourceSheet,
        string SourceReference,
        string PivotName,
        string CacheId);
}
