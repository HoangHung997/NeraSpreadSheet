using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal sealed class OpenXmlPackageEnvelope
{
    private readonly byte[] _packageBytes;
    private readonly WorksheetBinding[] _worksheets;

    private OpenXmlPackageEnvelope(
        byte[] packageBytes,
        WorksheetBinding[] worksheets)
    {
        _packageBytes = packageBytes;
        _worksheets = worksheets;
    }

    public IReadOnlyList<WorksheetBinding> Worksheets => _worksheets;

    public static OpenXmlPackageEnvelope Capture(
        byte[] packageBytes,
        Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        ArgumentNullException.ThrowIfNull(workbook);

        using var stream = new MemoryStream(
            packageBytes,
            writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);
        OpenXmlPackageGraphValidator.Validate(document);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The preserved XLSX package does not contain a workbook part.");
        var openXmlWorkbook = workbookPart.Workbook
            ?? throw new InvalidDataException(
                "The preserved XLSX package does not contain workbook markup.");
        var sheets = openXmlWorkbook.GetFirstChild<Sheets>()
            ?? throw new InvalidDataException(
                "The preserved XLSX package does not contain a sheets collection.");
        var sheetElements = sheets.Elements<Sheet>().ToArray();

        if (sheetElements.Length != workbook.Worksheets.Count)
        {
            throw new InvalidDataException(
                "Unknown-part preservation requires every workbook sheet to map to one worksheet part.");
        }

        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        var partUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bindings = new WorksheetBinding[sheetElements.Length];
        for (var index = 0; index < sheetElements.Length; index++)
        {
            var relationshipId = sheetElements[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                !relationshipIds.Add(relationshipId))
            {
                throw new InvalidDataException(
                    "The preserved XLSX package contains a missing or duplicate worksheet relationship identifier.");
            }

            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                throw new InvalidDataException(
                    "Unknown-part preservation supports worksheet parts only; chart and dialog sheet topology is not yet supported.");
            }

            var partUri = OpenXmlPackageGraphValidator.ValidatePartUri(
                worksheetPart.Uri);
            if (!partUris.Add(partUri))
            {
                throw new InvalidDataException(
                    "The preserved XLSX package maps multiple sheets to the same worksheet part URI.");
            }

            bindings[index] = new WorksheetBinding(
                workbook.Worksheets[index],
                relationshipId,
                partUri);
        }

        return new OpenXmlPackageEnvelope(
            packageBytes,
            bindings);
    }

    public byte[] ClonePackageBytes() =>
        (byte[])_packageBytes.Clone();

    public void ValidateWorkbookTopology(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (workbook.Worksheets.Count != _worksheets.Length)
        {
            throw new InvalidOperationException(
                "Cannot preserve unknown XLSX parts after worksheets have been added or removed.");
        }

        for (var index = 0; index < _worksheets.Length; index++)
        {
            if (!ReferenceEquals(
                    _worksheets[index].Worksheet,
                    workbook.Worksheets[index]))
            {
                throw new InvalidOperationException(
                    "Cannot preserve unknown XLSX parts after worksheet topology or order has changed.");
            }
        }
    }

    public void ValidatePackageBinding(
        int index,
        string relationshipId,
        Uri partUri)
    {
        if ((uint)index >= (uint)_worksheets.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var expected = _worksheets[index];
        var actualPartUri = OpenXmlPackageGraphValidator.ValidatePartUri(
            partUri);
        if (!string.Equals(
                expected.RelationshipId,
                relationshipId,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.PartUri,
                actualPartUri,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The preserved XLSX worksheet relationship graph no longer matches its captured envelope.");
        }
    }

    internal sealed record WorksheetBinding(
        NeraWorksheet Worksheet,
        string RelationshipId,
        string PartUri);
}

internal static class OpenXmlPackageEnvelopeStore
{
    private static readonly object Sync = new();
    private static readonly ConditionalWeakTable<Workbook, OpenXmlPackageEnvelope>
        Envelopes = new();

    public static void Attach(
        Workbook workbook,
        OpenXmlPackageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(envelope);
        lock (Sync)
        {
            Envelopes.Remove(workbook);
            Envelopes.Add(workbook, envelope);
        }
    }

    public static bool TryGet(
        Workbook workbook,
        [NotNullWhen(true)] out OpenXmlPackageEnvelope? envelope)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return Envelopes.TryGetValue(workbook, out envelope);
    }

    public static void Detach(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        lock (Sync)
        {
            Envelopes.Remove(workbook);
        }
    }
}
