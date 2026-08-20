using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.OpenXml;

internal sealed class OpenXmlSharedFormulaImportResolver
{
    private const int MaxSharedFormulaGroups = 100_000;
    private readonly IReadOnlyDictionary<uint, SharedFormulaAnchor> _anchors;

    private OpenXmlSharedFormulaImportResolver(
        IReadOnlyDictionary<uint, SharedFormulaAnchor> anchors)
    {
        _anchors = anchors;
    }

    public static OpenXmlSharedFormulaImportResolver Create(
        SheetData sheetData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        var anchors = new Dictionary<uint, SharedFormulaAnchor>();

        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var cell in row.Elements<Cell>())
            {
                var cellFormula = cell.CellFormula;
                if (cellFormula is null || !IsShared(cellFormula))
                {
                    continue;
                }

                var address = ReadSharedCellAddress(cell);
                var sharedIndex = ReadSharedIndex(cellFormula, address);
                var formulaText = cellFormula.Text;
                if (string.IsNullOrWhiteSpace(formulaText))
                {
                    continue;
                }

                var rangeText = GetAttributeValue(cellFormula, "ref");
                if (!TryParseSharedRange(rangeText, out var range))
                {
                    throw new InvalidDataException(
                        $"Shared formula {sharedIndex} at {address} has an invalid or missing reference range.");
                }
                if (!range.Contains(address))
                {
                    throw new InvalidDataException(
                        $"Shared formula anchor {address} is outside its declared range '{rangeText}'.");
                }
                if (anchors.Count >= MaxSharedFormulaGroups)
                {
                    throw new InvalidDataException(
                        $"The worksheet exceeds the supported shared-formula group limit of {MaxSharedFormulaGroups}.");
                }
                if (!anchors.TryAdd(
                        sharedIndex,
                        new SharedFormulaAnchor(
                            address,
                            range,
                            NormalizeFormula(formulaText))))
                {
                    throw new InvalidDataException(
                        $"The worksheet contains duplicate shared-formula anchor index {sharedIndex}.");
                }
            }
        }

        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var cell in row.Elements<Cell>())
            {
                var cellFormula = cell.CellFormula;
                if (cellFormula is null || !IsShared(cellFormula))
                {
                    continue;
                }

                var address = ReadSharedCellAddress(cell);
                var sharedIndex = ReadSharedIndex(cellFormula, address);
                if (!string.IsNullOrWhiteSpace(cellFormula.Text))
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(
                        GetAttributeValue(cellFormula, "ref")))
                {
                    throw new InvalidDataException(
                        $"Shared-formula follower {address} must not declare its own reference range.");
                }
                if (!anchors.TryGetValue(sharedIndex, out var anchor))
                {
                    throw new InvalidDataException(
                        $"Shared-formula follower {address} references missing anchor index {sharedIndex}.");
                }
                if (!anchor.Range.Contains(address))
                {
                    throw new InvalidDataException(
                        $"Shared-formula follower {address} is outside anchor range '{anchor.Range}'.");
                }
            }
        }

        return new OpenXmlSharedFormulaImportResolver(anchors);
    }

    public string? Resolve(Cell cell, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var cellFormula = cell.CellFormula;
        if (cellFormula is null)
        {
            return null;
        }

        if (!IsShared(cellFormula))
        {
            return string.IsNullOrWhiteSpace(cellFormula.Text)
                ? null
                : NormalizeFormula(cellFormula.Text);
        }

        var sharedIndex = ReadSharedIndex(cellFormula, address);
        if (!_anchors.TryGetValue(sharedIndex, out var anchor))
        {
            throw new InvalidDataException(
                $"Shared formula at {address} references missing anchor index {sharedIndex}.");
        }
        if (!anchor.Range.Contains(address))
        {
            throw new InvalidDataException(
                $"Shared formula at {address} is outside anchor range '{anchor.Range}'.");
        }

        if (!string.IsNullOrWhiteSpace(cellFormula.Text))
        {
            if (address != anchor.Address)
            {
                throw new InvalidDataException(
                    $"Shared formula index {sharedIndex} has more than one formula-bearing cell.");
            }
            return anchor.Formula;
        }

        return FormulaReferenceTranslator.Translate(
            anchor.Formula,
            anchor.Address,
            address);
    }

    private static bool IsShared(CellFormula formula) =>
        string.Equals(
            GetAttributeValue(formula, "t"),
            "shared",
            StringComparison.Ordinal);

    private static uint ReadSharedIndex(
        CellFormula formula,
        CellAddress address)
    {
        var value = GetAttributeValue(formula, "si");
        if (!uint.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var sharedIndex))
        {
            throw new InvalidDataException(
                $"Shared formula at {address} has an invalid or missing shared index.");
        }
        return sharedIndex;
    }

    private static CellAddress ReadSharedCellAddress(Cell cell)
    {
        var reference = cell.CellReference?.Value;
        if (string.IsNullOrWhiteSpace(reference) ||
            !CellAddress.TryParseA1(reference, out var address))
        {
            throw new InvalidDataException(
                "Every shared-formula cell must have a valid A1 cell reference.");
        }
        return address;
    }

    private static string? GetAttributeValue(
        CellFormula formula,
        string localName)
    {
        var attribute = formula.GetAttribute(
            localName,
            string.Empty);
        return string.IsNullOrEmpty(attribute.LocalName)
            ? null
            : attribute.Value;
    }

    private static bool TryParseSharedRange(
        string? reference,
        out CellRange range)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            range = default;
            return false;
        }

        var separatorIndex = reference.IndexOf(':');
        if (separatorIndex < 0)
        {
            if (!CellAddress.TryParseA1(reference, out var single))
            {
                range = default;
                return false;
            }
            range = new CellRange(single, single);
            return true;
        }

        if (separatorIndex == 0 ||
            separatorIndex == reference.Length - 1 ||
            separatorIndex != reference.LastIndexOf(':') ||
            !CellAddress.TryParseA1(
                reference[..separatorIndex],
                out var first) ||
            !CellAddress.TryParseA1(
                reference[(separatorIndex + 1)..],
                out var second) ||
            first.RowIndex > second.RowIndex ||
            first.ColumnIndex > second.ColumnIndex)
        {
            range = default;
            return false;
        }

        range = new CellRange(first, second);
        return true;
    }

    private static string NormalizeFormula(string formula) =>
        formula.StartsWith('=')
            ? formula
            : $"={formula}";

    private sealed record SharedFormulaAnchor(
        CellAddress Address,
        CellRange Range,
        string Formula);
}
