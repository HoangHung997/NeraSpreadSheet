using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class OpenXmlPackageGraphValidatorTests
{
    private const string RelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string OfficeDocumentRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string WorksheetRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string HyperlinkRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink";

    [TestMethod]
    public void SafePartUriRelationshipsAndTargetsAreAccepted()
    {
        var partUri = new Uri(
            "/xl/worksheets/sheet1.xml",
            UriKind.Relative);
        var escapedPartUri = new Uri(
            "/xl/custom%20parts/item%201.xml",
            UriKind.Relative);
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);

        var actual = OpenXmlPackageGraphValidator.ValidatePartUri(partUri);
        var escapedActual = OpenXmlPackageGraphValidator.ValidatePartUri(escapedPartUri);
        OpenXmlPackageGraphValidator.ValidateRelationshipId(
            "rId1",
            relationshipIds);
        OpenXmlPackageGraphValidator.ValidateRelationshipId(
            "R4a4880969ca5400d",
            relationshipIds);
        OpenXmlPackageGraphValidator.ValidateRelationshipType(
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
        OpenXmlPackageGraphValidator.ValidateRelationshipType(
            "urn:neraspreadsheet:test:opaque");
        OpenXmlPackageGraphValidator.ValidateRelationshipType(
            "https://example.invalid/relationships/custom%7Etype");
        OpenXmlPackageGraphValidator.ValidateReferenceTarget(
            new Uri(
                "https://example.invalid/opaque",
                UriKind.Absolute));
        OpenXmlPackageGraphValidator.ValidateReferenceTarget(
            new Uri(
                "relative/opaque-target",
                UriKind.Relative));

        Assert.AreEqual(
            "/xl/worksheets/sheet1.xml",
            actual);
        Assert.AreEqual(
            "/xl/custom%20parts/item%201.xml",
            escapedActual);
        Assert.AreEqual(2, relationshipIds.Count);
    }

    [TestMethod]
    public void SafeReferenceTargetFormsUsedByExcelAreAccepted()
    {
        Uri[] safeTargets =
        [
            new(
                "https://example.invalid/files/Book%202026.xlsx?sheet=Sheet%201#A1",
                UriKind.Absolute),
            new(
                "file:///C:/Users/Public/Documents/Book%202026.xlsx",
                UriKind.Absolute),
            new(
                "../media/image%201.png",
                UriKind.Relative),
            new(
                "#'Sheet 1'!A1",
                UriKind.Relative),
        ];

        foreach (var safeTarget in safeTargets)
        {
            OpenXmlPackageGraphValidator.ValidateReferenceTarget(safeTarget);
        }
    }

    [TestMethod]
    public void UnsafeLiteralAndEscapedPartUrisAreRejected()
    {
        string[] unsafeUris =
        [
            "xl/no-leading-slash.xml",
            "/xl/../evil.xml",
            "/xl/%2E%2E/evil.xml",
            "/xl/bad%.xml",
            "/xl/bad%2.xml",
            "/xl/bad%GG.xml",
            "/xl//evil.xml",
            "/xl/%2Fescape.xml",
            "/xl/evil.xml?query=1",
            "/xl/evil.xml#fragment",
            "/xl\\evil.xml",
            "/xl/evil.xml/",
        ];

        foreach (var unsafeUri in unsafeUris)
        {
            AssertInvalidData(() =>
                OpenXmlPackageGraphValidator.ValidatePartUri(
                    new Uri(
                        unsafeUri,
                        UriKind.Relative)));
        }
    }

    [TestMethod]
    public void DuplicateAndNonNcNameRelationshipIdentifiersAreRejected()
    {
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        OpenXmlPackageGraphValidator.ValidateRelationshipId(
            "rOpaque",
            relationshipIds);

        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.ValidateRelationshipId(
                "rOpaque",
                relationshipIds));

        string[] invalidIds =
        [
            string.Empty,
            " ",
            "1bad",
            "bad id",
            "bad:id",
            "bad\nid",
        ];
        foreach (var invalidId in invalidIds)
        {
            AssertInvalidData(() =>
                OpenXmlPackageGraphValidator.ValidateRelationshipId(
                    invalidId,
                    new HashSet<string>(StringComparer.Ordinal)));
        }
    }

    [TestMethod]
    public void RelativeEmptyAndControlRelationshipTypesAreRejected()
    {
        string[] invalidTypes =
        [
            string.Empty,
            " ",
            "relative/relationship/type",
            "urn:nera:bad\ntype",
            "https://example.invalid/relationships/bad%",
            "https://example.invalid/relationships/bad%2",
            "https://example.invalid/relationships/bad%GG",
        ];

        foreach (var invalidType in invalidTypes)
        {
            AssertInvalidData(() =>
                OpenXmlPackageGraphValidator.ValidateRelationshipType(
                    invalidType));
        }
    }

    [TestMethod]
    public void EmptyAndControlReferenceTargetsAreRejected()
    {
        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.ValidateReferenceTarget(
                new Uri(string.Empty, UriKind.Relative)));
        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.ValidateReferenceTarget(
                new Uri("bad%0Atarget", UriKind.Relative)));
    }

    [TestMethod]
    public void MalformedEscapedReferenceTargetsAreRejected()
    {
        string[] invalidTargets =
        [
            "https://example.invalid/bad%",
            "relative/bad%2",
            "file:///C:/Temp/bad%GG.xlsx",
        ];

        foreach (var invalidTarget in invalidTargets)
        {
            AssertInvalidData(() =>
                OpenXmlPackageGraphValidator.ValidateReferenceTarget(
                    new Uri(
                        invalidTarget,
                        UriKind.RelativeOrAbsolute)));
        }
    }

    [TestMethod]
    public void PackageGraphValidationAcceptsMinimalSpreadsheetPackage()
    {
        var packageBytes = CreateMinimalSpreadsheetPackage();

        OpenXmlPackageGraphValidator.Validate(packageBytes);
    }

    [TestMethod]
    public void PackageGraphValidationRejectsMalformedEscapedPartUris()
    {
        var packageBytes = CreateMinimalSpreadsheetPackage(
            worksheetTarget: "worksheets/bad%GG.xml",
            worksheetEntryName: "xl/worksheets/bad%GG.xml");

        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.Validate(packageBytes));
    }

    [TestMethod]
    public void PackageGraphValidationRejectsMalformedEscapedRelationshipTypes()
    {
        var packageBytes = CreateMinimalSpreadsheetPackage(
            worksheetRelationshipType:
                "https://example.invalid/relationships/bad%");

        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.Validate(packageBytes));
    }

    [TestMethod]
    public void PackageGraphValidationRejectsMalformedEscapedExternalTargets()
    {
        var packageBytes = CreateMinimalSpreadsheetPackage(
            externalTarget: "https://example.invalid/bad%");

        AssertInvalidData(() =>
            OpenXmlPackageGraphValidator.Validate(packageBytes));
    }

    private static void AssertInvalidData(Action action)
    {
        try
        {
            action();
            Assert.Fail("Expected InvalidDataException was not thrown.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static byte[] CreateMinimalSpreadsheetPackage(
        string worksheetTarget = "worksheets/sheet1.xml",
        string worksheetEntryName = "xl/worksheets/sheet1.xml",
        string worksheetRelationshipType = WorksheetRelationshipType,
        string? externalTarget = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(
                   stream,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            WriteEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(
                archive,
                "_rels/.rels",
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="{RelationshipNamespace}">
                  <Relationship Id="rIdPackageWorkbook" Type="{OfficeDocumentRelationshipType}" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(
                archive,
                "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Sheet1" sheetId="1" r:id="rIdWorkbookSheet1"/>
                  </sheets>
                </workbook>
                """);
            WriteEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                CreateWorkbookRelationships(
                    worksheetTarget,
                    worksheetRelationshipType,
                    externalTarget));
            WriteEntry(
                archive,
                worksheetEntryName,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData/>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static string CreateWorkbookRelationships(
        string worksheetTarget,
        string worksheetRelationshipType,
        string? externalTarget)
    {
        var externalRelationship = externalTarget is null
            ? string.Empty
            : $"""

                <Relationship Id="rIdExternal"
                              Type="{HyperlinkRelationshipType}"
                              Target="{externalTarget}"
                              TargetMode="External"/>
              """;

        return $"""
               <?xml version="1.0" encoding="utf-8"?>
               <Relationships xmlns="{RelationshipNamespace}">
                 <Relationship Id="rIdWorkbookSheet1"
                               Type="{worksheetRelationshipType}"
                               Target="{worksheetTarget}"/>{externalRelationship}
               </Relationships>
               """;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string entryName,
        string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
