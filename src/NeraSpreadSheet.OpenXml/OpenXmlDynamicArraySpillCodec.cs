using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Removes materialized spill-child values from an XLSX package while keeping
/// owner formulas and direct child styles. Spill children are derived output;
/// serializing them as independent values would block the owner when Nera
/// recalculates the workbook after loading.
/// </summary>
internal static class OpenXmlDynamicArraySpillCodec
{
    public static byte[] Patch(
        byte[] packageBytes,
        Workbook workbook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        ArgumentNullException.ThrowIfNull(workbook);
        using var buffer = new MemoryStream();
        buffer.Write(packageBytes);
        buffer.Position = 0L;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException(
                    "The XLSX package does not contain a workbook part.");
            var sheets = workbookPart.Workbook?
                .GetFirstChild<Sheets>()?
                .Elements<Sheet>()
                .ToArray() ?? [];
            if (sheets.Length != workbook.Worksheets.Count)
            {
                throw new InvalidOperationException(
                    "Dynamic-array spill cleanup requires stable worksheet topology.");
            }

            for (var worksheetIndex = 0;
                 worksheetIndex < sheets.Length;
                 worksheetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var spills = workbook.Worksheets[worksheetIndex]
                    .GetFormulaSpills();
                if (spills.Count == 0)
                {
                    continue;
                }

                var relationshipId = sheets[worksheetIndex].Id?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) ||
                    workbookPart.GetPartById(relationshipId) is not
                        WorksheetPart worksheetPart)
                {
                    throw new InvalidDataException(
                        "The XLSX worksheet relationship is invalid.");
                }
                PatchWorksheet(
                    worksheetPart,
                    spills,
                    cancellationToken);
            }
        }
        return buffer.ToArray();
    }

    private static void PatchWorksheet(
        WorksheetPart worksheetPart,
        IReadOnlyList<FormulaSpillRange> spills,
        CancellationToken cancellationToken)
    {
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException(
                "The XLSX worksheet part has no worksheet markup.");
        var sheetData = worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return;
        }

        var cells = sheetData
            .Descendants<Cell>()
            .Where(static cell =>
                !string.IsNullOrWhiteSpace(cell.CellReference?.Value))
            .ToDictionary(
                static cell => cell.CellReference!.Value!,
                StringComparer.OrdinalIgnoreCase);
        foreach (var spill in spills)
        {
            foreach (var pair in spill.EnumerateValues())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pair.Key == spill.Owner ||
                    !cells.TryGetValue(pair.Key.ToA1(), out var cell))
                {
                    continue;
                }

                cell.CellFormula = null;
                cell.CellValue = null;
                cell.InlineString = null;
                cell.DataType = null;
                cell.CellMetadataIndex = null;
                cell.ValueMetadataIndex = null;
                cell.RemoveAllChildren<ExtensionList>();
                if (cell.StyleIndex is null &&
                    cell.ChildElements.Count == 0 &&
                    HasOnlyReferenceAttribute(cell))
                {
                    cell.Remove();
                }
            }
        }
        worksheet.Save();
    }

    private static bool HasOnlyReferenceAttribute(Cell cell) =>
        cell.GetAttributes().All(attribute =>
            string.Equals(
                attribute.LocalName,
                "r",
                StringComparison.Ordinal));
}
