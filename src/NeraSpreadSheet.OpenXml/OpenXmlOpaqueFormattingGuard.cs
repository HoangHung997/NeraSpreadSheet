using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Conservative preserve-save restriction, not an in-memory rollback mechanism.
/// Only metadata is fingerprinted: no cell grid expansion or cell values scan.
/// Raw workbook metadata/structure changes, including reverted versioned changes,
/// require reopening the source; ordinary cell value/formula/base-style edits do not.
/// </summary>
internal sealed class OpenXmlOpaqueFormattingGuard
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly long _version;
    private readonly Worksheet[] _sheets;
    private readonly byte[][] _metadata;

    internal OpenXmlOpaqueFormattingGuard(Workbook workbook)
    {
        _version = workbook.Version;
        _sheets = workbook.Worksheets.ToArray();
        _metadata = _sheets.Select(Fingerprint).ToArray();
    }

    internal void Validate(Workbook workbook)
    {
        if (workbook.Version != _version || workbook.Worksheets.Count != _sheets.Length) throw Conflict();
        for (var index = 0; index < _sheets.Length; index++)
            if (!ReferenceEquals(_sheets[index], workbook.Worksheets[index]) ||
                !_metadata[index].AsSpan().SequenceEqual(Fingerprint(_sheets[index]))) throw Conflict();
    }

    private static byte[] Fingerprint(Worksheet sheet) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        sheet.Name,
        DimensionsVersion = sheet.Dimensions.Version,
        Merges = sheet.MergedCells.Ranges,
        Rules = sheet.ConditionalFormattingRules,
        Styles = sheet.DifferentialStyles.Snapshot(),
        Validation = sheet.DataValidationRules,
        sheet.Tables,
        sheet.AutoFilter,
    }));

    internal static void ValidateOutput(byte[] originalBytes, SpreadsheetDocument output,
        IReadOnlyList<OpenXmlPackageEnvelope.WorksheetBinding> bindings)
    {
        using var stream = new MemoryStream(originalBytes, writable: false);
        using var original = SpreadsheetDocument.Open(stream, false);
        var a = original.WorkbookPart ?? throw new InvalidDataException("Original workbook part is missing.");
        var b = output.WorkbookPart ?? throw new InvalidDataException("Output workbook part is missing.");
        var beforeStyles = Read(a.WorkbookStylesPart);
        var afterStyles = Read(b.WorkbookStylesPart);
        if (!XNode.DeepEquals(beforeStyles?.Element(S + "dxfs"), afterStyles?.Element(S + "dxfs")) ||
            !XNode.DeepEquals(beforeStyles?.Element(S + "tableStyles"), afterStyles?.Element(S + "tableStyles"))) throw Conflict();
        foreach (var binding in bindings)
        {
            var before = Read(a.GetPartById(binding.RelationshipId))!;
            var after = Read(b.GetPartById(binding.RelationshipId))!;
            var x = before.Elements().Where(IsProtected).Select(element => new XElement(element)).ToArray();
            var y = after.Elements().Where(IsProtected).Select(element => new XElement(element)).ToArray();
            if (x.Length != y.Length || !x.Zip(y).All(pair => XNode.DeepEquals(pair.First, pair.Second))) throw Conflict();
        }
    }

    private static bool IsProtected(XElement element) => element.Name.Namespace == S && element.Name.LocalName is
        "conditionalFormatting" or "extLst" or "autoFilter" or "sortState" or "dataValidations" or "tableParts";

    private static XElement? Read(OpenXmlPart? part)
    {
        if (part is null) return null;
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256L * 1024L * 1024L,
        });
        return XDocument.Load(reader).Root;
    }

    private static InvalidOperationException Conflict() => new(
        "Cannot preserve unsupported XLSX formatting after metadata/structure changes. " +
        "Keep worksheets, row/column structure/dimensions, merges, CF/dxfs, tables and filters unchanged. " +
        "Reopen the source and apply only cell value/formula/base-style edits. No destination bytes have been written. " +
        "This is a save restriction, not automatic model rollback; Undo of versioned structure is not sufficient.");
}
