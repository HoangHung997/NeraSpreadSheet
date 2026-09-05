using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AdvancedTrigonometricFormulaFunctions
{
    private const double MaximumTrigonometricMagnitude = 134_217_728d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return UnaryNumber(
            "ACOT",
            static value => Math.Atan2(1d, value));
        yield return FormulaFunctionFactory.Create(
            "ACOTH",
            1,
            1,
            static (arguments, _) => InverseHyperbolicCotangent(
                arguments[0]));
        yield return UnaryNumber("ASINH", Math.Asinh);
        yield return FormulaFunctionFactory.Create(
            "ACOSH",
            1,
            1,
            static (arguments, _) => InverseHyperbolicCosine(
                arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "ATANH",
            1,
            1,
            static (arguments, _) => InverseHyperbolicTangent(
                arguments[0]));
        yield return UnaryNumber("SINH", Math.Sinh);
        yield return UnaryNumber("COSH", Math.Cosh);
        yield return UnaryNumber("TANH", Math.Tanh);
        yield return FormulaFunctionFactory.Create(
            "COT",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Tan));
        yield return FormulaFunctionFactory.Create(
            "COTH",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Tanh));
        yield return FormulaFunctionFactory.Create(
            "CSC",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Sin));
        yield return FormulaFunctionFactory.Create(
            "CSCH",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Sinh));
        yield return FormulaFunctionFactory.Create(
            "SEC",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Cos));
        yield return FormulaFunctionFactory.Create(
            "SECH",
            1,
            1,
            static (arguments, _) => Reciprocal(
                arguments[0],
                Math.Cosh));
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

    private static CellValue InverseHyperbolicCotangent(
        CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (Math.Abs(number) <= 1d)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        return FormulaValueCoercion.SafeNumber(
            0.5d * Math.Log((number + 1d) / (number - 1d)));
    }

    private static CellValue InverseHyperbolicCosine(
        CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return number < 1d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(Math.Acosh(number));
    }

    private static CellValue InverseHyperbolicTangent(
        CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return number is <= -1d or >= 1d
            ? FormulaValueCoercion.Error("#NUM!")
            : FormulaValueCoercion.SafeNumber(Math.Atanh(number));
    }

    private static CellValue Reciprocal(
        CellValue value,
        Func<double, double> denominatorFunction)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (Math.Abs(number) >= MaximumTrigonometricMagnitude)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var denominator = denominatorFunction(number);
        if (denominator == 0d)
        {
            return FormulaValueCoercion.Error("#DIV/0!");
        }

        return FormulaValueCoercion.SafeNumber(1d / denominator);
    }
}
