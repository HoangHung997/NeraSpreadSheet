using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class DifferentialStyleImportCompatibilityTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    [DataRow("<numFmt numFmtId='0' formatCode='General'/>", false)]
    [DataRow("<numFmt numFmtId='0' formatCode='General'/>", true)]
    [DataRow("<font><b val='0'/></font>", false)]
    [DataRow("<font><b val='0'/></font>", true)]
    public async Task WorkbookDxfDecoderShouldAcceptExplicitDefaultValuedOverrides(string content, bool preserve)
    {
        using var source = await CreatePackage(content);
        var before = source.ToArray();
        var workbook = await new NeraOpenXmlWorkbookSerializer().LoadAsync(source,
            new OpenXmlImportOptions { PreserveUnknownParts = preserve });
        Assert.AreEqual(42d, workbook.Worksheets[0].GetValue(default));
        CollectionAssert.AreEqual(before, source.ToArray());
        // Decoding the workbook dxf table must not intern unused patches into
        // the managed conditional-rule catalog or weaken that catalog's contract.
        Assert.AreEqual(0, workbook.Worksheets[0].DifferentialStyles.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<protection locked='1'/>")]
    public async Task StrictDecoderShouldContinueRejectingEmptyOrUnsupportedDxf(string content)
    {
        using var source = await CreatePackage(content);
        var before = source.ToArray();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new NeraOpenXmlWorkbookSerializer()
            .LoadAsync(source, new OpenXmlImportOptions()));
        CollectionAssert.AreEqual(before, source.ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<protection locked='1'/>")]
    public async Task PreservationDecoderShouldKeepExistingUnusedUnsupportedDxfFallback(string content)
    {
        using var source = await CreatePackage(content);
        var workbook = await new NeraOpenXmlWorkbookSerializer().LoadAsync(source,
            new OpenXmlImportOptions { PreserveUnknownParts = true });
        Assert.AreEqual(42d, workbook.Worksheets[0].GetValue(default));
        Assert.AreEqual(0, workbook.Worksheets[0].DifferentialStyles.Count);
    }

    private static async Task<MemoryStream> CreatePackage(string dxfContent)
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, 42d);
        var stream = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var styles = document.WorkbookPart!.WorkbookStylesPart!;
            XDocument xml;
            using (var input = styles.GetStream()) xml = XDocument.Load(input);
            var dxfs = xml.Root!.Element(S + "dxfs");
            if (dxfs is null)
            {
                dxfs = new XElement(S + "dxfs");
                var following = xml.Root.Elements().FirstOrDefault(element => element.Name.LocalName is "tableStyles" or "colors" or "extLst");
                if (following is null) xml.Root.Add(dxfs);
                else following.AddBeforeSelf(dxfs);
            }
            dxfs.RemoveNodes();
            dxfs.SetAttributeValue("count", 1);
            dxfs.Add(XElement.Parse($"<dxf xmlns='{S}'>{dxfContent}</dxf>"));
            using var output = styles.GetStream(FileMode.Create, FileAccess.Write);
            xml.Save(output);
        }
        stream.Position = 0;
        return stream;
    }
}
