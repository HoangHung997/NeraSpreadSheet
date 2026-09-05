using System.Globalization;
using System.Numerics;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class ComplexFormulaMath
{
    internal readonly record struct Operand(Complex Value, char Suffix)
    {
        public char EffectiveSuffix => Suffix == '\0' ? 'i' : Suffix;
    }

    public static bool TryRead(
        FormulaFunctionArgument argument,
        out Operand operand,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            operand = default;
            error = InvalidValue();
            return false;
        }
        return TryRead(argument.ScalarValue, out operand, out error);
    }

    public static bool TryRead(
        CellValue value,
        out Operand operand,
        out FormulaEvaluationResult error)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                operand = new Operand(
                    new Complex((double)value.RawValue!, 0d),
                    '\0');
                error = default!;
                return true;
            case CellValueKind.Boolean:
                operand = new Operand(
                    new Complex((bool)value.RawValue! ? 1d : 0d, 0d),
                    '\0');
                error = default!;
                return true;
            case CellValueKind.Blank:
                operand = new Operand(Complex.Zero, '\0');
                error = default!;
                return true;
            case CellValueKind.DateTime:
                if (FormulaValueCoercion.TryNumber(value, out var serial) &&
                    double.IsFinite(serial))
                {
                    operand = new Operand(new Complex(serial, 0d), '\0');
                    error = default!;
                    return true;
                }
                break;
            case CellValueKind.Text:
                return TryParseText(
                    (string)value.RawValue!,
                    out operand,
                    out error);
        }
        operand = default;
        error = InvalidValue();
        return false;
    }

    public static bool TryReadReal(
        FormulaFunctionArgument argument,
        out double number,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            number = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    public static bool TryReadSuffix(
        FormulaFunctionArgument argument,
        out char suffix,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            suffix = default;
            error = InvalidValue();
            return false;
        }
        var text = FormulaValueCoercion.ToText(argument.ScalarValue).Trim();
        if (text.Length != 1 ||
            char.ToLowerInvariant(text[0]) is not ('i' or 'j'))
        {
            suffix = default;
            error = InvalidValue();
            return false;
        }
        suffix = char.ToLowerInvariant(text[0]);
        error = default!;
        return true;
    }

    public static bool TryMergeSuffix(
        char current,
        char candidate,
        out char merged)
    {
        if (candidate == '\0')
        {
            merged = current;
            return true;
        }
        if (current == '\0' || current == candidate)
        {
            merged = candidate;
            return true;
        }
        merged = default;
        return false;
    }

    public static FormulaEvaluationResult ComplexText(
        Complex value,
        char suffix = 'i')
    {
        if (!IsFinite(value))
        {
            return NumericError();
        }
        suffix = suffix == '\0' ? 'i' : char.ToLowerInvariant(suffix);
        if (suffix is not ('i' or 'j'))
        {
            return InvalidValue();
        }
        return FormulaEvaluationResult.Success(
            CellValue.FromText(Format(value, suffix)));
    }

    public static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    public static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    public static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    public static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    public static bool IsZero(Complex value) =>
        value.Real == 0d && value.Imaginary == 0d;

    public static bool IsFinite(Complex value) =>
        double.IsFinite(value.Real) &&
        double.IsFinite(value.Imaginary);

    private static bool TryParseText(
        string source,
        out Operand operand,
        out FormulaEvaluationResult error)
    {
        var text = source.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (text.Length == 0)
        {
            operand = default;
            error = InvalidValue();
            return false;
        }

        var suffix = '\0';
        var last = char.ToLowerInvariant(text[^1]);
        if (last is 'i' or 'j')
        {
            suffix = last;
            text = text[..^1];
            if (text.Length == 0 || text == "+")
            {
                operand = new Operand(new Complex(0d, 1d), suffix);
                error = default!;
                return true;
            }
            if (text == "-")
            {
                operand = new Operand(new Complex(0d, -1d), suffix);
                error = default!;
                return true;
            }

            var split = FindImaginarySeparator(text);
            if (split > 0)
            {
                if (!TryParseNumber(text[..split], out var real) ||
                    !TryParseImaginary(text[split..], out var imaginary))
                {
                    operand = default;
                    error = InvalidValue();
                    return false;
                }
                operand = new Operand(new Complex(real, imaginary), suffix);
                error = default!;
                return true;
            }

            if (!TryParseImaginary(text, out var onlyImaginary))
            {
                operand = default;
                error = InvalidValue();
                return false;
            }
            operand = new Operand(new Complex(0d, onlyImaginary), suffix);
            error = default!;
            return true;
        }

        if (!TryParseNumber(text, out var realOnly))
        {
            operand = default;
            error = InvalidValue();
            return false;
        }
        operand = new Operand(new Complex(realOnly, 0d), '\0');
        error = default!;
        return true;
    }

    private static int FindImaginarySeparator(string text)
    {
        for (var index = text.Length - 1; index > 0; index--)
        {
            if (text[index] is not ('+' or '-'))
            {
                continue;
            }
            if (text[index - 1] is 'e' or 'E')
            {
                continue;
            }
            return index;
        }
        return -1;
    }

    private static bool TryParseImaginary(string text, out double value)
    {
        if (text == "+")
        {
            value = 1d;
            return true;
        }
        if (text == "-")
        {
            value = -1d;
            return true;
        }
        return TryParseNumber(text, out value);
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) &&
        double.IsFinite(value);

    private static string Format(Complex value, char suffix)
    {
        var real = NormalizeZero(value.Real);
        var imaginary = NormalizeZero(value.Imaginary);
        if (imaginary == 0d)
        {
            return FormatNumber(real);
        }

        var imaginaryMagnitude = Math.Abs(imaginary);
        var imaginaryText = imaginaryMagnitude == 1d
            ? suffix.ToString()
            : string.Concat(FormatNumber(imaginaryMagnitude), suffix);
        if (real == 0d)
        {
            return imaginary < 0d
                ? string.Concat("-", imaginaryText)
                : imaginaryText;
        }
        return string.Concat(
            FormatNumber(real),
            imaginary < 0d ? "-" : "+",
            imaginaryText);
    }

    private static double NormalizeZero(double value) =>
        Math.Abs(value) < 1e-14d ? 0d : value;

    private static string FormatNumber(double value) =>
        value.ToString("G15", CultureInfo.InvariantCulture);
}
