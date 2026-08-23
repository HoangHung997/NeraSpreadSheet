namespace NeraSpreadSheet.Formulas;

public sealed class BuiltInFormulaFunctionRegistry : IFormulaFunctionRegistry
{
    private readonly Dictionary<string, IFormulaFunction> _functions =
        new(StringComparer.OrdinalIgnoreCase);

    public BuiltInFormulaFunctionRegistry()
    {
        foreach (var function in StandardFormulaFunctions.CreateAll())
        {
            Register(function);
        }
    }

    public int Count => _functions.Count;

    public void Register(IFormulaFunction formulaFunction)
    {
        ArgumentNullException.ThrowIfNull(formulaFunction);
        if (!_functions.TryAdd(
                formulaFunction.Name,
                formulaFunction))
        {
            throw new InvalidOperationException(
                $"Formula function '{formulaFunction.Name}' is already registered.");
        }
    }

    public bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction) =>
        _functions.TryGetValue(name, out formulaFunction!);
}
