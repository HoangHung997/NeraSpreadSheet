using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlPackagePreserver
{
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] WorksheetOwnedElements =
    [
        "cols",
        "sheetData",
        "mergeCells",
        "conditionalFormatting",
    ];

    private static readonly IReadOnlyDictionary<string, int> WorksheetOrder =
        CreateOrder(
        [
            "sheetPr",
            "dimension",
            "sheetViews",
            "sheetFormatPr",
            "cols",
            "sheetData",
            "sheetCalcPr",
            "sheetProtection",
            "protectedRanges",
            "scenarios",
            "autoFilter",
            "sortState",
            "dataConsolidate",
            "customSheetViews",
            "mergeCells",
            "phoneticPr",
            "conditionalFormatting",
            "dataValidations",
            "hyperlinks",
            "printOptions",
            "pageMargins",
            "pageSetup",
            "headerFooter",
            "rowBreaks",
            "colBreaks",
            "customProperties",
            "cellWatches",
            "ignoredErrors",
            "smartTags",
            "drawing",
            "legacyDrawing",
            "legacyDrawingHF",
            "picture",
            "oleObjects",
            "controls",
            "webPublishItems",
            "tableParts",
            "extLst",
        ]);

    private static readonly string[] StylesheetOwnedElements =
    [
        "numFmts",
        "fonts",
        "fills",
        "borders",
        "cellStyleXfs",
        "cellXfs",
        "cellStyles",
        "dxfs",
    ];

    private static readonly IReadOnlyDictionary<string, int> StylesheetOrder =
        CreateOrder(
        [
            "numFmts",
            "fonts",
            "fills",
            "borders",
            "cellStyleXfs",
            "cellXfs",
            "cellStyles",
            "dxfs",
            "tableStyles",
            "colors",
            "extLst",
        ]);

    public static byte[] Merge(
        Workbook workbook,
        OpenXmlPackageEnvelope envelope,
        byte[] generatedPackageBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(generatedPackageBytes);
        envelope.ValidateWorkbookTopology(workbook);
        cancellationToken.ThrowIfCancellationRequested();

        using var preservedStream = new MemoryStream();
        var capturedBytes = envelope.ClonePackageBytes();
        preservedStream.Write(
            capturedBytes,
            0,
            capturedBytes.Length);
        preservedStream.Position = 0L;

        using var generatedStream = new MemoryStream(
            generatedPackageBytes,
            writable: false);
        using (var preservedDocument = SpreadsheetDocument.Open(
                   preservedStream,
                   true))
        using (var generatedDocument = SpreadsheetDocument.Open(
                   generatedStream,
                   false))
        {
            var preservedWorkbookPart = preservedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The preserved XLSX package does not contain a workbook part.");
            var generatedWorkbookPart = generatedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The generated XLSX package does not contain a workbook part.");

            var preservedWorkbookXml = LoadPartXml(preservedWorkbookPart);
            var generatedWorkbookXml = LoadPartXml(generatedWorkbookPart);
            var preservedSheets = GetSheetElements(preservedWorkbookXml);
            var generatedSheets = GetSheetElements(generatedWorkbookXml);
            if (preservedSheets.Length != workbook.Worksheets.Count ||
                generatedSheets.Length != workbook.Worksheets.Count)
            {
                throw new InvalidOperationException(
                    "Cannot preserve unknown XLSX parts when worksheet topology differs from the captured package.");
            }

            var worksheetPairs = new List<(WorksheetPart Preserved, WorksheetPart Generated)>(
                workbook.Worksheets.Count);
            for (var index = 0; index < workbook.Worksheets.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var preservedRelationshipId =
                    GetWorksheetRelationshipId(preservedSheets[index]);
                var generatedRelationshipId =
                    GetWorksheetRelationshipId(generatedSheets[index]);
                if (preservedWorkbookPart.GetPartById(
                        preservedRelationshipId) is not WorksheetPart preservedWorksheetPart ||
                    generatedWorkbookPart.GetPartById(
                        generatedRelationshipId) is not WorksheetPart generatedWorksheetPart)
                {
                    throw new InvalidDataException(
                        "The XLSX package contains a sheet relationship that is not a worksheet part.");
                }

                envelope.ValidatePackageBinding(
                    index,
                    preservedRelationshipId,
                    preservedWorksheetPart.Uri);
                worksheetPairs.Add((preservedWorksheetPart, generatedWorksheetPart));
                preservedSheets[index].SetAttributeValue(
                    "name",
                    workbook.Worksheets[index].Name);
            }

            var preserveConditionalFormatting = worksheetPairs.Any(
                static pair => ContainsUnsupportedConditionalFormatting(
                    pair.Preserved));
            var preserveTableDifferentialStyles = ContainsTableDifferentialStyleReferences(preservedWorkbookPart);
            var preserveDifferentialStyles = preserveConditionalFormatting || preserveTableDifferentialStyles;
            PatchStyles(preservedWorkbookPart, generatedWorkbookPart, preserveDifferentialStyles);
            var differentialStyleMap = preserveTableDifferentialStyles
                ? OpenXmlDifferentialStyleRemapper.MergeGeneratedStyles(preservedWorkbookPart, generatedWorkbookPart)
                : null;
            foreach (var pair in worksheetPairs)
            {
                PatchWorksheetPart(
                    pair.Preserved,
                    pair.Generated,
                    preserveConditionalFormatting,
                    differentialStyleMap);
            }

            SavePartXml(
                preservedWorkbookPart,
                preservedWorkbookXml);
            NeraOpenXmlStyleStateCodec.Write(
                preservedWorkbookPart,
                workbook);
        }

        return preservedStream.ToArray();
    }

    private static void PatchWorksheetPart(
        WorksheetPart preservedPart,
        WorksheetPart generatedPart,
        bool preserveConditionalFormatting,
        IReadOnlyDictionary<uint, uint>? differentialStyleMap)
    {
        var preservedXml = LoadPartXml(preservedPart);
        var generatedXml = LoadPartXml(generatedPart);
        var preservedRoot = preservedXml.Root
            ?? throw new InvalidDataException(
                "The preserved worksheet part is missing its root element.");
        var generatedRoot = generatedXml.Root
            ?? throw new InvalidDataException(
                "The generated worksheet part is missing its root element.");
        if (preservedRoot.Name != SpreadsheetNamespace + "worksheet" ||
            generatedRoot.Name != SpreadsheetNamespace + "worksheet")
        {
            throw new InvalidDataException(
                "The XLSX package contains invalid worksheet markup.");
        }
        if (!preserveConditionalFormatting && differentialStyleMap is not null)
        {
            foreach (var rule in generatedRoot.Descendants(SpreadsheetNamespace + "cfRule"))
            {
                var attribute = rule.Attribute("dxfId");
                if (attribute is null) continue;
                if (!uint.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var generatedId) ||
                    !differentialStyleMap.TryGetValue(generatedId, out var outputId))
                    throw new InvalidDataException("A generated conditional rule references an unavailable differential style.");
                attribute.Value = outputId.ToString(CultureInfo.InvariantCulture);
            }
        }

        ReplaceOwnedElements(
            preservedRoot,
            generatedRoot,
            preserveConditionalFormatting
                ? WorksheetOwnedElements.Where(
                    static name => name != "conditionalFormatting")
                : WorksheetOwnedElements,
            WorksheetOrder);
        SavePartXml(
            preservedPart,
            preservedXml);
    }

    private static void PatchStyles(
        WorkbookPart preservedWorkbookPart,
        WorkbookPart generatedWorkbookPart,
        bool preserveDifferentialStyles)
    {
        var generatedStylesPart = generatedWorkbookPart.WorkbookStylesPart
            ?? throw new InvalidDataException(
                "The generated XLSX package does not contain a style table.");
        var preservedStylesPart = preservedWorkbookPart.WorkbookStylesPart;
        if (preservedStylesPart is null)
        {
            preservedStylesPart =
                preservedWorkbookPart.AddNewPart<WorkbookStylesPart>();
            CopyPartContent(
                generatedStylesPart,
                preservedStylesPart);
            return;
        }

        var preservedXml = LoadPartXml(preservedStylesPart);
        var generatedXml = LoadPartXml(generatedStylesPart);
        var preservedRoot = preservedXml.Root
            ?? throw new InvalidDataException(
                "The preserved style part is missing its root element.");
        var generatedRoot = generatedXml.Root
            ?? throw new InvalidDataException(
                "The generated style part is missing its root element.");
        if (preservedRoot.Name != SpreadsheetNamespace + "styleSheet" ||
            generatedRoot.Name != SpreadsheetNamespace + "styleSheet")
        {
            throw new InvalidDataException(
                "The XLSX package contains invalid style-table markup.");
        }

        ReplaceOwnedElements(
            preservedRoot,
            generatedRoot,
            preserveDifferentialStyles
                ? StylesheetOwnedElements.Where(static name => name != "dxfs")
                : StylesheetOwnedElements,
            StylesheetOrder);
        SavePartXml(
            preservedStylesPart,
            preservedXml);
    }

    private static bool ContainsTableDifferentialStyleReferences(WorkbookPart workbookPart)
    {
        var styleRoot = workbookPart.WorkbookStylesPart is { } styles ? LoadPartXml(styles).Root : null;
        var count = styleRoot?.Element(SpreadsheetNamespace + "dxfs")?.Elements(SpreadsheetNamespace + "dxf").Count() ?? 0;
        var found = false;
        foreach (var tablePart in workbookPart.WorksheetParts.SelectMany(sheet => sheet.TableDefinitionParts))
        {
            var root = LoadPartXml(tablePart).Root ?? throw new InvalidDataException("A preserved Table definition is empty.");
            found |= OpenXmlTableCodec.ValidateDifferentialStyleReferences(root, count);
        }
        return found;
    }

    private static bool ContainsUnsupportedConditionalFormatting(
        WorksheetPart worksheetPart)
    {
        var root = LoadPartXml(worksheetPart).Root;
        return root is not null && root
            .Descendants(SpreadsheetNamespace + "cfRule")
            .Any(static rule =>
                (string?)rule.Attribute("type") is not (
                    "cellIs" or "expression"));
    }

    private static XElement[] GetSheetElements(XDocument workbookXml)
    {
        var root = workbookXml.Root
            ?? throw new InvalidDataException(
                "The workbook part is missing its root element.");
        if (root.Name != SpreadsheetNamespace + "workbook")
        {
            throw new InvalidDataException(
                "The XLSX workbook part contains invalid markup.");
        }

        var sheets = root.Element(
            SpreadsheetNamespace + "sheets")
            ?? throw new InvalidDataException(
                "The XLSX workbook does not contain a sheets collection.");
        return sheets
            .Elements(SpreadsheetNamespace + "sheet")
            .ToArray();
    }

    private static string GetWorksheetRelationshipId(XElement sheet)
    {
        var relationshipId =
            (string?)sheet.Attribute(
                OfficeRelationshipNamespace + "id");
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            throw new InvalidDataException(
                "The XLSX workbook contains a sheet without a relationship identifier.");
        }

        return relationshipId;
    }

    private static void ReplaceOwnedElements(
        XElement preservedRoot,
        XElement generatedRoot,
        IEnumerable<string> ownedElementNames,
        IReadOnlyDictionary<string, int> schemaOrder)
    {
        foreach (var localName in ownedElementNames)
        {
            var name = SpreadsheetNamespace + localName;
            var preservedElements = preservedRoot
                .Elements(name)
                .ToArray();
            var generatedElements = generatedRoot
                .Elements(name)
                .Select(static element => new XElement(element))
                .ToArray();

            if (preservedElements.Length > 0)
            {
                var insertionAnchor = preservedElements[0];
                foreach (var generatedElement in generatedElements)
                {
                    insertionAnchor.AddBeforeSelf(generatedElement);
                }

                foreach (var preservedElement in preservedElements)
                {
                    preservedElement.Remove();
                }
                continue;
            }

            foreach (var generatedElement in generatedElements)
            {
                InsertInSchemaOrder(
                    preservedRoot,
                    generatedElement,
                    schemaOrder);
            }
        }
    }

    private static void InsertInSchemaOrder(
        XElement root,
        XElement element,
        IReadOnlyDictionary<string, int> schemaOrder)
    {
        var targetRank = schemaOrder[element.Name.LocalName];
        var following = root.Elements().FirstOrDefault(candidate =>
            candidate.Name.Namespace == SpreadsheetNamespace &&
            schemaOrder.TryGetValue(
                candidate.Name.LocalName,
                out var candidateRank) &&
            candidateRank > targetRank);
        if (following is null)
        {
            root.Add(element);
        }
        else
        {
            following.AddBeforeSelf(element);
        }
    }

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(
            FileMode.Open,
            FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
                MaxCharactersInDocument = MaxXmlCharacters,
            });
        try
        {
            return XDocument.Load(
                reader,
                LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                $"The OpenXml part '{part.Uri}' does not contain valid XML.",
                exception);
        }
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(
            FileMode.Create,
            FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private static void CopyPartContent(
        OpenXmlPart source,
        OpenXmlPart destination)
    {
        using var sourceStream = source.GetStream(
            FileMode.Open,
            FileAccess.Read);
        using var destinationStream = destination.GetStream(
            FileMode.Create,
            FileAccess.Write);
        sourceStream.CopyTo(destinationStream);
    }

    private static Dictionary<string, int> CreateOrder(
        IReadOnlyList<string> elementNames)
    {
        var result = new Dictionary<string, int>(
            elementNames.Count,
            StringComparer.Ordinal);
        for (var index = 0;
             index < elementNames.Count;
             index++)
        {
            result.Add(
                elementNames[index],
                index);
        }
        return result;
    }
}
