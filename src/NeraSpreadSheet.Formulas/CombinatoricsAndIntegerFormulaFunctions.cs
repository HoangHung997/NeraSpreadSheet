using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class CombinatoricsAndIntegerFormulaFunctions
{
    private const double MaximumExactIntegerExclusive =
        9_007_199_254_740_992d;
    private const ulong MaximumExactIntegerExclusiveUnsigned = 1UL << 53;
    private const int MaximumCombinationIterations = 1_000_000;
    private const int MaximumFactorialInput = 170;
    private const int MaximumDoubleFactorialInput = 300;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "COMBIN",
            2,
            2,
            static (arguments, _) => Combination(
                arguments,
                withRepetition: false));
        yield return FormulaFunctionFactory.Create(
            "COMBINA",
            2,
            2,
            static (arguments, _) => Combination(
                arguments,
                withRepetition: true));
        yield return FormulaFunctionFactory.Create(
            "FACT",
            1,
            1,
            static (arguments, _) => Factorial(
                arguments[0],
                doubleFactorial: false));
        yield return FormulaFunctionFactory.Create(
            "FACTDOUBLE",
            1,
            1,
            static (arguments, _) => Factorial(
                arguments[0],
                doubleFactorial: true));
        yield return FormulaFunctionFactory.Create(
            "GCD",
            1,
            255,
            static (arguments, _) => GreatestCommonDivisor(arguments));
        yield return FormulaFunctionFactory.Create(
            "LCM",
            1,
            255,
            static (arguments, _) => LeastCommonMultiple(arguments));
    }

    private static CellValue Combination(
        IReadOnlyList<CellValue> arguments,
        bool withRepetition)
    {
        if (!TryNonNegativeTruncatedNumber(
                arguments[0],
                out var number,
                out var error))
        {
            return error;
        }
        if (!TryNonNegativeTruncatedNumber(
                arguments[1],
                out var chosen,
                out error))
        {
            return error;
        }
        if (number < chosen)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
        if (chosen == 0d)
        {
            return CellValue.FromNumber(1d);
        }

        var effectiveNumber = number;
        if (withRepetition)
        {
            effectiveNumber = number + chosen - 1d;
            if (!double.IsFinite(effectiveNumber))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
        }

        return CalculateCombination(effectiveNumber, chosen);
    }

    private static CellValue CalculateCombination(
        double number,
        double chosen)
    {
        var selection = Math.Min(chosen, number - chosen);
        if (selection <= 0d)
        {
            return CellValue.FromNumber(1d);
        }
        if (selection > MaximumCombinationIterations)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var iterations = (int)selection;
        var result = 1d;
        for (var index = 1; index <= iterations; index++)
        {
            var numerator = number - selection + index;
            var factor = numerator / index;
            if (!double.IsFinite(factor) ||
                factor <= 0d ||
                result > double.MaxValue / factor)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            result *= factor;
        }

        if (result < MaximumExactIntegerExclusive)
        {
            result = Math.Round(result);
        }
        return FormulaValueCoercion.SafeNumber(result);
    }

    private static CellValue Factorial(
        CellValue value,
        bool doubleFactorial)
    {
        if (!TryNonNegativeTruncatedNumber(
                value,
                out var truncated,
                out var error))
        {
            return error;
        }

        var maximum = doubleFactorial
            ? MaximumDoubleFactorialInput
            : MaximumFactorialInput;
        if (truncated > maximum)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var number = (int)truncated;
        var result = 1d;
        var step = doubleFactorial ? 2 : 1;
        for (var factor = number; factor > 1; factor -= step)
        {
            if (result > double.MaxValue / factor)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
            result *= factor;
        }

        return FormulaValueCoercion.SafeNumber(result);
    }

    private static CellValue GreatestCommonDivisor(
        IReadOnlyList<CellValue> arguments)
    {
        if (!TryGetIntegerArguments(arguments, out var values, out var error))
        {
            return error;
        }

        var result = 0UL;
        foreach (var value in values)
        {
            result = GreatestCommonDivisor(result, value);
        }
        return CellValue.FromNumber(result);
    }

    private static CellValue LeastCommonMultiple(
        IReadOnlyList<CellValue> arguments)
    {
        if (!TryGetIntegerArguments(arguments, out var values, out var error))
        {
            return error;
        }

        var result = 1UL;
        foreach (var value in values)
        {
            if (result == 0UL || value == 0UL)
            {
                result = 0UL;
                continue;
            }

            var divisor = GreatestCommonDivisor(result, value);
            var reduced = result / divisor;
            if (reduced >
                (MaximumExactIntegerExclusiveUnsigned - 1UL) / value)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            result = reduced * value;
            if (result >= MaximumExactIntegerExclusiveUnsigned)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
        }

        return CellValue.FromNumber(result);
    }

    private static bool TryGetIntegerArguments(
        IReadOnlyList<CellValue> arguments,
        out ulong[] values,
        out CellValue error)
    {
        values = new ulong[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryExactIntegerArgument(
                    arguments[index],
                    out values[index],
                    out error))
            {
                return false;
            }
        }

        error = default;
        return true;
    }

    private static bool TryExactIntegerArgument(
        CellValue value,
        out ulong result,
        out CellValue error)
    {
        if (!TryNonNegativeTruncatedNumber(
                value,
                out var number,
                out error))
        {
            result = default;
            return false;
        }
        if (number >= MaximumExactIntegerExclusive)
        {
            result = default;
            error = FormulaValueCoercion.Error("#NUM!");
            return false;
        }

        result = (ulong)number;
        error = default;
        return true;
    }

    private static bool TryNonNegativeTruncatedNumber(
        CellValue value,
        out double result,
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
        if (!double.IsFinite(number) || number < 0d)
        {
            result = default;
            error = FormulaValueCoercion.Error("#NUM!");
            return false;
        }

        result = Math.Truncate(number);
        error = default;
        return true;
    }

    private static ulong GreatestCommonDivisor(
        ulong left,
        ulong right)
    {
        while (right != 0UL)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
    }
}
