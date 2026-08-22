using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlWorksheetAutoFilterPackagePatcher
{
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

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
                    "Cannot patch worksheet AutoFilter when worksheet topology differs.");
            }

            for (var index = 0; index < expectedWorksheetCount; index++)
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
                workbookPart.GetPartById(relationshipId) is not WorksheetPart part)
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

        OpenXmlWorksheetAutoFilterCodec.PatchPreservedFilter(
            preservedRoot,
            generatedRoot);
        SavePartXml(preservedPart, preservedDocument);
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
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
