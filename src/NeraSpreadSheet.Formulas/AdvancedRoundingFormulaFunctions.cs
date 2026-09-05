using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AdvancedRoundingFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "MROUND",
            2,
            2,
            static (arguments, _) => MRound(arguments));
        yield return FormulaFunctionFactory.Create(
            "CEILING",
            2,
            2,
            static (arguments, _) => LegacyMultipleRound(
                arguments,
                ceiling: true));
        yield return FormulaFunctionFactory.Create(
            "FLOOR",
            2,
            2,
            static (arguments, _) => LegacyMultipleRound(
                arguments,
                ceiling: false));
        yield return FormulaFunctionFactory.Create(
            "CEILING.PRECISE",
            1,
            2,
            static (arguments, _) => PreciseMultipleRound(
                arguments,
                ceiling: true));
        yield return FormulaFunctionFactory.Create(
            "FLOOR.PRECISE",
            1,
            2,
            static (arguments, _) => PreciseMultipleRound(
                arguments,
                ceiling: false));
        yield return FormulaFunctionFactory.Create(
            "ISO.CEILING",
            1,
            2,
            static (arguments, _) => PreciseMultipleRound(
                arguments,
                ceiling: true));
        yield return FormulaFunctionFactory.Create(
            "SQRTPI",
            1,
            1,
            static (arguments, _) => SqrtPi(arguments[0]));
    }

    private static CellValue MRound(
        IReadOnlyList<CellValue> arguments)
    {
        if (!TryTwoNumbers(
                arguments,
                out var number,
                out var multiple))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (number == 0d || multiple == 0d)
        {
            return CellValue.FromNumber(0d);
        }
        if (Math.Sign(number) != Math.Sign(multiple))
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var ratio = number / multiple;
        var rounded = Math.Round(
            ratio,
            MidpointRounding.AwayFromZero);
        return FormulaValueCoercion.SafeNumber(
            rounded * multiple);
    }

    private static CellValue LegacyMultipleRound(
        IReadOnlyList<CellValue> arguments,
        bool ceiling)
    {
        if (!TryTwoNumbers(
                arguments,
                out var number,
                out var significance))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (significance == 0d)
        {
            return CellValue.FromNumber(0d);
        }
        if (number != 0d &&
            Math.Sign(number) != Math.Sign(significance))
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var ratio = number / significance;
        var rounded = ceiling
            ? Math.Ceiling(ratio)
            : Math.Floor(ratio);
        return FormulaValueCoercion.SafeNumber(
            rounded * significance);
    }

    private static CellValue PreciseMultipleRound(
        IReadOnlyList<CellValue> arguments,
        bool ceiling)
    {
        if (!FormulaValueCoercion.TryNumber(
                arguments[0],
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var significance = 1d;
        if (arguments.Count == 2 &&
            !FormulaValueCoercion.TryNumber(
                arguments[1],
                out significance,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        if (!double.IsFinite(number) ||
            !double.IsFinite(significance))
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        significance = Math.Abs(significance);
        if (significance == 0d)
        {
            return CellValue.FromNumber(0d);
        }

        var ratio = number / significance;
        var rounded = ceiling
            ? Math.Ceiling(ratio)
            : Math.Floor(ratio);
        return FormulaValueCoercion.SafeNumber(
            rounded * significance);
    }

    private static CellValue SqrtPi(CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return !double.IsFinite(number) || number < 0d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(
                Math.Sqrt(number * Math.PI));
    }

    private static bool TryTwoNumbers(
        IReadOnlyList<CellValue> arguments,
        out double first,
        out double second)
    {
        second = default;
        return FormulaValueCoercion.TryNumber(
                arguments[0],
                out first,
                allowText: true) &&
            double.IsFinite(first) &&
            FormulaValueCoercion.TryNumber(
                arguments[1],
                out second,
                allowText: true) &&
            double.IsFinite(second);
    }
}
