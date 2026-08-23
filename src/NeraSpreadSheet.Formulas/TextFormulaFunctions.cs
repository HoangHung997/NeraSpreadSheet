using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class TextFormulaFunctions
{
    private const int MaximumTextLength = 32_767;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "LEN",
            1,
            1,
            static (arguments, _) => CellValue.FromNumber(
                FormulaValueCoercion.ToText(arguments[0]).Length));
        yield return UnaryText(
            "LOWER",
            static text => text.ToLowerInvariant());
        yield return UnaryText(
            "UPPER",
            static text => text.ToUpperInvariant());
        yield return UnaryText("PROPER", Proper);
        yield return UnaryText("TRIM", TrimSpaces);
        yield return UnaryText("CLEAN", Clean);
        yield return FormulaFunctionFactory.Create(
            "LEFT",
            1,
            2,
            static (arguments, _) => LeftRight(arguments, left: true));
        yield return FormulaFunctionFactory.Create(
            "RIGHT",
            1,
            2,
            static (arguments, _) => LeftRight(arguments, left: false));
        yield return FormulaFunctionFactory.Create(
            "MID",
            3,
            3,
            static (arguments, _) => Mid(arguments));
        yield return FormulaFunctionFactory.Create(
            "REPT",
            2,
            2,
            static (arguments, _) => Rept(arguments));
        yield return FormulaFunctionFactory.Create(
            "EXACT",
            2,
            2,
            static (arguments, _) => CellValue.FromBoolean(
                string.Equals(
                    FormulaValueCoercion.ToText(arguments[0]),
                    FormulaValueCoercion.ToText(arguments[1]),
                    StringComparison.Ordinal)));
        yield return FormulaFunctionFactory.Create(
            "CONCAT",
            1,
            int.MaxValue,
            static (arguments, _) => Concat(arguments));
        yield return FormulaFunctionFactory.Create(
            "TEXTJOIN",
            3,
            int.MaxValue,
            static (arguments, _) => TextJoin(arguments));
        yield return FormulaFunctionFactory.Create(
            "FIND",
            2,
            3,
            static (arguments, _) => FindSearch(
                arguments,
                StringComparison.Ordinal));
        yield return FormulaFunctionFactory.Create(
            "SEARCH",
            2,
            3,
            static (arguments, _) => FindSearch(
                arguments,
                StringComparison.OrdinalIgnoreCase));
        yield return FormulaFunctionFactory.Create(
            "REPLACE",
            4,
            4,
            static (arguments, _) => Replace(arguments));
        yield return FormulaFunctionFactory.Create(
            "SUBSTITUTE",
            3,
            4,
            static (arguments, _) => Substitute(arguments));
        yield return FormulaFunctionFactory.Create(
            "VALUE",
            1,
            1,
            static (arguments, _) => Value(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "CHAR",
            1,
            1,
            static (arguments, _) => Character(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "CODE",
            1,
            1,
            static (arguments, _) => Code(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "UNICHAR",
            1,
            1,
            static (arguments, _) => UnicodeCharacter(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "UNICODE",
            1,
            1,
            static (arguments, _) => UnicodeCode(arguments[0]));
    }

    private static IFormulaFunction UnaryText(
        string name,
        Func<string, string> operation) =>
        FormulaFunctionFactory.Create(
            name,
            1,
            1,
            (arguments, _) => SafeText(
                operation(
                    FormulaValueCoercion.ToText(arguments[0]))));

    private static CellValue LeftRight(
        IReadOnlyList<CellValue> arguments,
        bool left)
    {
        var text = FormulaValueCoercion.ToText(arguments[0]);
        var count = 1;
        if (arguments.Count == 2 &&
            !TryNonNegativeInteger(arguments[1], out count))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        count = Math.Min(count, text.Length);
        var result = left
            ? text[..count]
            : text[(text.Length - count)..];
        return SafeText(result);
    }

    private static CellValue Mid(IReadOnlyList<CellValue> arguments)
    {
        var text = FormulaValueCoercion.ToText(arguments[0]);
        if (!FormulaValueCoercion.TryInteger(
                arguments[1],
                out var start,
                allowText: true) ||
            start <= 0 ||
            !TryNonNegativeInteger(arguments[2], out var count))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (start > text.Length)
        {
            return CellValue.Blank;
        }

        var zeroBasedStart = start - 1;
        var length = Math.Min(count, text.Length - zeroBasedStart);
        return SafeText(text.Substring(zeroBasedStart, length));
    }

    private static CellValue Rept(IReadOnlyList<CellValue> arguments)
    {
        var text = FormulaValueCoercion.ToText(arguments[0]);
        if (!TryNonNegativeInteger(arguments[1], out var count))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (text.Length == 0 || count == 0)
        {
            return CellValue.Blank;
        }
        if ((long)text.Length * count > MaximumTextLength)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var builder = new StringBuilder(text.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(text);
        }
        return CellValue.FromText(builder.ToString());
    }

    private static CellValue Concat(IReadOnlyList<CellValue> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            builder.Append(FormulaValueCoercion.ToText(argument));
            if (builder.Length > MaximumTextLength)
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
        }
        return CellValue.FromText(builder.ToString());
    }

    private static CellValue TextJoin(IReadOnlyList<CellValue> arguments)
    {
        var delimiter = FormulaValueCoercion.ToText(arguments[0]);
        if (!FormulaValueCoercion.TryBoolean(
                arguments[1],
                out var ignoreEmpty))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var values = new List<string>(arguments.Count - 2);
        for (var index = 2; index < arguments.Count; index++)
        {
            var value = FormulaValueCoercion.ToText(arguments[index]);
            if (ignoreEmpty && value.Length == 0)
            {
                continue;
            }
            values.Add(value);
        }
        var result = string.Join(delimiter, values);
        return SafeText(result);
    }

    private static CellValue FindSearch(
        IReadOnlyList<CellValue> arguments,
        StringComparison comparison)
    {
        var find = FormulaValueCoercion.ToText(arguments[0]);
        var within = FormulaValueCoercion.ToText(arguments[1]);
        var start = 1;
        if (arguments.Count == 3 &&
            (!FormulaValueCoercion.TryInteger(
                arguments[2],
                out start,
                allowText: true) ||
             start <= 0))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (start > within.Length + 1)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var index = within.IndexOf(
            find,
            start - 1,
            comparison);
        return index < 0
            ? FormulaValueCoercion.Error("#VALUE!")
            : CellValue.FromNumber(index + 1d);
    }

    private static CellValue Replace(IReadOnlyList<CellValue> arguments)
    {
        var oldText = FormulaValueCoercion.ToText(arguments[0]);
        if (!FormulaValueCoercion.TryInteger(
                arguments[1],
                out var start,
                allowText: true) ||
            start <= 0 ||
            !TryNonNegativeInteger(arguments[2], out var count))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (start > oldText.Length + 1)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var newText = FormulaValueCoercion.ToText(arguments[3]);
        var index = start - 1;
        var removeCount = Math.Min(count, oldText.Length - index);
        var result = oldText.Remove(index, removeCount)
            .Insert(index, newText);
        return SafeText(result);
    }

    private static CellValue Substitute(
        IReadOnlyList<CellValue> arguments)
    {
        var text = FormulaValueCoercion.ToText(arguments[0]);
        var oldText = FormulaValueCoercion.ToText(arguments[1]);
        var newText = FormulaValueCoercion.ToText(arguments[2]);
        if (oldText.Length == 0)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (arguments.Count == 3)
        {
            return SafeText(text.Replace(
                oldText,
                newText,
                StringComparison.Ordinal));
        }
        if (!FormulaValueCoercion.TryInteger(
                arguments[3],
                out var instance,
                allowText: true) ||
            instance <= 0)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var occurrence = 0;
        var searchStart = 0;
        while (searchStart <= text.Length - oldText.Length)
        {
            var found = text.IndexOf(
                oldText,
                searchStart,
                StringComparison.Ordinal);
            if (found < 0)
            {
                return CellValue.FromText(text);
            }
            occurrence++;
            if (occurrence == instance)
            {
                var result = text.Remove(found, oldText.Length)
                    .Insert(found, newText);
                return SafeText(result);
            }
            searchStart = found + oldText.Length;
        }
        return CellValue.FromText(text);
    }

    private static CellValue Value(CellValue value)
    {
        if (FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.SafeNumber(number);
        }
        return FormulaValueCoercion.Error("#VALUE!");
    }

    private static CellValue Character(CellValue value)
    {
        if (!FormulaValueCoercion.TryInteger(
                value,
                out var code,
                allowText: true) ||
            code is < 1 or > 255)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return CellValue.FromText(((char)code).ToString());
    }

    private static CellValue Code(CellValue value)
    {
        var text = FormulaValueCoercion.ToText(value);
        return text.Length == 0
            ? FormulaValueCoercion.Error("#VALUE!")
            : CellValue.FromNumber(text[0]);
    }

    private static CellValue UnicodeCharacter(CellValue value)
    {
        if (!FormulaValueCoercion.TryInteger(
                value,
                out var codePoint,
                allowText: true) ||
            codePoint <= 0 ||
            !Rune.IsValid(codePoint))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return CellValue.FromText(new Rune(codePoint).ToString());
    }

    private static CellValue UnicodeCode(CellValue value)
    {
        var text = FormulaValueCoercion.ToText(value);
        return text.Length == 0 ||
               !Rune.TryGetRuneAt(text, 0, out var rune)
            ? FormulaValueCoercion.Error("#VALUE!")
            : CellValue.FromNumber(rune.Value);
    }

    private static string Proper(string text)
    {
        var builder = new StringBuilder(text.Length);
        var startWord = true;
        foreach (var character in text)
        {
            if (char.IsLetter(character))
            {
                builder.Append(startWord
                    ? char.ToUpperInvariant(character)
                    : char.ToLowerInvariant(character));
                startWord = false;
            }
            else
            {
                builder.Append(character);
                startWord = !char.IsDigit(character);
            }
        }
        return builder.ToString();
    }

    private static string TrimSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character >= 32)
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    private static bool TryNonNegativeInteger(
        CellValue value,
        out int integer) =>
        FormulaValueCoercion.TryInteger(
            value,
            out integer,
            allowText: true) &&
        integer >= 0;

    private static CellValue SafeText(string text) =>
        text.Length > MaximumTextLength
            ? FormulaValueCoercion.Error("#VALUE!")
            : CellValue.FromText(text);
}
