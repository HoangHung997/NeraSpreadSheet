using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Expands canonical Nera table structured references to A1 references before
/// delegating to the existing formula engine. Expansion is sparse and does not
/// materialize any table cells.
/// </summary>
public sealed class StructuredReferenceFormulaEngine
{
    private readonly NeraFormulaEngine _inner;

    public StructuredReferenceFormulaEngine(
        IFormulaFunctionRegistry? functions = null)
    {
        _inner = new NeraFormulaEngine(functions);
    }

    public string Expand(
        string formula,
        Workbook workbook,
        Worksheet currentWorksheet,
        CellAddress formulaAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(currentWorksheet);
        if (!workbook.Worksheets.Contains(currentWorksheet))
        {
            throw new ArgumentException(
                "The current worksheet must belong to the workbook.",
                nameof(currentWorksheet));
        }

        return StructuredReferenceFormulaTranslator.Translate(
            formula,
            workbook,
            currentWorksheet,
            formulaAddress);
    }

    public FormulaEvaluationResult Evaluate(
        string formula,
        Workbook workbook,
        Worksheet currentWorksheet,
        CellAddress formulaAddress,
        IFormulaEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var expanded = Expand(
            formula,
            workbook,
            currentWorksheet,
            formulaAddress);
        return _inner.Evaluate(expanded, context);
    }
}
