using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Expands table structured references to A1 ranges before delegating to the
/// existing Nera formula engine. The expansion is deterministic and does not
/// materialize table cells.
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
        WorkbookTableCatalog tables,
        Worksheet currentWorksheet,
        CellAddress formulaAddress) =>
        SpreadsheetStructuredReferenceResolver.ResolveFormula(
            formula,
            tables,
            currentWorksheet,
            formulaAddress);

    public FormulaEvaluationResult Evaluate(
        string formula,
        WorkbookTableCatalog tables,
        Worksheet currentWorksheet,
        CellAddress formulaAddress,
        IFormulaEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var expanded = Expand(
            formula,
            tables,
            currentWorksheet,
            formulaAddress);
        return _inner.Evaluate(expanded, context);
    }
}
