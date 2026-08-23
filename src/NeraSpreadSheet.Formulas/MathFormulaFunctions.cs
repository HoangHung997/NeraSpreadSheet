using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class MathFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return UnaryNumber("ABS", static value => Math.Abs(value));
        yield return UnaryNumber("SIGN", static value => Math.Sign(value));
        yield return UnaryNumber("INT", static value => Math.Floor(value));
        yield return FormulaFunctionFactory.Create(
            "TRUNC",
            1,
            2,
            static (arguments, _) => Round(
                arguments,
                RoundingKind.TowardZero));
        yield return FormulaFunctionFactory.Create(
            "ROUND",
            2,
            2,
            static (arguments, _) => Round(
                arguments,
                RoundingKind.Nearest));
        yield return FormulaFunctionFactory.Create(
            "ROUNDDOWN",
            2,
            2,
            static (arguments, _) => Round(
                arguments,
                RoundingKind.TowardZero));
        yield return FormulaFunctionFactory.Create(
            "ROUNDUP",
            2,
            2,
            static (arguments, _) => Round(
                arguments,
                RoundingKind.AwayFromZero));
        yield return FormulaFunctionFactory.Create(
            "MOD",
            2,
            2,
            static (arguments, _) => Mod(arguments));
        yield return FormulaFunctionFactory.Create(
            "POWER",
            2,
            2,
            static (arguments, _) => BinaryMath(
                arguments,
                Math.Pow));
        yield return FormulaFunctionFactory.Create(
            "SQRT",
            1,
            1,
            static (arguments, _) => Sqrt(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "QUOTIENT",
            2,
            2,
            static (arguments, _) => Quotient(arguments));
        yield return FormulaFunctionFactory.Create(
            "EVEN",
            1,
            1,
            static (arguments, _) => EvenOdd(arguments[0], even: true));
        yield return FormulaFunctionFactory.Create(
            "ODD",
            1,
            1,
            static (arguments, _) => EvenOdd(arguments[0], even: false));
        yield return FormulaFunctionFactory.Create(
            "CEILING.MATH",
            1,
            3,
            static (arguments, _) => CeilingFloor(
                arguments,
                ceiling: true));
        yield return FormulaFunctionFactory.Create(
            "FLOOR.MATH",
            1,
            3,
            static (arguments, _) => CeilingFloor(
                arguments,
                ceiling: false));
        yield return FormulaFunctionFactory.Create(
            "PI",
            0,
            0,
            static (_, _) => CellValue.FromNumber(Math.PI));
        yield return UnaryNumber("EXP", Math.Exp);
        yield return FormulaFunctionFactory.Create(
            "LN",
            1,
            1,
            static (arguments, _) => PositiveDomain(
                arguments[0],
                Math.Log));
        yield return FormulaFunctionFactory.Create(
            "LOG10",
            1,
            1,
            static (arguments, _) => PositiveDomain(
                arguments[0],
                Math.Log10));
        yield return FormulaFunctionFactory.Create(
            "LOG",
            1,
            2,
            static (arguments, _) => Log(arguments));
        yield return UnaryNumber("SIN", Math.Sin);
        yield return UnaryNumber("COS", Math.Cos);
        yield return UnaryNumber("TAN", Math.Tan);
        yield return FormulaFunctionFactory.Create(
            "ASIN",
            1,
            1,
            static (arguments, _) => UnitDomain(
                arguments[0],
                Math.Asin));
        yield return FormulaFunctionFactory.Create(
            "ACOS",
            1,
            1,
            static (arguments, _) => UnitDomain(
                arguments[0],
                Math.Acos));
        yield return UnaryNumber("ATAN", Math.Atan);
        yield return FormulaFunctionFactory.Create(
            "ATAN2",
            2,
            2,
            static (arguments, _) => Atan2(arguments));
        yield return UnaryNumber(
            "DEGREES",
            static value => value * 180d / Math.PI);
        yield return UnaryNumber(
            "RADIANS",
            static value => value * Math.PI / 180d);
    }

    private static IFormulaFunction UnaryNumber(
        string name,
        Func<double, double> operation) =>
        FormulaFunctionFactory.Create(
            name,
            1,
            1,
            (arguments, _) =>
                FormulaValueCoercion.TryNumber(
                    arguments[0],
                    out var number,
                    allowText: true)
                    ? FormulaValueCoercion.SafeNumber(operation(number))
                    : FormulaValueCoercion.Error("#VALUE!"));

    private static CellValue Round(
        IReadOnlyList<CellValue> arguments,
        RoundingKind kind)
    {
        if (!FormulaValueCoercion.TryNumber(
                arguments[0],
                out var number,
                allowText: true) ||
            arguments.Count == 2 &&
            !FormulaValueCoercion.TryInteger(
                arguments[1],
                out _,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        var digits = arguments.Count == 2
            ? GetInteger(arguments[1])
            : 0;
        if (digits is < -308 or > 308)
        {
            return digits > 0
                ? FormulaValueCoercion.SafeNumber(number)
                : CellValue.FromNumber(0d);
        }

        var result = ApplyRounding(number, digits, kind);
        return FormulaValueCoercion.SafeNumber(result);
    }

    private static double ApplyRounding(
        double number,
        int digits,
        RoundingKind kind)
    {
        if (number == 0d)
        {
            return 0d;
        }
        var factor = Math.Pow(10d, Math.Abs(digits));
        if (!double.IsFinite(factor) || factor == 0d)
        {
            return digits >= 0 ? number : 0d;
        }
        var scaled = digits >= 0
            ? number * factor
            : number / factor;
        var rounded = kind switch
        {
            RoundingKind.Nearest => Math.Round(
                scaled,
                MidpointRounding.AwayFromZero),
            RoundingKind.TowardZero => Math.Truncate(scaled),
            RoundingKind.AwayFromZero => Math.CopySign(
                Math.Ceiling(Math.Abs(scaled)),
                scaled),
            _ => throw new InvalidOperationException(
                "Unknown rounding kind."),
        };
        return digits >= 0
            ? rounded / factor
            : rounded * factor;
    }

    private static CellValue Mod(IReadOnlyList<CellValue> arguments)
    {
        if (!TryTwoNumbers(arguments, out var number, out var divisor))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (Math.Abs(divisor) <= double.Epsilon)
        {
            return FormulaValueCoercion.Error("#DIV/0!");
        }
        return FormulaValueCoercion.SafeNumber(
            number - (divisor * Math.Floor(number / divisor)));
    }

    private static CellValue BinaryMath(
        IReadOnlyList<CellValue> arguments,
        Func<double, double, double> operation)
    {
        return TryTwoNumbers(arguments, out var left, out var right)
            ? FormulaValueCoercion.SafeNumber(operation(left, right))
            : FormulaValueCoercion.Error("#VALUE!");
    }

    private static CellValue Sqrt(CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return number < 0d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(Math.Sqrt(number));
    }

    private static CellValue Quotient(IReadOnlyList<CellValue> arguments)
    {
        if (!TryTwoNumbers(arguments, out var numerator, out var denominator))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (Math.Abs(denominator) <= double.Epsilon)
        {
            return FormulaValueCoercion.Error("#DIV/0!");
        }
        return FormulaValueCoercion.SafeNumber(
            Math.Truncate(numerator / denominator));
    }

    private static CellValue EvenOdd(CellValue value, bool even)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (number == 0d)
        {
            return CellValue.FromNumber(even ? 0d : 1d);
        }

        var magnitude = Math.Ceiling(Math.Abs(number));
        var remainder = magnitude % 2d;
        if (even && remainder != 0d ||
            !even && remainder == 0d)
        {
            magnitude++;
        }
        return FormulaValueCoercion.SafeNumber(
            Math.CopySign(magnitude, number));
    }

    private static CellValue CeilingFloor(
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
        if (arguments.Count >= 2 &&
            !FormulaValueCoercion.TryNumber(
                arguments[1],
                out significance,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        significance = Math.Abs(significance);
        if (Math.Abs(significance) <= double.Epsilon)
        {
            return CellValue.FromNumber(0d);
        }
        var mode = 0d;
        if (arguments.Count == 3 &&
            !FormulaValueCoercion.TryNumber(
                arguments[2],
                out mode,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var magnitude = Math.Abs(number) / significance;
        double roundedMagnitude;
        if (number >= 0d)
        {
            roundedMagnitude = ceiling
                ? Math.Ceiling(magnitude)
                : Math.Floor(magnitude);
        }
        else if (ceiling)
        {
            roundedMagnitude = Math.Abs(mode) > double.Epsilon
                ? Math.Ceiling(magnitude)
                : Math.Floor(magnitude);
        }
        else
        {
            roundedMagnitude = Math.Abs(mode) > double.Epsilon
                ? Math.Floor(magnitude)
                : Math.Ceiling(magnitude);
        }

        return FormulaValueCoercion.SafeNumber(
            Math.CopySign(
                roundedMagnitude * significance,
                number));
    }

    private static CellValue PositiveDomain(
        CellValue value,
        Func<double, double> operation)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return number <= 0d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(operation(number));
    }

    private static CellValue Log(IReadOnlyList<CellValue> arguments)
    {
        if (!FormulaValueCoercion.TryNumber(
                arguments[0],
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        var @base = 10d;
        if (arguments.Count == 2 &&
            !FormulaValueCoercion.TryNumber(
                arguments[1],
                out @base,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (number <= 0d || @base <= 0d ||
            Math.Abs(@base - 1d) <= double.Epsilon)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
        return FormulaValueCoercion.SafeNumber(
            Math.Log(number, @base));
    }

    private static CellValue UnitDomain(
        CellValue value,
        Func<double, double> operation)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return number is < -1d or > 1d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(operation(number));
    }

    private static CellValue Atan2(IReadOnlyList<CellValue> arguments)
    {
        if (!TryTwoNumbers(arguments, out var x, out var y))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (Math.Abs(x) <= double.Epsilon &&
            Math.Abs(y) <= double.Epsilon)
        {
            return FormulaValueCoercion.Error("#DIV/0!");
        }
        return FormulaValueCoercion.SafeNumber(Math.Atan2(y, x));
    }

    private static bool TryTwoNumbers(
        IReadOnlyList<CellValue> arguments,
        out double first,
        out double second) =>
        FormulaValueCoercion.TryNumber(
            arguments[0],
            out first,
            allowText: true) &&
        FormulaValueCoercion.TryNumber(
            arguments[1],
            out second,
            allowText: true);

    private static int GetInteger(CellValue value)
    {
        if (!FormulaValueCoercion.TryInteger(
                value,
                out var integer,
                allowText: true))
        {
            throw new InvalidOperationException(
                "The value was expected to be an integer.");
        }
        return integer;
    }

    private enum RoundingKind
    {
        Nearest,
        TowardZero,
        AwayFromZero,
    }
}
