using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class BaseAndRomanFormulaFunctions
{
    private const double MaximumExactInteger =
        9_007_199_254_740_991d;
    private const int MaximumTextLength = 255;
    private const string BaseDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "BASE",
            2,
            3,
            static (arguments, _) => Base(arguments));
        yield return FormulaFunctionFactory.Create(
            "DECIMAL",
            2,
            2,
            static (arguments, _) => Decimal(arguments));
        yield return FormulaFunctionFactory.Create(
            "ARABIC",
            1,
            1,
            static (arguments, _) => Arabic(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "ROMAN",
            1,
            2,
            static (arguments, _) => Roman(arguments));
    }

    private static CellValue Base(
        IReadOnlyList<CellValue> arguments)
    {
        if (!FormulaValueCoercion.TryNumber(
                arguments[0],
                out var number,
                allowText: true) ||
            !FormulaValueCoercion.TryNumber(
                arguments[1],
                out var radixValue,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var minimumLength = 0;
        if (arguments.Count == 3)
        {
            if (!FormulaValueCoercion.TryNumber(
                    arguments[2],
                    out var minimumLengthValue,
                    allowText: true))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
            if (!TryTruncatedInt(minimumLengthValue, out minimumLength))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
        }

        if (!TryTruncatedInt(radixValue, out var radix))
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        number = Math.Truncate(number);
        if (!double.IsFinite(number) ||
            number < 0d ||
            number > MaximumExactInteger ||
            radix is < 2 or > 36 ||
            minimumLength is < 0 or > MaximumTextLength)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var converted = ConvertToBase((ulong)number, radix);
        if (converted.Length < minimumLength)
        {
            converted = converted.PadLeft(
                minimumLength,
                '0');
        }
        return CellValue.FromText(converted);
    }

    private static CellValue Decimal(
        IReadOnlyList<CellValue> arguments)
    {
        var text = FormulaValueCoercion.ToText(arguments[0]).Trim();
        if (!FormulaValueCoercion.TryNumber(
                arguments[1],
                out var radixValue,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        if (!TryTruncatedInt(radixValue, out var radix))
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        if (radix is < 2 or > 36 ||
            text.Length == 0 ||
            text.Length > MaximumTextLength)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var negative = text[0] == '-';
        var start = negative || text[0] == '+' ? 1 : 0;
        if (start == text.Length)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        ulong result = 0UL;
        for (var index = start; index < text.Length; index++)
        {
            var digit = GetBaseDigit(text[index]);
            if (digit < 0 || digit >= radix)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
            if (result >
                ((ulong)MaximumExactInteger - (ulong)digit) /
                (ulong)radix)
            {
                return FormulaValueCoercion.Error("#NUM!");
            }

            result = (result * (ulong)radix) + (ulong)digit;
        }

        var number = (double)result;
        return CellValue.FromNumber(
            negative ? -number : number);
    }

    private static CellValue Arabic(CellValue value)
    {
        var roman = FormulaValueCoercion.ToText(value)
            .Trim()
            .ToUpperInvariant();
        if (roman.Length > MaximumTextLength)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var result = 0;
        var validRest = 3999;
        var index = 0;
        while (index < roman.Length)
        {
            if (!TryRomanValue(
                    roman[index],
                    out var first,
                    out var firstIsDecimal))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }

            var second = 0;
            if (index + 1 < roman.Length &&
                !TryRomanValue(
                    roman[index + 1],
                    out second,
                    out _))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }

            if (first >= second)
            {
                result += first;
                validRest %= first * (firstIsDecimal ? 5 : 2);
                if (validRest < first)
                {
                    return FormulaValueCoercion.Error("#VALUE!");
                }
                validRest -= first;
                index++;
                continue;
            }

            if (first * 2 == second)
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }

            var difference = second - first;
            result += difference;
            if (validRest < difference)
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
            validRest = first - 1;
            index += 2;
        }

        return CellValue.FromNumber(result);
    }

    private static CellValue Roman(
        IReadOnlyList<CellValue> arguments)
    {
        if (!FormulaValueCoercion.TryNumber(
                arguments[0],
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var form = 0;
        if (arguments.Count == 2)
        {
            if (!FormulaValueCoercion.TryNumber(
                    arguments[1],
                    out var formValue,
                    allowText: true))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
            if (!TryTruncatedInt(formValue, out form))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
        }

        number = Math.Truncate(number);
        if (!double.IsFinite(number) ||
            number < 0d ||
            number >= 4000d ||
            form is < 0 or > 4)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var remaining = (int)number;
        var chars = new[] { 'M', 'D', 'C', 'L', 'X', 'V', 'I' };
        var values = new[] { 1000, 500, 100, 50, 10, 5, 1 };
        var builder = new StringBuilder();

        for (var group = 0; group <= 3; group++)
        {
            var primaryIndex = 2 * group;
            var digit = remaining / values[primaryIndex];

            if (digit % 5 == 4)
            {
                var secondaryIndex = digit == 4
                    ? primaryIndex - 1
                    : primaryIndex - 2;
                var steps = 0;
                while (steps < form &&
                       primaryIndex < values.Length - 1)
                {
                    steps++;
                    if (values[secondaryIndex] -
                        values[primaryIndex + 1] <= remaining)
                    {
                        primaryIndex++;
                    }
                    else
                    {
                        steps = form;
                    }
                }

                builder.Append(chars[primaryIndex]);
                builder.Append(chars[secondaryIndex]);
                remaining += values[primaryIndex];
                remaining -= values[secondaryIndex];
            }
            else
            {
                if (digit > 4)
                {
                    builder.Append(chars[primaryIndex - 1]);
                }

                builder.Append(
                    chars[primaryIndex],
                    digit % 5);
                remaining %= values[primaryIndex];
            }
        }

        return CellValue.FromText(builder.ToString());
    }

    private static bool TryTruncatedInt(
        double value,
        out int result)
    {
        var truncated = Math.Truncate(value);
        if (!double.IsFinite(truncated) ||
            truncated < int.MinValue ||
            truncated > int.MaxValue)
        {
            result = default;
            return false;
        }

        result = (int)truncated;
        return true;
    }

    private static string ConvertToBase(ulong number, int radix)
    {
        if (number == 0UL)
        {
            return "0";
        }

        Span<char> buffer = stackalloc char[64];
        var position = buffer.Length;
        while (number > 0UL)
        {
            var digit = (int)(number % (ulong)radix);
            buffer[--position] = BaseDigits[digit];
            number /= (ulong)radix;
        }

        return new string(buffer[position..]);
    }

    private static int GetBaseDigit(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return value - '0';
        }

        var upper = char.ToUpperInvariant(value);
        return upper is >= 'A' and <= 'Z'
            ? 10 + (upper - 'A')
            : -1;
    }

    private static bool TryRomanValue(
        char value,
        out int result,
        out bool isDecimal)
    {
        switch (value)
        {
            case 'M':
                result = 1000;
                isDecimal = true;
                return true;
            case 'D':
                result = 500;
                isDecimal = false;
                return true;
            case 'C':
                result = 100;
                isDecimal = true;
                return true;
            case 'L':
                result = 50;
                isDecimal = false;
                return true;
            case 'X':
                result = 10;
                isDecimal = true;
                return true;
            case 'V':
                result = 5;
                isDecimal = false;
                return true;
            case 'I':
                result = 1;
                isDecimal = true;
                return true;
            default:
                result = default;
                isDecimal = default;
                return false;
        }
    }
}
