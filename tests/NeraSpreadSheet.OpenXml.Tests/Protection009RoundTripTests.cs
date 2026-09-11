using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class Protection009RoundTripTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    public async Task WorksheetAndWorkbookProtectionRoundTripWithExcelCompatibleMarkup()
    {
        var workbook = new Workbook();
        workbook.SetProtectionSettings(new WorkbookProtectionSettings
        {
            Enabled = true,
            LockStructure = true,
            LockWindows = true,
            PasswordHash = SpreadsheetProtectionPassword.HashLegacy("book"),
        });
        workbook.Worksheets[0].SetProtectionSettings(new WorksheetProtectionSettings
        {
            Enabled = true,
            PasswordHash = SpreadsheetProtectionPassword.HashLegacy("sheet"),
            SelectLockedCells = false,
            SelectUnlockedCells = true,
            AllowFormatCells = true,
            AllowSort = true,
            AllowAutoFilter = true,
        });

        var serializer = new NeraOpenXmlDocumentSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        AssertSchemaValid(stream);

        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var workbookXml = Load(document.WorkbookPart!);
            var workbookProtection = workbookXml.Root?.Element(Ns + "workbookProtection")
                ?? throw new AssertFailedException("workbookProtection is missing.");
            Assert.AreEqual("1", (string?)workbookProtection.Attribute("lockStructure"));
            Assert.AreEqual("1", (string?)workbookProtection.Attribute("lockWindows"));
            Assert.AreEqual(4, ((string?)workbookProtection.Attribute("workbookPassword"))?.Length);

            var worksheetXml = Load(document.WorkbookPart!.WorksheetParts.Single());
            var sheetProtection = worksheetXml.Root?.Element(Ns + "sheetProtection")
                ?? throw new AssertFailedException("sheetProtection is missing.");
            Assert.AreEqual("1", (string?)sheetProtection.Attribute("sheet"));
            Assert.AreEqual("1", (string?)sheetProtection.Attribute("selectLockedCells"));
            Assert.IsNull(sheetProtection.Attribute("selectUnlockedCells"));
            Assert.IsNull(sheetProtection.Attribute("formatCells"));
            Assert.IsNull(sheetProtection.Attribute("sort"));
            Assert.IsNull(sheetProtection.Attribute("autoFilter"));
        }

        stream.Position = 0;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var bookSettings = loaded.GetProtectionSettings();
        Assert.IsTrue(bookSettings.Enabled);
        Assert.IsTrue(bookSettings.LockStructure);
        Assert.IsTrue(bookSettings.LockWindows);
        Assert.IsTrue(bookSettings.VerifyPassword("book"));
        var sheetSettings = loaded.Worksheets[0].GetProtectionSettings();
        Assert.IsTrue(sheetSettings.Enabled);
        Assert.IsFalse(sheetSettings.SelectLockedCells);
        Assert.IsTrue(sheetSettings.SelectUnlockedCells);
        Assert.IsTrue(sheetSettings.AllowFormatCells);
        Assert.IsTrue(sheetSettings.AllowSort);
        Assert.IsTrue(sheetSettings.AllowAutoFilter);
        Assert.IsTrue(sheetSettings.VerifyPassword("sheet"));
    }

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream);
    }

    private static void AssertSchemaValid(MemoryStream stream)
    {
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013).Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(Environment.NewLine, errors.Select(static error => error.Description)));
        stream.Position = 0;
    }
}
