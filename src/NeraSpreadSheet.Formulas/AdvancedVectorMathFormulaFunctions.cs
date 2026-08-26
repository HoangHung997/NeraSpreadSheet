using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AdvancedVectorMathFormulaFunctions
{
    private const double MaximumExactInteger =
        9_007_199_254_740_991d;
    private const int MaximumVectorValues = 1_000_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateLogical(
            "MULTINOMIAL",
            1,
            255,
            Multinomial);
        yield return CreateLogical(
            "SERIESSUM",
            4,
            4,
            SeriesSum);
        yield return CreateLogical(
            "SUMPRODUCT",
            1,
            255,
            SumProduct);
        yield return CreateLogical(
            "SUMX2MY2",
            2,
            2,
            static invocation => SumPairwise(
                invocation,
                PairwiseOperation.XSquaredMinusYSquared));
        yield return CreateLogical(
            "SUMX2PY2",
            2,
            2,
            static invocation => SumPairwise(
                invocation,
                PairwiseOperation.XSquaredPlusYSquared));
        yield return CreateLogical(
            "SUMXMY2",
            2,
            2,
            static invocation => SumPairwise(
                invocation,
                PairwiseOperation.DifferenceSquared));
    }

    private static IFormulaFunction CreateLogical(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, CellValue> evaluator) =>
        new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                FormulaFunctionVolatility.Deterministic,
                FormulaFunctionSecurityClassification.Pure,
                FormulaFunctionDependencyPolicy.EngineCapturedOnly,
                propagateArgumentErrors: true,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            invocation => FormulaEvaluationResult.Success(
                evaluator(invocation)));

    private static CellValue Multinomial(
        FormulaFunctionInvocation invocation)
    {
        var values = invocation.FlattenValues();
        if (values.Length > MaximumVectorValues)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var total = 0;
        var result = 1d;
        foreach (var value in values)
        {
            if (!TryNonNegativeTruncatedInteger(
                    value,
                    out var number,
                    out var error))
            {
                return error;
            }
            if (total + number > 170)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            var combination = Combination(total + number, number);
            if (!double.IsFinite(combination) ||
                result > double.MaxValue / combination)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            result *= combination;
            total += number;
        }

        if (result <= MaximumExactInteger)
        {
            result = Math.Round(result);
        }
        return FormulaValueCoercion.SafeNumber(result);
    }

    private static CellValue SeriesSum(
        FormulaFunctionInvocation invocation)
    {
        if (!TryScalarNumber(
                invocation.Arguments[0],
                out var x) ||
            !TryScalarNumber(
                invocation.Arguments[1],
                out var initialPower) ||
            !TryScalarNumber(
                invocation.Arguments[2],
                out var powerStep))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var coefficients = invocation.Arguments[3].Values;
        if (coefficients.Count > MaximumVectorValues)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var sum = 0d;
        var compensation = 0d;
        for (var index = 0; index < coefficients.Count; index++)
        {
            if (!FormulaValueCoercion.TryNumber(
                    coefficients[index],
                    out var coefficient,
                    allowText: true))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }

            var exponent = initialPower + (index * powerStep);
            var term = coefficient * Math.Pow(x, exponent);
            if (!double.IsFinite(term))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            KahanAdd(ref sum, ref compensation, term);
        }

        return FormulaValueCoercion.SafeNumber(sum);
    }

    private static CellValue SumProduct(
        FormulaFunctionInvocation invocation)
    {
        var arguments = invocation.Arguments;
        var length = arguments[0].Values.Count;
        if (length > MaximumVectorValues)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
        if (arguments.Any(argument =>
                argument.Values.Count != length))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var sum = 0d;
        var compensation = 0d;
        for (var index = 0; index < length; index++)
        {
            var product = 1d;
            foreach (var argument in arguments)
            {
                var factor = GetProductNumber(argument, index);
                if (!double.IsFinite(factor) ||
                    factor != 0d &&
                    Math.Abs(product) >
                    double.MaxValue / Math.Abs(factor))
                {
                    return FormulaValueCoercion.Error("#NUM!");
                }
                product *= factor;
            }

            KahanAdd(ref sum, ref compensation, product);
        }

        return FormulaValueCoercion.SafeNumber(sum);
    }

    private static CellValue SumPairwise(
        FormulaFunctionInvocation invocation,
        PairwiseOperation operation)
    {
        var left = invocation.Arguments[0];
        var right = invocation.Arguments[1];
        if (left.Values.Count != right.Values.Count)
        {
            return FormulaValueCoercion.Error("#N/A");
        }
        if (left.Values.Count > MaximumVectorValues)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var sum = 0d;
        var compensation = 0d;
        for (var index = 0; index < left.Values.Count; index++)
        {
            if (!TryPairwiseNumber(left, index, out var x) ||
                !TryPairwiseNumber(right, index, out var y))
            {
                continue;
            }

            var term = operation switch
            {
                PairwiseOperation.XSquaredMinusYSquared =>
                    (x * x) - (y * y),
                PairwiseOperation.XSquaredPlusYSquared =>
                    (x * x) + (y * y),
                PairwiseOperation.DifferenceSquared =>
                    (x - y) * (x - y),
                _ => throw new InvalidOperationException(
                    "Unknown pairwise operation."),
            };
            if (!double.IsFinite(term))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            KahanAdd(ref sum, ref compensation, term);
        }

        return FormulaValueCoercion.SafeNumber(sum);
    }

    private static bool TryScalarNumber(
        FormulaFunctionArgument argument,
        out double value)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            value = default;
            return false;
        }

        return FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out value,
                allowText: true) &&
            double.IsFinite(value);
    }

    private static bool TryNonNegativeTruncatedInteger(
        CellValue value,
        out int result,
        out CellValue error)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            result = default;
            error = FormulaValueCoercion.Error("#VALUE!");
            return false;
        }

        number = Math.Truncate(number);
        if (!double.IsFinite(number) ||
            number < 0d ||
            number > 170d)
        {
            result = default;
            error = FormulaValueCoercion.Error("#NUM!");
            return false;
        }

        result = (int)number;
        error = default;
        return true;
    }

    private static double Combination(int number, int chosen)
    {
        chosen = Math.Min(chosen, number - chosen);
        var result = 1d;
        for (var index = 1; index <= chosen; index++)
        {
            result *= (double)(number - chosen + index) / index;
        }
        return result;
    }

    private static double GetProductNumber(
        FormulaFunctionArgument argument,
        int index)
    {
        var value = argument.Values[index];
        if (argument.Kind == FormulaFunctionArgumentKind.Scalar)
        {
            return FormulaValueCoercion.TryNumber(
                    value,
                    out var scalar,
                    allowText: true)
                ? scalar
                : 0d;
        }

        return value.Kind == CellValueKind.Number
            ? (double)value.RawValue!
            : 0d;
    }

    private static bool TryPairwiseNumber(
        FormulaFunctionArgument argument,
        int index,
        out double number)
    {
        var value = argument.Values[index];
        if (argument.Kind == FormulaFunctionArgumentKind.Scalar)
        {
            return FormulaValueCoercion.TryNumber(
                value,
                out number,
                allowText: true);
        }

        if (value.Kind == CellValueKind.Number)
        {
            number = (double)value.RawValue!;
            return true;
        }

        number = default;
        return false;
    }

    private static void KahanAdd(
        ref double sum,
        ref double compensation,
        double value)
    {
        var adjusted = value - compensation;
        var next = sum + adjusted;
        compensation = (next - sum) - adjusted;
        sum = next;
    }

    private enum PairwiseOperation
    {
        XSquaredMinusYSquared,
        XSquaredPlusYSquared,
        DifferenceSquared,
    }
}
