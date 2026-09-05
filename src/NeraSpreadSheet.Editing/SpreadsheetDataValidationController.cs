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

    public void AddRule(DataValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _session.Execute(new AddDataValidationRuleOperation(
            _session.ActiveWorksheet,
            rule));
    }

    public bool RemoveRule(Guid ruleId)
    {
        var rule = _session.ActiveWorksheet.DataValidationRules
            .FirstOrDefault(candidate => candidate.Id == ruleId);
        if (rule is null)
        {
            return false;
        }

        _session.Execute(new RemoveDataValidationRuleOperation(
            _session.ActiveWorksheet,
            rule));
        return true;
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

    private static CellRange CalculateRange(DataValidationRule rule)
    {
        var top = rule.Ranges.Min(static range => range.Top);
        var left = rule.Ranges.Min(static range => range.Left);
        var bottom = rule.Ranges.Max(static range => range.Bottom);
        var right = rule.Ranges.Max(static range => range.Right);
        return new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right));
    }

    private sealed class AddDataValidationRuleOperation
        : ISpreadsheetEditOperation
    {
        private readonly DataValidationRule _rule;

        public AddDataValidationRuleOperation(
            Worksheet worksheet,
            DataValidationRule rule)
        {
            Worksheet = worksheet;
            _rule = rule.Copy();
            AffectedRange = CalculateRange(rule);
        }

        public string Description => "Add data validation";

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public bool AffectsCalculation => false;

        public void Execute() =>
            Worksheet.AddDataValidationRule(_rule);

        public void Undo()
        {
            if (!Worksheet.RemoveDataValidationRule(_rule.Id))
            {
                throw new InvalidOperationException(
                    "The data-validation rule could not be removed during undo.");
            }
        }
    }

    private sealed class RemoveDataValidationRuleOperation
        : ISpreadsheetEditOperation
    {
        private readonly DataValidationRule _rule;

        public RemoveDataValidationRuleOperation(
            Worksheet worksheet,
            DataValidationRule rule)
        {
            Worksheet = worksheet;
            _rule = rule.Copy();
            AffectedRange = CalculateRange(rule);
        }

        public string Description => "Remove data validation";

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public bool AffectsCalculation => false;

        public void Execute()
        {
            if (!Worksheet.RemoveDataValidationRule(_rule.Id))
            {
                throw new InvalidOperationException(
                    "The data-validation rule does not exist.");
            }
        }

        public void Undo() =>
            Worksheet.AddDataValidationRule(_rule);
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
