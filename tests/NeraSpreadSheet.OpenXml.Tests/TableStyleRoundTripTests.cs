using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class TableStyleRoundTripTests
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    public async Task CustomTableStyleThemeTintAndElementsShouldRoundTripSchemaValid()
    {
        var workbook = CreateWorkbook("NeraCustomStyle");
        workbook.Theme = WorkbookTheme.Office with
        {
            Accent1 = new ColorRgba(28, 96, 164),
        };
        var accentTint = TableStyleColor.FromTheme(
            WorkbookThemeColor.Accent1,
            0.35d);
        var borderColor = TableStyleColor.FromTheme(
            WorkbookThemeColor.Accent1,
            -0.2d);
        workbook.TableStyles.AddOrReplaceCustom(new TableStyleDefinition(
            "custom:nera-style",
            "NeraCustomStyle",
            [
                new TableStyleElement(
                    TableStyleElementType.WholeTable,
                    new TableStyleFormat
                    {
                        FillColor = accentTint,
                        Border = new TableStyleBorder
                        {
                            Bottom = new TableStyleBorderSide
                            {
                                Color = borderColor,
                                Style = CellBorderLineStyle.Medium,
                                Width = 2d,
                            },
                        },
                    }),
                new TableStyleElement(
                    TableStyleElementType.HeaderRow,
                    new TableStyleFormat
                    {
                        FontWeight = 700,
                        FontColor = TableStyleColor.FromTheme(WorkbookThemeColor.Light1),
                    }),
                new TableStyleElement(
                    TableStyleElementType.FirstRowStripe,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromTheme(
                            WorkbookThemeColor.Accent1,
                            0.8d),
                    },
                    stripeSize: 2),
            ]));
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        AssertSchemaValid(stream);
        AssertStyleMarkup(stream);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        Assert.AreEqual(workbook.Theme, loaded.Theme);
        Assert.IsTrue(loaded.TableStyles.TryGet("NeraCustomStyle", out var definition));
        Assert.IsFalse(definition!.IsBuiltIn);
        Assert.AreEqual(3, definition.Elements.Count);
        Assert.AreEqual(
            2,
            definition.Elements.Single(static element =>
                element.Type == TableStyleElementType.FirstRowStripe).StripeSize);
        var resolved = TableStyleResolver.Resolve(definition, loaded.Theme);
        Assert.AreEqual(
            accentTint.Resolve(workbook.Theme),
            resolved.ResolveCell(
                loaded.Worksheets[0].Tables.Single(),
                new CellAddress(3, 1)).Fill.Color);

        await using var repeated = new MemoryStream();
        await serializer.SaveAsync(loaded, repeated, new OpenXmlExportOptions());
        AssertSchemaValid(repeated);
        AssertStyleMarkup(repeated);
    }

    [TestMethod]
    public async Task PreservedUnsupportedCustomStyleShouldRemainUntouchedAcrossRepeatedSave()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        var workbook = CreateWorkbook("VendorStyle");
        await using var source = new MemoryStream();
        await serializer.SaveAsync(workbook, source, new OpenXmlExportOptions());
        AddUnsupportedCustomStyle(source);
        AssertSchemaValid(source);

        source.Position = 0L;
        var loaded = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions { PreserveUnknownParts = true });
        Assert.IsFalse(loaded.TableStyles.TryGet("VendorStyle", out _));

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            loaded,
            first,
            new OpenXmlExportOptions { PreserveUnknownParts = true });
        AssertUnsupportedCustomStyle(first);
        AssertSchemaValid(first);

        first.Position = 0L;
        var reloaded = await serializer.LoadAsync(
            first,
            new OpenXmlImportOptions { PreserveUnknownParts = true });
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            reloaded,
            second,
            new OpenXmlExportOptions { PreserveUnknownParts = true });
        AssertUnsupportedCustomStyle(second);
        AssertSchemaValid(second);
    }

    private static Workbook CreateWorkbook(string styleName)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ],
            styleName: styleName,
            showFirstColumn: true,
            showLastColumn: true,
            showRowStripes: true,
            showColumnStripes: true));
        worksheet.SetValue(default, "Item");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        return workbook;
    }

    private static void AssertStyleMarkup(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0L;
        }
        using var document = SpreadsheetDocument.Open(stream, false);
        var root = LoadStyles(document).Root!;
        var style = root
            .Element(SpreadsheetNamespace + "tableStyles")?
            .Elements(SpreadsheetNamespace + "tableStyle")
            .Single(element => (string?)element.Attribute("name") == "NeraCustomStyle")
            ?? throw new AssertFailedException("Custom Table style is missing.");
        Assert.AreEqual("3", (string?)style.Attribute("count"));
        var stripe = style.Elements(SpreadsheetNamespace + "tableStyleElement")
            .Single(element => (string?)element.Attribute("type") == "firstRowStripe");
        Assert.AreEqual("2", (string?)stripe.Attribute("size"));
        var dxfIdText = (string?)style.Elements(SpreadsheetNamespace + "tableStyleElement")
                .Single(element => (string?)element.Attribute("type") == "wholeTable")
                .Attribute("dxfId")
            ?? throw new AssertFailedException("Custom style dxfId is missing.");
        var dxfId = uint.Parse(
            dxfIdText,
            CultureInfo.InvariantCulture);
        var dxf = root.Element(SpreadsheetNamespace + "dxfs")!
            .Elements(SpreadsheetNamespace + "dxf")
            .ElementAt(checked((int)dxfId));
        var foreground = dxf.Descendants(SpreadsheetNamespace + "fgColor").Single();
        Assert.AreEqual("4", (string?)foreground.Attribute("theme"));
        Assert.AreEqual("0.35", (string?)foreground.Attribute("tint"));
    }

    private static void AddUnsupportedCustomStyle(MemoryStream stream)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var stylesPart = document.WorkbookPart?.WorkbookStylesPart
                ?? throw new AssertFailedException("Style part is missing.");
            var xml = LoadPartXml(stylesPart);
            var root = xml.Root!;
            var dxfs = root.Element(SpreadsheetNamespace + "dxfs");
            if (dxfs is null)
            {
                dxfs = new XElement(SpreadsheetNamespace + "dxfs");
                root.Element(SpreadsheetNamespace + "tableStyles")!.AddBeforeSelf(dxfs);
            }
            var dxfId = dxfs.Elements(SpreadsheetNamespace + "dxf").Count();
            dxfs.Add(new XElement(SpreadsheetNamespace + "dxf"));
            dxfs.SetAttributeValue("count", dxfId + 1);
            var tableStyles = root.Element(SpreadsheetNamespace + "tableStyles")!;
            tableStyles.Add(new XElement(
                SpreadsheetNamespace + "tableStyle",
                new XAttribute("name", "VendorStyle"),
                new XAttribute("pivot", 0),
                new XAttribute("table", 1),
                new XAttribute("count", 1),
                new XElement(
                    SpreadsheetNamespace + "tableStyleElement",
                    new XAttribute("type", "firstHeaderCell"),
                    new XAttribute("dxfId", dxfId))));
            tableStyles.SetAttributeValue("count", 1);
            SavePartXml(stylesPart, xml);
        }
        stream.Position = 0L;
    }

    private static void AssertUnsupportedCustomStyle(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0L;
        }
        using var document = SpreadsheetDocument.Open(stream, false);
        var style = LoadStyles(document).Root!
            .Element(SpreadsheetNamespace + "tableStyles")!
            .Elements(SpreadsheetNamespace + "tableStyle")
            .Single(element => (string?)element.Attribute("name") == "VendorStyle");
        Assert.AreEqual(
            "firstHeaderCell",
            (string?)style.Element(SpreadsheetNamespace + "tableStyleElement")?.Attribute("type"));
    }

    private static XDocument LoadStyles(SpreadsheetDocument document) =>
        LoadPartXml(document.WorkbookPart?.WorkbookStylesPart
            ?? throw new AssertFailedException("Style part is missing."));

    private static void AssertSchemaValid(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0L;
        }
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(error => $"{error.Path?.XPath}: {error.Description}")));
    }

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void SavePartXml(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
