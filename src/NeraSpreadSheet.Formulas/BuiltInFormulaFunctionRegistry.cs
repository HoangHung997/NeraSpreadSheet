using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed class BuiltInFormulaFunctionRegistry : IFormulaFunctionRegistry
{
    private readonly Dictionary<string, IFormulaFunction> _functions = new(StringComparer.OrdinalIgnoreCase);

    public BuiltInFormulaFunctionRegistry()
    {
        Register(new AggregateFormulaFunction("SUM", AggregateKind.Sum));
        Register(new AggregateFormulaFunction("AVERAGE", AggregateKind.Average));
        Register(new AggregateFormulaFunction("MIN", AggregateKind.Minimum));
        Register(new AggregateFormulaFunction("MAX", AggregateKind.Maximum));
        Register(new CountFormulaFunction());
    }

    public int Count => _functions.Count;

    public void Register(IFormulaFunction formulaFunction)
    {
        ArgumentNullException.ThrowIfNull(formulaFunction);
        if (!_functions.TryAdd(formulaFunction.Name, formulaFunction))
        {
            throw new InvalidOperationException($"Formula function '{formulaFunction.Name}' is already registered.");
        }
    }

    public bool TryResolve(string name, out IFormulaFunction formulaFunction) => _functions.TryGetValue(name, out formulaFunction!);

    private enum AggregateKind { Sum, Average, Minimum, Maximum }

    private sealed class AggregateFormulaFunction : IFormulaFunction
    {
        private readonly AggregateKind _kind;

        public AggregateFormulaFunction(string name, AggregateKind kind)
        {
            Name = name;
            _kind = kind;
        }

        public string Name { get; }

        public FormulaEvaluationResult Invoke(IReadOnlyList<CellValue> arguments, IFormulaEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);
            var numbers = arguments.Where(value => value.Kind == CellValueKind.Number).Select(value => (double)value.RawValue!).ToArray();

            if (_kind == AggregateKind.Sum)
            {
                return FormulaEvaluationResult.Success(CellValue.FromNumber(numbers.Sum()));
            }

            if (numbers.Length == 0)
            {
                return FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);
            }

            var result = _kind switch
            {
                AggregateKind.Average => numbers.Average(),
                AggregateKind.Minimum => numbers.Min(),
                AggregateKind.Maximum => numbers.Max(),
                _ => throw new InvalidOperationException("Unknown aggregate kind."),
            };

            return FormulaEvaluationResult.Success(CellValue.FromNumber(result));
        }
    }

    private sealed class CountFormulaFunction : IFormulaFunction
    {
        public string Name => "COUNT";

        public FormulaEvaluationResult Invoke(IReadOnlyList<CellValue> arguments, IFormulaEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);
            var count = arguments.Count(value => value.Kind == CellValueKind.Number);
            return FormulaEvaluationResult.Success(CellValue.FromNumber(count));
        }
    }
}
