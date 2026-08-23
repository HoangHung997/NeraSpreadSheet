using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class StandardFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> CreateAll()
    {
        foreach (var function in AggregateLogicalFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in MathFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in TextFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in DateTimeFormulaFunctions.Create())
        {
            yield return function;
        }
    }
}

internal static class FormulaFunctionFactory
{
    public static IFormulaFunction Create(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<IReadOnlyList<CellValue>, IFormulaEvaluationContext, CellValue>
            evaluator,
        bool propagateErrors = true) =>
        new DelegateFormulaFunction(
            name,
            minimumArguments,
            maximumArguments,
            evaluator,
            propagateErrors);

    private sealed class DelegateFormulaFunction : IFormulaFunction
    {
        private readonly int _minimumArguments;
        private readonly int _maximumArguments;
        private readonly Func<
            IReadOnlyList<CellValue>,
            IFormulaEvaluationContext,
            CellValue> _evaluator;
        private readonly bool _propagateErrors;

        public DelegateFormulaFunction(
            string name,
            int minimumArguments,
            int maximumArguments,
            Func<
                IReadOnlyList<CellValue>,
                IFormulaEvaluationContext,
                CellValue> evaluator,
            bool propagateErrors)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegative(minimumArguments);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                maximumArguments,
                minimumArguments);
            Name = name.Trim();
            _minimumArguments = minimumArguments;
            _maximumArguments = maximumArguments;
            _evaluator = evaluator ??
                throw new ArgumentNullException(nameof(evaluator));
            _propagateErrors = propagateErrors;
        }

        public string Name { get; }

        public FormulaEvaluationResult Invoke(
            IReadOnlyList<CellValue> arguments,
            IFormulaEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);
            if (arguments.Count < _minimumArguments ||
                arguments.Count > _maximumArguments)
            {
                return FormulaEvaluationResult.Success(
                    CellValue.FromError("#VALUE!"));
            }
            if (_propagateErrors &&
                FormulaValueCoercion.TryGetFirstError(
                    arguments,
                    out var error))
            {
                return FormulaEvaluationResult.Success(error);
            }

            return FormulaEvaluationResult.Success(
                _evaluator(arguments, context));
        }
    }
}
