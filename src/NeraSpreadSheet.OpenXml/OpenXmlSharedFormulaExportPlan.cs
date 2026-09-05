using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.OpenXml;

internal sealed class OpenXmlSharedFormulaExportPlan
{
    private const int MaxSharedFormulaGroups = 100_000;
    private const long MaxSharedFormulaCellsPerGroup = 1_000_000L;
    private readonly IReadOnlyDictionary<
        CellAddress,
        OpenXmlSharedFormulaExportCell> _cells;

    private OpenXmlSharedFormulaExportPlan(
        IReadOnlyDictionary<
            CellAddress,
            OpenXmlSharedFormulaExportCell> cells)
    {
        _cells = cells;
    }

    public static OpenXmlSharedFormulaExportPlan Create(
        IEnumerable<KeyValuePair<CellAddress, CellData>> usedCells,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usedCells);
        var formulas = new Dictionary<CellAddress, string>();
        foreach (var (address, cell) in usedCells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cell.Formula is not { } formula)
            {
                continue;
            }
            formulas[address] = NormalizeFormula(formula);
        }

        if (formulas.Count < 2)
        {
            return new OpenXmlSharedFormulaExportPlan(
                new Dictionary<
                    CellAddress,
                    OpenXmlSharedFormulaExportCell>());
        }

        var orderedAddresses = formulas.Keys
            .OrderBy(static address => address.RowIndex)
            .ThenBy(static address => address.ColumnIndex)
            .ToArray();
        var assigned = new HashSet<CellAddress>();
        var planned = new Dictionary<
            CellAddress,
            OpenXmlSharedFormulaExportCell>();
        uint nextSharedIndex = 0U;

        foreach (var anchorAddress in orderedAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (assigned.Contains(anchorAddress) ||
                nextSharedIndex >= MaxSharedFormulaGroups)
            {
                continue;
            }

            var anchorFormula = formulas[anchorAddress];
            if (!IsEligibleFormula(anchorFormula))
            {
                continue;
            }

            var range = FindBestRectangle(
                anchorAddress,
                anchorFormula,
                formulas,
                assigned,
                cancellationToken);
            if (range is null)
            {
                continue;
            }

            var sharedRange = range.Value;
            for (var row = sharedRange.Top;
                 row <= sharedRange.Bottom;
                 row++)
            {
                for (var column = sharedRange.Left;
                     column <= sharedRange.Right;
                     column++)
                {
                    var address = new CellAddress(row, column);
                    assigned.Add(address);
                    planned.Add(
                        address,
                        new OpenXmlSharedFormulaExportCell(
                            nextSharedIndex,
                            sharedRange,
                            address == anchorAddress));
                }
            }
            nextSharedIndex++;
        }

        return new OpenXmlSharedFormulaExportPlan(planned);
    }

    public bool TryGet(
        CellAddress address,
        out OpenXmlSharedFormulaExportCell sharedCell) =>
        _cells.TryGetValue(address, out sharedCell);

    private static CellRange? FindBestRectangle(
        CellAddress anchorAddress,
        string anchorFormula,
        IReadOnlyDictionary<CellAddress, string> formulas,
        IReadOnlySet<CellAddress> assigned,
        CancellationToken cancellationToken)
    {
        var maximumWidth = CountCompatibleWidth(
            anchorAddress,
            anchorAddress.RowIndex,
            SpreadsheetLimits.MaxColumns - anchorAddress.ColumnIndex,
            anchorFormula,
            formulas,
            assigned);
        if (maximumWidth == 0)
        {
            return null;
        }

        var currentWidth = maximumWidth;
        var bestWidth = 1;
        var bestHeight = 1;
        var bestArea = 1L;
        for (var row = anchorAddress.RowIndex;
             row < SpreadsheetLimits.MaxRows &&
             currentWidth > 0;
             row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowWidth = CountCompatibleWidth(
                anchorAddress,
                row,
                currentWidth,
                anchorFormula,
                formulas,
                assigned);
            currentWidth = Math.Min(currentWidth, rowWidth);
            if (currentWidth == 0)
            {
                break;
            }

            var height = row - anchorAddress.RowIndex + 1;
            var safeWidth = (int)Math.Min(
                currentWidth,
                MaxSharedFormulaCellsPerGroup / height);
            if (safeWidth == 0)
            {
                break;
            }
            currentWidth = safeWidth;
            var area = (long)currentWidth * height;
            if (area > bestArea ||
                area == bestArea && currentWidth > bestWidth)
            {
                bestArea = area;
                bestWidth = currentWidth;
                bestHeight = height;
            }
        }

        if (bestArea < 2L)
        {
            return null;
        }

        return new CellRange(
            anchorAddress,
            new CellAddress(
                anchorAddress.RowIndex + bestHeight - 1,
                anchorAddress.ColumnIndex + bestWidth - 1));
    }

    private static int CountCompatibleWidth(
        CellAddress anchorAddress,
        int rowIndex,
        int maximumWidth,
        string anchorFormula,
        IReadOnlyDictionary<CellAddress, string> formulas,
        IReadOnlySet<CellAddress> assigned)
    {
        var width = 0;
        while (width < maximumWidth)
        {
            var address = new CellAddress(
                rowIndex,
                anchorAddress.ColumnIndex + width);
            if (assigned.Contains(address) ||
                !formulas.TryGetValue(address, out var targetFormula) ||
                !AreTranslationEquivalent(
                    anchorAddress,
                    anchorFormula,
                    address,
                    targetFormula))
            {
                break;
            }
            width++;
        }
        return width;
    }

    private static bool AreTranslationEquivalent(
        CellAddress anchorAddress,
        string anchorFormula,
        CellAddress targetAddress,
        string targetFormula)
    {
        if (!IsEligibleFormula(targetFormula))
        {
            return false;
        }
        if (anchorAddress == targetAddress)
        {
            return string.Equals(
                anchorFormula,
                targetFormula,
                StringComparison.Ordinal);
        }

        var translatedForward = FormulaReferenceTranslator.Translate(
            anchorFormula,
            anchorAddress,
            targetAddress);
        if (!string.Equals(
                translatedForward,
                targetFormula,
                StringComparison.Ordinal))
        {
            return false;
        }

        var translatedBack = FormulaReferenceTranslator.Translate(
            targetFormula,
            targetAddress,
            anchorAddress);
        return string.Equals(
            translatedBack,
            anchorFormula,
            StringComparison.Ordinal);
    }

    private static bool IsEligibleFormula(string formula) =>
        !string.IsNullOrWhiteSpace(formula) &&
        formula.IndexOfAny(['#', '[', ']', '{', '}']) < 0;

    private static string NormalizeFormula(string formula) =>
        formula.StartsWith('=')
            ? formula
            : $"={formula}";
}

internal readonly record struct OpenXmlSharedFormulaExportCell(
    uint SharedIndex,
    CellRange Range,
    bool IsAnchor);
