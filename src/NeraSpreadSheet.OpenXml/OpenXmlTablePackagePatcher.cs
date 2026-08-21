using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlTablePackagePatcher
{
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly IReadOnlyDictionary<string, int> WorksheetOrder =
        CreateOrder([
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

    public static byte[] Patch(
        byte[] preservedPackageBytes,
        byte[] generatedPackageBytes,
        int expectedWorksheetCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preservedPackageBytes);
        ArgumentNullException.ThrowIfNull(generatedPackageBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedWorksheetCount);

        using var preservedStream = new MemoryStream();
        preservedStream.Write(preservedPackageBytes);
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
            var preservedParts = GetWorksheetParts(
                preservedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The preserved package is missing its workbook part."));
            var generatedParts = GetWorksheetParts(
                generatedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The generated package is missing its workbook part."));
            if (preservedParts.Length != expectedWorksheetCount ||
                generatedParts.Length != expectedWorksheetCount)
            {
                throw new InvalidOperationException(
                    "Cannot patch table parts when worksheet topology differs.");
            }

            for (var index = 0;
                 index < expectedWorksheetCount;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PatchWorksheet(
                    preservedParts[index],
                    generatedParts[index]);
            }
        }

        return preservedStream.ToArray();
    }

    private static WorksheetPart[] GetWorksheetParts(
        WorkbookPart workbookPart)
    {
        var document = LoadPartXml(workbookPart);
        var sheets = document.Root?
            .Element(SpreadsheetNamespace + "sheets")?
            .Elements(SpreadsheetNamespace + "sheet")
            .ToArray()
            ?? throw new InvalidDataException(
                "The workbook is missing its sheet collection.");
        var result = new WorksheetPart[sheets.Length];
        for (var index = 0; index < sheets.Length; index++)
        {
            var relationshipId = (string?)sheets[index].Attribute(
                OfficeRelationshipNamespace + "id");
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId)
                    is not WorksheetPart part)
            {
                throw new InvalidDataException(
                    "A sheet relationship does not target a worksheet part.");
            }
            result[index] = part;
        }
        return result;
    }

    private static void PatchWorksheet(
        WorksheetPart preservedPart,
        WorksheetPart generatedPart)
    {
        var preservedDocument = LoadPartXml(preservedPart);
        var generatedDocument = LoadPartXml(generatedPart);
        var preservedRoot = preservedDocument.Root
            ?? throw new InvalidDataException(
                "The preserved worksheet is missing its root element.");
        var generatedRoot = generatedDocument.Root
            ?? throw new InvalidDataException(
                "The generated worksheet is missing its root element.");
        var generatedContainer = generatedRoot
            .Elements(SpreadsheetNamespace + "tableParts")
            .SingleOrDefault();
        var generatedRelationshipIds = generatedContainer?
            .Elements(SpreadsheetNamespace + "tablePart")
            .Select(element =>
                (string?)element.Attribute(
                    OfficeRelationshipNamespace + "id")
                ?? throw new InvalidDataException(
                    "A generated tablePart is missing its relationship identifier."))
            .ToArray() ?? [];
        if (generatedRelationshipIds.Distinct(
                StringComparer.Ordinal).Count() !=
            generatedRelationshipIds.Length)
        {
            throw new InvalidDataException(
                "The generated worksheet contains duplicate table relationships.");
        }

        var generatedIds = generatedRelationshipIds.ToHashSet(
            StringComparer.Ordinal);
        var preservedTableParts = preservedPart.TableDefinitionParts
            .ToDictionary(
                part => preservedPart.GetIdOfPart(part),
                StringComparer.Ordinal);
        foreach (var relationshipId in generatedRelationshipIds)
        {
            if (generatedPart.GetPartById(relationshipId)
                is not TableDefinitionPart generatedTablePart)
            {
                throw new InvalidDataException(
                    "A generated table relationship does not target a table-definition part.");
            }

            if (preservedTableParts.TryGetValue(
                    relationshipId,
                    out var preservedTablePart))
            {
                PatchTableDefinition(
                    preservedTablePart,
                    generatedTablePart);
                continue;
            }

            if (TryGetPartById(
                    preservedPart,
                    relationshipId,
                    out _))
            {
                throw new InvalidDataException(
                    "A generated table relationship conflicts with an existing worksheet relationship.");
            }
            var created = preservedPart
                .AddNewPart<TableDefinitionPart>(relationshipId);
            CopyPartContent(generatedTablePart, created);
        }

        foreach (var (relationshipId, tablePart) in
                 preservedTableParts)
        {
            if (!generatedIds.Contains(relationshipId))
            {
                preservedPart.DeletePart(tablePart);
            }
        }

        preservedRoot.Elements(
            SpreadsheetNamespace + "tableParts").Remove();
        if (generatedContainer is not null)
        {
            InsertInSchemaOrder(
                preservedRoot,
                new XElement(generatedContainer),
                WorksheetOrder);
        }
        SavePartXml(preservedPart, preservedDocument);
    }

    private static void PatchTableDefinition(
        TableDefinitionPart preservedPart,
        TableDefinitionPart generatedPart)
    {
        var preservedDocument = LoadPartXml(preservedPart);
        var generatedDocument = LoadPartXml(generatedPart);
        var preservedRoot = preservedDocument.Root
            ?? throw new InvalidDataException(
                "The preserved table part is missing its root element.");
        var generatedRoot = generatedDocument.Root
            ?? throw new InvalidDataException(
                "The generated table part is missing its root element.");
        if (preservedRoot.Name != SpreadsheetNamespace + "table" ||
            generatedRoot.Name != SpreadsheetNamespace + "table")
        {
            throw new InvalidDataException(
                "A table-definition part contains invalid root markup.");
        }

        if (!generatedRoot.Elements(
                SpreadsheetNamespace + "extLst").Any())
        {
            foreach (var extensionList in preservedRoot.Elements(
                         SpreadsheetNamespace + "extLst"))
            {
                generatedRoot.Add(new XElement(extensionList));
            }
        }
        SavePartXml(preservedPart, generatedDocument);
    }

    private static bool TryGetPartById(
        OpenXmlPartContainer container,
        string relationshipId,
        out OpenXmlPart? part)
    {
        try
        {
            part = container.GetPartById(relationshipId);
            return part is not null;
        }
        catch (ArgumentOutOfRangeException)
        {
            part = null;
            return false;
        }
        catch (KeyNotFoundException)
        {
            part = null;
            return false;
        }
    }

    private static Dictionary<string, int> CreateOrder(
        IReadOnlyList<string> names)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
        {
            result.Add(names[index], index);
        }
        return result;
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
                MaxCharactersInDocument = MaxXmlCharacters,
                XmlResolver = null,
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
                $"The OpenXml part '{part.Uri}' contains invalid XML.",
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
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
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
}
