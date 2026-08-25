namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Optional deterministic locale context used by locale-sensitive functions.
/// Hosts may supply workbook-specific separators without reading process-global
/// culture state during formula evaluation.
/// </summary>
public interface IFormulaLocaleEvaluationContext : IFormulaEvaluationContext
{
    string DecimalSeparator { get; }

    string GroupSeparator { get; }
}
