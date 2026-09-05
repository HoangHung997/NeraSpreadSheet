using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlDifferentialStyleRemapper
{
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static IReadOnlyDictionary<uint, uint> MergeGeneratedStyles(
        WorkbookPart outputWorkbookPart,
        WorkbookPart generatedWorkbookPart)
    {
        ArgumentNullException.ThrowIfNull(outputWorkbookPart);
        ArgumentNullException.ThrowIfNull(generatedWorkbookPart);
        var outputPart = outputWorkbookPart.WorkbookStylesPart
            ?? throw new InvalidDataException(
                "The preserved XLSX package does not contain a style table.");
        var generatedPart = generatedWorkbookPart.WorkbookStylesPart
            ?? throw new InvalidDataException(
                "The generated XLSX package does not contain a style table.");
        var output = Load(outputPart);
        var generated = Load(generatedPart);
        var outputRoot = RequireStyleRoot(output);
        var generatedRoot = RequireStyleRoot(generated);
        var outputDxfs = GetDifferentialStyles(outputRoot);
        var generatedDxfs = GetDifferentialStyles(generatedRoot);
        var signatures = outputDxfs
            .Select(CreateSignature)
            .Select((signature, index) => (signature, index))
            .GroupBy(static item => item.signature, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => checked((uint)group.First().index),
                StringComparer.Ordinal);
        var mapping = new Dictionary<uint, uint>();
        var outputContainer = outputRoot.Element(SpreadsheetNamespace + "dxfs");
        foreach (var (generatedDxf, generatedIndex) in generatedDxfs
                     .Select((dxf, index) => (dxf, index)))
        {
            var signature = CreateSignature(generatedDxf);
            if (!signatures.TryGetValue(signature, out var outputIndex))
            {
                outputContainer ??= CreateDifferentialStyleContainer(outputRoot);
                outputIndex = checked((uint)outputDxfs.Count);
                outputContainer.Add(new XElement(generatedDxf));
                outputDxfs.Add(generatedDxf);
                signatures.Add(signature, outputIndex);
            }
            mapping.Add(checked((uint)generatedIndex), outputIndex);
        }
        if (outputContainer is not null)
        {
            outputContainer.SetAttributeValue("count", outputDxfs.Count);
        }
        MergeGeneratedTableStyles(
            outputRoot,
            generatedRoot,
            mapping);
        Save(outputPart, output);
        return mapping;
    }

    private static void MergeGeneratedTableStyles(
        XElement outputRoot,
        XElement generatedRoot,
        Dictionary<uint, uint> mapping)
    {
        var generatedStyles = generatedRoot
            .Element(SpreadsheetNamespace + "tableStyles")?
            .Elements(SpreadsheetNamespace + "tableStyle")
            .Select(static style => new XElement(style))
            .ToArray() ?? [];
        if (generatedStyles.Length == 0)
        {
            return;
        }

        var outputContainer = outputRoot.Element(
            SpreadsheetNamespace + "tableStyles");
        if (outputContainer is null)
        {
            outputContainer = new XElement(
                SpreadsheetNamespace + "tableStyles",
                new XAttribute("count", 0),
                new XAttribute("defaultTableStyle", "TableStyleMedium2"),
                new XAttribute("defaultPivotStyle", "PivotStyleLight16"));
            var following = outputRoot.Elements().FirstOrDefault(element =>
                element.Name == SpreadsheetNamespace + "colors" ||
                element.Name == SpreadsheetNamespace + "extLst");
            if (following is null)
            {
                outputRoot.Add(outputContainer);
            }
            else
            {
                following.AddBeforeSelf(outputContainer);
            }
        }

        foreach (var generatedStyle in generatedStyles)
        {
            foreach (var element in generatedStyle.Elements(
                         SpreadsheetNamespace + "tableStyleElement"))
            {
                var attribute = element.Attribute("dxfId")
                    ?? throw new InvalidDataException(
                        "A generated Table style element is missing dxfId.");
                if (!uint.TryParse(
                        attribute.Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var generatedId) ||
                    !mapping.TryGetValue(generatedId, out var outputId))
                {
                    throw new InvalidDataException(
                        "A generated Table style references an unavailable differential style.");
                }
                attribute.Value = outputId.ToString(CultureInfo.InvariantCulture);
            }

            var name = (string?)generatedStyle.Attribute("name")
                ?? throw new InvalidDataException(
                    "A generated Table style is missing its name.");
            var existing = outputContainer
                .Elements(SpreadsheetNamespace + "tableStyle")
                .FirstOrDefault(style => string.Equals(
                    (string?)style.Attribute("name"),
                    name,
                    StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                outputContainer.Add(generatedStyle);
            }
            else if (!string.Equals(
                         CreateSignature(existing),
                         CreateSignature(generatedStyle),
                         StringComparison.Ordinal))
            {
                existing.ReplaceWith(generatedStyle);
            }
        }
        outputContainer.SetAttributeValue(
            "count",
            outputContainer.Elements(
                SpreadsheetNamespace + "tableStyle").Count());
    }

    public static void RewriteFilterReferences(
        XElement root,
        IReadOnlyDictionary<uint, uint> mapping)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(mapping);
        foreach (var element in root.Descendants().Where(element =>
                     element.Name == SpreadsheetNamespace + "colorFilter" ||
                     element.Name == SpreadsheetNamespace + "sortCondition" &&
                     (string?)element.Attribute("sortBy") is "cellColor" or "fontColor"))
        {
            var attribute = element.Attribute("dxfId")
                ?? throw new InvalidDataException(
                    "A color filter or sort condition is missing dxfId.");
            if (!uint.TryParse(
                    attribute.Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var generatedId) ||
                !mapping.TryGetValue(generatedId, out var outputId))
            {
                throw new InvalidDataException(
                    "A generated color filter references an unavailable differential style.");
            }
            attribute.Value = outputId.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static List<XElement> GetDifferentialStyles(XElement root)
    {
        var containers = root.Elements(SpreadsheetNamespace + "dxfs").ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX style table contains duplicate dxfs collections.");
        }
        return containers.SingleOrDefault()?.Elements(SpreadsheetNamespace + "dxf")
            .ToList() ?? [];
    }

    private static XElement CreateDifferentialStyleContainer(XElement root)
    {
        var container = new XElement(
            SpreadsheetNamespace + "dxfs",
            new XAttribute("count", 0));
        var next = root.Elements().FirstOrDefault(element =>
            element.Name == SpreadsheetNamespace + "tableStyles" ||
            element.Name == SpreadsheetNamespace + "colors" ||
            element.Name == SpreadsheetNamespace + "extLst");
        if (next is null)
        {
            root.Add(container);
        }
        else
        {
            next.AddBeforeSelf(container);
        }
        return container;
    }

    private static string CreateSignature(XElement element) =>
        Normalize(element).ToString(SaveOptions.DisableFormatting);

    private static XElement Normalize(XElement source) =>
        new(
            source.Name,
            source.Attributes()
                .Where(static attribute => !attribute.IsNamespaceDeclaration)
                .OrderBy(static attribute => attribute.Name.ToString(), StringComparer.Ordinal)
                .Select(static attribute => new XAttribute(attribute.Name, attribute.Value)),
            source.Nodes().Select(static node => (XNode?)(node switch
            {
                XElement child => Normalize(child),
                XText text when !string.IsNullOrWhiteSpace(text.Value) =>
                    new XText(text.Value),
                _ => null,
            })));

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MaxXmlCharacters,
                XmlResolver = null,
            });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static XElement RequireStyleRoot(XDocument document)
    {
        var root = document.Root
            ?? throw new InvalidDataException("The XLSX style table is empty.");
        if (root.Name != SpreadsheetNamespace + "styleSheet")
        {
            throw new InvalidDataException("The XLSX style table has an invalid root.");
        }
        return root;
    }

    private static void Save(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
