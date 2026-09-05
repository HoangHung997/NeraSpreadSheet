using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;

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
            var outputWorkbookPart = preservedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The preserved package is missing its workbook part.");
            var generatedWorkbookPart = generatedDocument.WorkbookPart
                ?? throw new InvalidDataException(
                    "The generated package is missing its workbook part.");
            PatchTheme(outputWorkbookPart, generatedWorkbookPart);
            var differentialStyleMap =
                OpenXmlDifferentialStyleRemapper.MergeGeneratedStyles(
                    outputWorkbookPart,
                    generatedWorkbookPart);
            var differentialStyles = OpenXmlConditionalFormattingCodec.ReadDifferentialStyles(outputWorkbookPart, true);
            var preservedParts = GetWorksheetParts(
                outputWorkbookPart);
            var generatedParts = GetWorksheetParts(
                generatedWorkbookPart);
            var reservedTableIds = preservedParts.SelectMany(part => part.TableDefinitionParts)
                .Select(part => (uint)LoadPartXml(part).Root!.Attribute("id")!)
                .ToHashSet();
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
                    generatedParts[index],
                    differentialStyleMap,
                    reservedTableIds,
                    differentialStyles);
            }
        }

        return preservedStream.ToArray();
    }

    private static void PatchTheme(
        WorkbookPart outputWorkbookPart,
        WorkbookPart generatedWorkbookPart)
    {
        var generatedTheme = generatedWorkbookPart.ThemePart;
        if (generatedTheme is null)
        {
            return;
        }
        var outputTheme = outputWorkbookPart.ThemePart;
        if (outputTheme is not null && string.Equals(
                GetThemeColorSchemeSignature(outputTheme),
                GetThemeColorSchemeSignature(generatedTheme),
                StringComparison.Ordinal))
        {
            return;
        }
        outputTheme ??= outputWorkbookPart.AddNewPart<ThemePart>();
        CopyPartContent(generatedTheme, outputTheme);
    }

    private static string GetThemeColorSchemeSignature(ThemePart part)
    {
        var document = LoadPartXml(part);
        var root = document.Root;
        XNamespace drawingNamespace =
            "http://schemas.openxmlformats.org/drawingml/2006/main";
        return root?
            .Element(drawingNamespace + "themeElements")?
            .Element(drawingNamespace + "clrScheme")?
            .ToString(SaveOptions.DisableFormatting) ?? string.Empty;
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
        WorksheetPart generatedPart,
        IReadOnlyDictionary<uint, uint> differentialStyleMap,
        HashSet<uint> reservedTableIds,
        IReadOnlyList<CellStylePatch> differentialStyles)
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

        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        var preservedTableParts = preservedPart.TableDefinitionParts
            .ToDictionary(
                part => OpenXmlTableCodec.ParseTableGuid(preservedPart.GetIdOfPart(part), part.Uri.ToString()));
        foreach (var relationshipId in generatedRelationshipIds)
        {
            if (generatedPart.GetPartById(relationshipId)
                is not TableDefinitionPart generatedTablePart)
            {
                throw new InvalidDataException(
                    "A generated table relationship does not target a table-definition part.");
            }

            if (preservedTableParts.TryGetValue(
                    OpenXmlTableCodec.ParseTableGuid(relationshipId, generatedTablePart.Uri.ToString()),
                    out var preservedTablePart))
            {
                var retainedRelationshipId = preservedPart.GetIdOfPart(preservedTablePart);
                retainedIds.Add(retainedRelationshipId);
                generatedContainer!.Elements(SpreadsheetNamespace + "tablePart")
                    .Single(element => (string?)element.Attribute(OfficeRelationshipNamespace + "id") == relationshipId)
                    .SetAttributeValue(OfficeRelationshipNamespace + "id", retainedRelationshipId);
                PatchTableDefinition(
                    preservedTablePart,
                    generatedTablePart,
                    differentialStyleMap,
                    OpenXmlTableCodec.ParseTableGuid(relationshipId, generatedTablePart.Uri.ToString()),
                    differentialStyles);
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
            retainedIds.Add(relationshipId);
            CopyTablePart(
                generatedTablePart,
                created,
                differentialStyleMap,
                reservedTableIds);
        }

        foreach (var tablePart in preservedTableParts.Values)
        {
            if (!retainedIds.Contains(preservedPart.GetIdOfPart(tablePart)))
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
        TableDefinitionPart generatedPart,
        IReadOnlyDictionary<uint, uint> differentialStyleMap,
        Guid tableId,
        IReadOnlyList<CellStylePatch> differentialStyles)
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

        OpenXmlDifferentialStyleRemapper.RewriteFilterReferences(
            generatedRoot,
            differentialStyleMap);

        generatedRoot.SetAttributeValue("id", (string?)preservedRoot.Attribute("id"));
        PreserveAttributes(preservedRoot, generatedRoot,
            "id", "name", "displayName", "ref", "headerRowCount", "totalsRowCount", "totalsRowShown");
        if (preservedRoot.Element(SpreadsheetNamespace + "tableStyleInfo") is { } oldStyle &&
            generatedRoot.Element(SpreadsheetNamespace + "tableStyleInfo") is { } newStyle)
        {
            PreserveAttributes(oldStyle, newStyle, "name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes");
        }
        var oldColumns = preservedRoot.Element(SpreadsheetNamespace + "tableColumns")!.Elements().ToArray();
        var oldById = oldColumns.Select((column, index) => (column, index)).ToDictionary(item => OpenXmlTableCodec.ParseColumnGuid(
            (string?)item.column.Attribute("uniqueName"), tableId, (uint)item.column.Attribute("id")!));
        var reservedColumnIds = oldColumns.Select(column => (uint)column.Attribute("id")!).ToHashSet();
        var retainedOffsets = new Dictionary<int, int>();
        var newColumns = generatedRoot.Element(SpreadsheetNamespace + "tableColumns")!.Elements().ToArray();
        for (var index = 0; index < newColumns.Length; index++)
        {
            var column = newColumns[index];
            var id = OpenXmlTableCodec.ParseColumnGuid((string?)column.Attribute("uniqueName"), tableId, (uint)column.Attribute("id")!);
            if (!oldById.TryGetValue(id, out var old))
            {
                column.SetAttributeValue("id", AllocateId(reservedColumnIds));
                continue;
            }
            var oldColumn = old.column;
            retainedOffsets.Add(old.index, index);
            column.SetAttributeValue("id", (string?)oldColumn.Attribute("id"));
            column.SetAttributeValue("uniqueName", (string?)oldColumn.Attribute("uniqueName"));
            PreserveAttributes(oldColumn, column, "id", "name", "uniqueName", "totalsRowLabel", "totalsRowFunction");
            PreserveExtensions(oldColumn, column);
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
        var preservedFilter = preservedRoot.Element(SpreadsheetNamespace + "autoFilter");
        if (preservedRoot.Element(SpreadsheetNamespace + "sortState") is { } tableSort)
        {
            preservedFilter = preservedFilter is null
                ? new XElement(SpreadsheetNamespace + "autoFilter", new XAttribute("ref", (string)preservedRoot.Attribute("ref")!))
                : new XElement(preservedFilter);
            preservedFilter.Add(new XElement(tableSort));
        }
        var generatedFilter = generatedRoot.Element(SpreadsheetNamespace + "autoFilter");
        if (preservedFilter is not null && generatedFilter is not null)
        {
            // Only opaque criteria are restored; supported criteria that the user cleared stay cleared.
            var remapped = new XElement(preservedFilter);
            foreach (var column in remapped.Elements(SpreadsheetNamespace + "filterColumn").ToArray())
            {
                var offset = (int)column.Attribute("colId")!;
                if (!retainedOffsets.TryGetValue(offset, out var newOffset)) column.Remove();
                else column.SetAttributeValue("colId", newOffset);
            }
            RemoveClearedOwnedCriteria(remapped, generatedFilter, differentialStyles);
            OpenXmlWorksheetAutoFilterCodec.PreserveFilterMarkup(
                remapped,
                generatedFilter);
        }
        SavePartXml(preservedPart, generatedDocument);
    }

    private static void CopyTablePart(
        TableDefinitionPart generatedPart,
        TableDefinitionPart createdPart,
        IReadOnlyDictionary<uint, uint> differentialStyleMap,
        HashSet<uint> reservedTableIds)
    {
        var document = LoadPartXml(generatedPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The generated table part is empty.");
        OpenXmlDifferentialStyleRemapper.RewriteFilterReferences(
            root,
            differentialStyleMap);
        root.SetAttributeValue("id", AllocateId(reservedTableIds));
        SavePartXml(createdPart, document);
    }

    private static uint AllocateId(HashSet<uint> reserved)
    {
        var candidate = checked((uint)reserved.Count + 1U);
        while (!reserved.Add(candidate)) candidate = checked(candidate + 1);
        return candidate;
    }

    private static void PreserveAttributes(XElement source, XElement target, params string[] owned)
    {
        foreach (var attribute in source.Attributes().Where(attribute =>
                     attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None ||
                     !owned.Contains(attribute.Name.LocalName, StringComparer.Ordinal)))
        {
            if (target.Attribute(attribute.Name) is null) target.Add(new XAttribute(attribute));
        }
    }

    private static void PreserveExtensions(XElement source, XElement target)
    {
        foreach (var extension in source.Elements(SpreadsheetNamespace + "extLst"))
        {
            target.Add(new XElement(extension));
        }
    }

    private static void RemoveClearedOwnedCriteria(XElement preserved, XElement replacement, IReadOnlyList<CellStylePatch> differentialStyles)
    {
        foreach (var column in preserved.Elements(SpreadsheetNamespace + "filterColumn").ToArray())
        {
            var target = replacement.Elements(SpreadsheetNamespace + "filterColumn").SingleOrDefault(candidate =>
                (string?)candidate.Attribute("colId") == (string?)column.Attribute("colId"));
            try
            {
                _ = OpenXmlAutoFilterCriteriaCodec.Parse(column, (id, cellColor) => OpenXmlTableCodec.ResolveColor(differentialStyles, id, cellColor));
                if (target is null) column.Remove();
            }
            catch (InvalidDataException)
            {
                if (target is not null && !target.Elements().Any(child => child.Name != SpreadsheetNamespace + "extLst"))
                {
                    target.AddFirst(column.Elements().Where(child => child.Name != SpreadsheetNamespace + "extLst").Select(child => new XElement(child)));
                }
                // Unsupported criteria remain producer-owned in the preservation envelope.
            }
        }
        if (replacement.Element(SpreadsheetNamespace + "sortState") is null)
        {
            try
            {
                var range = OpenXmlTableCodec.ParseRange((string)preserved.Attribute("ref")!);
                _ = OpenXmlAutoFilterCriteriaCodec.ParseSortState(preserved, range,
                    (id, cellColor) => OpenXmlTableCodec.ResolveColor(differentialStyles, id, cellColor));
                preserved.Elements(SpreadsheetNamespace + "sortState").Remove();
            }
            catch (InvalidDataException)
            {
                // Unsupported sort metadata remains producer-owned.
            }
        }
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
