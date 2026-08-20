using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing;

public sealed record DataValidationInputMessage(
    string? Title,
    string? Message);

public sealed record DataValidationDiagnostic(
    CellAddress Address,
    DataValidationEvaluationResult Result);

public sealed class SpreadsheetDataValidationController
{
    private readonly SpreadsheetSession _session;
    private readonly NeraFormulaEngine _formulaEngine = new();

    public SpreadsheetDataValidationController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public DataValidationRule? GetRule(CellAddress address)
    {
        address = _session.ActiveWorksheet.ResolveMergedAnchor(address);
        return _session.ActiveWorksheet.TryGetDataValidationRule(
            address,
            out var rule)
            ? rule
            : null;
    }

    public DataValidationInputMessage? GetInputMessage(
        CellAddress address)
    {
        var rule = GetRule(address);
        if (rule is null || !rule.ShowInputMessage ||
            rule.PromptTitle is null && rule.Prompt is null)
        {
            return null;
        }

        return new DataValidationInputMessage(
            rule.PromptTitle,
            rule.Prompt);
    }

    public DataValidationEvaluationResult ValidateValue(
        CellAddress address,
        object? value) =>
        Validate(
            address,
            CellValue.FromObject(value));

    public DataValidationEvaluationResult Validate(
        CellAddress address,
        CellValue candidate)
    {
        address = _session.ActiveWorksheet.ResolveMergedAnchor(address);
        return DataValidationEvaluator.Evaluate(
            WorksheetSnapshot.Capture(_session.ActiveWorksheet),
            address,
            candidate);
    }

    public DataValidationEvaluationResult ValidateFormula(
        CellAddress address,
        string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        address = _session.ActiveWorksheet.ResolveMergedAnchor(address);
        var normalized = formula.StartsWith('=')
            ? formula
            : $"={formula}";
        var evaluated = _formulaEngine.Evaluate(
            normalized,
            new WorkbookEvaluationContext(
                _session.Workbook,
                _session.ActiveWorksheet));
        var candidate = evaluated.IsSuccess
            ? evaluated.Value
            : CellValue.FromError("#VALUE!");
        return Validate(address, candidate);
    }

    public IReadOnlyList<DataValidationDiagnostic> GetInvalidCells(
        CellRange range,
        int maximumCells = 100_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCells);
        var cellCount = checked((long)range.RowCount * range.ColumnCount);
        if (cellCount > maximumCells)
        {
            throw new InvalidOperationException(
                $"Validation diagnostics are bounded to {maximumCells} cells per request.");
        }

        var snapshot = WorksheetSnapshot.Capture(_session.ActiveWorksheet);
        var diagnostics = new List<DataValidationDiagnostic>();
        for (var row = range.Top; row <= range.Bottom; row++)
        {
            for (var column = range.Left; column <= range.Right; column++)
            {
                var address = new CellAddress(row, column);
                var result = DataValidationEvaluator.Evaluate(
                    snapshot,
                    address,
                    snapshot.GetCell(address).Value);
                if (!result.IsValid)
                {
                    diagnostics.Add(new DataValidationDiagnostic(
                        address,
                        result));
                }
            }
        }

        return diagnostics;
    }

    private sealed class WorkbookEvaluationContext
        : IFormulaEvaluationContext
    {
        private readonly Workbook _workbook;
        private readonly Worksheet _activeWorksheet;

        public WorkbookEvaluationContext(
            Workbook workbook,
            Worksheet activeWorksheet)
        {
            _workbook = workbook;
            _activeWorksheet = activeWorksheet;
        }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address)
        {
            var worksheet = worksheetName is null
                ? _activeWorksheet
                : _workbook.Worksheets.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Name,
                        worksheetName,
                        StringComparison.OrdinalIgnoreCase));
            return worksheet is null
                ? CellValue.FromError("#REF!")
                : worksheet.GetCell(address).Value;
        }
    }
}
