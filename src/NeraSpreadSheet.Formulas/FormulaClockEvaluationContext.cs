namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Optional evaluation context used to make TODAY and NOW deterministic in
/// tests, previews and batch calculations. The engine falls back to the local
/// system clock when this interface is not supplied.
/// </summary>
public interface IFormulaClockEvaluationContext : IFormulaEvaluationContext
{
    DateTime CurrentDateTime { get; }
}
