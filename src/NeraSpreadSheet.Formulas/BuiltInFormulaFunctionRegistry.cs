namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Default registry containing NeraSpreadSheet's platform-neutral built-in
/// formula functions.
/// </summary>
public sealed class BuiltInFormulaFunctionRegistry :
    VersionedFormulaFunctionRegistry
{
    public BuiltInFormulaFunctionRegistry()
    {
        foreach (var function in StandardFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in AggregateLogicalFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in MathFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in TextFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in DateTimeFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in ConditionalAggregateFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in StatisticalFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in AdvancedStatisticalFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in ContinuousDistributionFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in FinancialFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in RemainingFinancialFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in EngineeringFormulaFunctions.CreateAll())
        {
            Register(function);
        }
        foreach (var function in DatabaseFormulaFunctions.CreateAll())
        {
            Register(function);
        }
    }
}
