using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// First-generation deterministic engineering functions. The conversion
/// family follows the fixed-width two's-complement conventions used by common
/// spreadsheet implementations while remaining bounded and culture-neutral.
/// </summary>
internal static class EngineeringFormulaFunctions
{
    private const long MaximumBitValue = (1L << 48) - 1L;
    private const int MaximumShift = 53;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateScalarDefinition("DELTA", 1, 2, EvaluateDelta);
        yield return CreateScalarDefinition("GESTEP", 1, 2, EvaluateGreaterOrEqualStep);
        yield return CreateScalarDefinition("BITAND", 2, 2, static invocation =>
            EvaluateBitwise(invocation, static (left, right) => left & right));
        yield return CreateScalarDefinition("BITOR", 2, 2, static invocation =>
            EvaluateBitwise(invocation, static (left, right) => left | right));
        yield return CreateScalarDefinition("BITXOR", 2, 2, static invocation =>
            EvaluateBitwise(invocation, static (left, right) => left ^ right));
        yield return CreateScalarDefinition("BITLSHIFT", 2, 2, static invocation =>
            EvaluateShift(invocation, leftFunction: true));
        yield return CreateScalarDefinition("BITRSHIFT", 2, 2, static invocation =>
            EvaluateShift(invocation, leftFunction: false));

        yield return CreateScalarDefinition("DEC2BIN", 1, 2, invocation =>
            EvaluateDecimalToBase(invocation, BinaryFormat));
        yield return CreateScalarDefinition("DEC2OCT", 1, 2, invocation =>
            EvaluateDecimalToBase(invocation, OctalFormat));
        yield return CreateScalarDefinition("DEC2HEX", 1, 2, invocation =>
            EvaluateDecimalToBase(invocation, HexadecimalFormat));

        yield return CreateScalarDefinition("BIN2DEC", 1, 1, invocation =>
            EvaluateBaseToDecimal(invocation, BinaryFormat));
        yield return CreateScalarDefinition("OCT2DEC", 1, 1, invocation =>
            EvaluateBaseToDecimal(invocation, OctalFormat));
        yield return CreateScalarDefinition("HEX2DEC", 1, 1, invocation =>
            EvaluateBaseToDecimal(invocation, HexadecimalFormat));

        yield return CreateScalarDefinition("BIN2OCT", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, BinaryFormat, OctalFormat));
        yield return CreateScalarDefinition("BIN2HEX", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, BinaryFormat, HexadecimalFormat));
        yield return CreateScalarDefinition("OCT2BIN", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, OctalFormat, BinaryFormat));
        yield return CreateScalarDefinition("OCT2HEX", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, OctalFormat, HexadecimalFormat));
        yield return CreateScalarDefinition("HEX2BIN", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, HexadecimalFormat, BinaryFormat));
        yield return CreateScalarDefinition("HEX2OCT", 1, 2, invocation =>
            EvaluateBaseToBase(invocation, HexadecimalFormat, OctalFormat));
    }

    private static FormulaFunctionDefinition CreateScalarDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateDelta(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var left, out var error))
        {
            return error;
        }
        var right = 0d;
        if (invocation.Arguments.Count == 2 &&
            !TryGetScalarNumber(invocation.Arguments[1], out right, out error))
        {
            return error;
        }
        return Number(left.Equals(right) ? 1d : 0d);
    }

    private static FormulaEvaluationResult EvaluateGreaterOrEqualStep(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var number, out var error))
        {
            return error;
        }
        var step = 0d;
        if (invocation.Arguments.Count == 2 &&
            !TryGetScalarNumber(invocation.Arguments[1], out step, out error))
        {
            return error;
        }
        return Number(number >= step ? 1d : 0d);
    }

    private static FormulaEvaluationResult EvaluateBitwise(
        FormulaFunctionInvocation invocation,
        Func<long, long, long> operation)
    {
        if (!TryGetBitValue(invocation.Arguments[0], out var left, out var error) ||
            !TryGetBitValue(invocation.Arguments[1], out var right, out error))
        {
            return error;
        }
        return Number(operation(left, right));
    }

    private static FormulaEvaluationResult EvaluateShift(
        FormulaFunctionInvocation invocation,
        bool leftFunction)
    {
        if (!TryGetBitValue(invocation.Arguments[0], out var value, out var error) ||
            !TryGetTruncatedInteger(invocation.Arguments[1], out var shift, out error))
        {
            return error;
        }
        if (shift is < -MaximumShift or > MaximumShift)
        {
            return NumericError();
        }

        var effectiveLeft = leftFunction ? shift >= 0 : shift < 0;
        var magnitude = Math.Abs(shift);
        if (magnitude == 0)
        {
            return Number(value);
        }
        if (!effectiveLeft)
        {
            return Number(value >> magnitude);
        }
        if (magnitude >= 48 && value != 0L ||
            magnitude < 48 && value > (MaximumBitValue >> magnitude))
        {
            return NumericError();
        }
        return Number(value << magnitude);
    }

    private static FormulaEvaluationResult EvaluateDecimalToBase(
        FormulaFunctionInvocation invocation,
        RadixFormat format)
    {
        if (!TryGetTruncatedLong(invocation.Arguments[0], out var value, out var error))
        {
            return error;
        }
        if (value < format.MinimumSignedValue ||
            value > format.MaximumPositiveValue)
        {
            return NumericError();
        }

        int? places = null;
        if (invocation.Arguments.Count == 2 &&
            !TryGetPlaces(invocation.Arguments[1], out places, out error))
        {
            return error;
        }
        return FormatValue(value, format, places);
    }

    private static FormulaEvaluationResult EvaluateBaseToDecimal(
        FormulaFunctionInvocation invocation,
        RadixFormat sourceFormat)
    {
        if (!TryParseRadixValue(
                invocation.Arguments[0],
                sourceFormat,
                out var value,
                out var error))
        {
            return error;
        }
        return Number(value);
    }

    private static FormulaEvaluationResult EvaluateBaseToBase(
        FormulaFunctionInvocation invocation,
        RadixFormat sourceFormat,
        RadixFormat targetFormat)
    {
        if (!TryParseRadixValue(
                invocation.Arguments[0],
                sourceFormat,
                out var value,
                out var error))
        {
            return error;
        }
        if (value < targetFormat.MinimumSignedValue ||
            value > targetFormat.MaximumPositiveValue)
        {
            return NumericError();
        }

        int? places = null;
        if (invocation.Arguments.Count == 2 &&
            !TryGetPlaces(invocation.Arguments[1], out places, out error))
        {
            return error;
        }
        return FormatValue(value, targetFormat, places);
    }

    private static FormulaEvaluationResult FormatValue(
        long value,
        RadixFormat format,
        int? places)
    {
        if (value < 0L)
        {
            var unsigned = checked((1L << format.BitWidth) + value);
            return Text(Convert.ToString(unsigned, format.Radix)!
                .ToUpperInvariant()
                .PadLeft(format.MaximumDigits, '0'));
        }

        var text = Convert.ToString(value, format.Radix)!.ToUpperInvariant();
        if (places.HasValue)
        {
            if (places.Value < text.Length)
            {
                return NumericError();
            }
            text = text.PadLeft(places.Value, '0');
        }
        return Text(text);
    }

    private static bool TryParseRadixValue(
        FormulaFunctionArgument argument,
        RadixFormat format,
        out long value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !TryGetRadixText(argument.ScalarValue, out var text))
        {
            value = default;
            error = InvalidValue();
            return false;
        }
        if (text.Length == 0 || text.Length > format.MaximumDigits)
        {
            value = default;
            error = NumericError();
            return false;
        }

        long unsigned = 0L;
        foreach (var character in text)
        {
            var digit = GetDigit(character);
            if (digit < 0 || digit >= format.Radix)
            {
                value = default;
                error = NumericError();
                return false;
            }
            unsigned = checked((unsigned * format.Radix) + digit);
        }

        var signBitSet = text.Length == format.MaximumDigits &&
                         unsigned >= (1L << (format.BitWidth - 1));
        value = signBitSet
            ? unsigned - (1L << format.BitWidth)
            : unsigned;
        error = default!;
        return true;
    }

    private static bool TryGetRadixText(CellValue value, out string text)
    {
        switch (value.Kind)
        {
            case CellValueKind.Text:
                text = ((string)value.RawValue!).Trim();
                return true;
            case CellValueKind.Number:
            case CellValueKind.Boolean:
            case CellValueKind.Blank:
            case CellValueKind.DateTime:
                if (FormulaValueCoercion.TryNumber(value, out var number) &&
                    double.IsFinite(number) &&
                    number >= long.MinValue &&
                    number <= long.MaxValue)
                {
                    text = Math.Truncate(number)
                        .ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                break;
        }
        text = string.Empty;
        return false;
    }

    private static int GetDigit(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return character - '0';
        }
        var upper = char.ToUpperInvariant(character);
        return upper is >= 'A' and <= 'F'
            ? upper - 'A' + 10
            : -1;
    }

    private static bool TryGetBitValue(
        FormulaFunctionArgument argument,
        out long value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetTruncatedLong(argument, out value, out error))
        {
            return false;
        }
        if (value is < 0L or > MaximumBitValue)
        {
            value = default;
            error = NumericError();
            return false;
        }
        return true;
    }

    private static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error) ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            value = default;
            if (error is null)
            {
                error = NumericError();
            }
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private static bool TryGetTruncatedLong(
        FormulaFunctionArgument argument,
        out long value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error) ||
            number < long.MinValue ||
            number > long.MaxValue)
        {
            value = default;
            if (error is null)
            {
                error = NumericError();
            }
            return false;
        }
        value = checked((long)Math.Truncate(number));
        return true;
    }

    private static bool TryGetPlaces(
        FormulaFunctionArgument argument,
        out int? places,
        out FormulaEvaluationResult error)
    {
        if (!TryGetTruncatedInteger(argument, out var parsed, out error))
        {
            places = null;
            return false;
        }
        if (parsed is < 1 or > 10)
        {
            places = null;
            error = NumericError();
            return false;
        }
        places = parsed;
        return true;
    }

    private static bool TryGetScalarNumber(
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

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult Text(string value) =>
        FormulaEvaluationResult.Success(CellValue.FromText(value));

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private static readonly RadixFormat BinaryFormat = new(
        Radix: 2,
        MaximumDigits: 10,
        BitWidth: 10,
        MinimumSignedValue: -512L,
        MaximumPositiveValue: 511L);

    private static readonly RadixFormat OctalFormat = new(
        Radix: 8,
        MaximumDigits: 10,
        BitWidth: 30,
        MinimumSignedValue: -536_870_912L,
        MaximumPositiveValue: 536_870_911L);

    private static readonly RadixFormat HexadecimalFormat = new(
        Radix: 16,
        MaximumDigits: 10,
        BitWidth: 40,
        MinimumSignedValue: -549_755_813_888L,
        MaximumPositiveValue: 549_755_813_887L);

    private readonly record struct RadixFormat(
        int Radix,
        int MaximumDigits,
        int BitWidth,
        long MinimumSignedValue,
        long MaximumPositiveValue);
}
