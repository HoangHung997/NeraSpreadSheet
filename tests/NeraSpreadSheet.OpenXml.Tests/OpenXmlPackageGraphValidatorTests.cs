using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class OpenXmlPackageGraphValidatorTests
{
    [TestMethod]
    public void SafePartUriAndRelationshipIdentifiersAreAccepted()
    {
        var partUri = new Uri(
            "/xl/worksheets/sheet1.xml",
            UriKind.Relative);
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);

        var actual = OpenXmlPackageGraphValidator.ValidatePartUri(partUri);
        OpenXmlPackageGraphValidator.ValidateRelationshipId(
            "rId1",
            relationshipIds);
        OpenXmlPackageGraphValidator.ValidateRelationshipId(
            "R4a4880969ca5400d",
            relationshipIds);
        OpenXmlPackageGraphValidator.ValidateExternalTarget(
            new Uri(
                "https://example.invalid/opaque",
                UriKind.Absolute));
        OpenXmlPackageGraphValidator.ValidateExternalTarget(
            new Uri(
                "relative/opaque-target",
                UriKind.Relative));

        Assert.AreEqual(
            "/xl/worksheets/sheet1.xml",
            actual);
        Assert.AreEqual(2, relationshipIds.Count);
    }

    [TestMethod]
    public void UnsafeLiteralAndEscapedPartUrisAreRejected()
    {
        string[] unsafeUris =
        [
            "xl/no-leading-slash.xml",
            "/xl/../evil.xml",
            "/xl/%2E%2E/evil.xml",
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
}
