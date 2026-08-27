using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class F018TextCompatibilityFormulaFunctions
{
    private const int MaximumTextLength = 32_767;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly char[] HalfWidthKatakana =
        "｡｢｣､･ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝﾞﾟ".ToCharArray();

    private static readonly string[] FullWidthKatakana =
    [
        "。", "「", "」", "、", "・", "ヲ", "ァ", "ィ", "ゥ", "ェ", "ォ",
        "ャ", "ュ", "ョ", "ッ", "ー", "ア", "イ", "ウ", "エ", "オ",
        "カ", "キ", "ク", "ケ", "コ", "サ", "シ", "ス", "セ", "ソ",
        "タ", "チ", "ツ", "テ", "ト", "ナ", "ニ", "ヌ", "ネ", "ノ",
        "ハ", "ヒ", "フ", "ヘ", "ホ", "マ", "ミ", "ム", "メ", "モ",
        "ヤ", "ユ", "ヨ", "ラ", "リ", "ル", "レ", "ロ", "ワ", "ン",
        "\u3099", "\u309A",
    ];

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return Definition("ASC", 1, 1, ScalarRange, EvaluateAsc);
        yield return Definition("ARRAYTOTEXT", 1, 2, ScalarRange, EvaluateArrayToText);
        yield return Definition("BAHTTEXT", 1, 1, ScalarRange, EvaluateBahtText);
        yield return Definition("CONCATENATE", 1, 255, ScalarRange, EvaluateConcatenate);
        yield return Definition("DBCS", 1, 1, ScalarRange, EvaluateDbcs);
        yield return Definition("DOLLAR", 1, 2, ScalarRange, EvaluateDollar);
        yield return Definition("FINDB", 2, 3, ScalarRange, invocation => EvaluateFindB(invocation, caseSensitive: true));
        yield return Definition("FIXED", 1, 3, ScalarRange, EvaluateFixed);
        yield return Definition("JIS", 1, 1, ScalarRange, EvaluateDbcs);
        yield return Definition("LEFTB", 1, 2, ScalarRange, invocation => EvaluateLeftRightB(invocation, left: true));
        yield return Definition("LENB", 1, 1, ScalarRange, EvaluateLenB);
        yield return Definition("MIDB", 3, 3, ScalarRange, EvaluateMidB);
        yield return Definition("REGEXEXTRACT", 2, 4, ScalarRange, EvaluateRegexExtract);
        yield return Definition("REGEXREPLACE", 3, 5, ScalarRange, EvaluateRegexReplace);
        yield return Definition("REGEXTEST", 2, 3, ScalarRange, EvaluateRegexTest);
        yield return Definition("REPLACEB", 4, 4, ScalarRange, EvaluateReplaceB);
        yield return Definition("RIGHTB", 1, 2, ScalarRange, invocation => EvaluateLeftRightB(invocation, left: false));
        yield return Definition("SEARCHB", 2, 3, ScalarRange, invocation => EvaluateFindB(invocation, caseSensitive: false));
        yield return Definition("TEXTAFTER", 2, 6, ScalarRange, invocation => EvaluateTextRelative(invocation, after: true));
        yield return Definition("TEXTBEFORE", 2, 6, ScalarRange, invocation => EvaluateTextRelative(invocation, after: false));
    }

    private const FormulaFunctionCapabilities ScalarRange =
        FormulaFunctionCapabilities.ScalarArguments |
        FormulaFunctionCapabilities.RangeArguments;

    private static FormulaFunctionDefinition Definition(
        string name,
        int minimumArguments,
        int maximumArguments,
        FormulaFunctionCapabilities capabilities,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                capabilities | FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateAsc(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error))
        {
            return error;
        }
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var rune in decomposed.EnumerateRunes())
        {
            if (rune.Value == 0x3000)
            {
                builder.Append(' ');
                continue;
            }
            if (rune.Value is >= 0xFF01 and <= 0xFF5E)
            {
                builder.Append((char)(rune.Value - 0xFEE0));
                continue;
            }
            var mapped = FindHalfWidthKatakana(rune.Value);
            if (mapped != '\0')
            {
                builder.Append(mapped);
                continue;
            }
            builder.Append(rune.ToString());
        }
        return Text(builder.ToString());
    }

    private static FormulaEvaluationResult EvaluateDbcs(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error))
        {
            return error;
        }
        var builder = new StringBuilder(text.Length * 2);
        foreach (var character in text)
        {
            if (character == ' ')
            {
                builder.Append('\u3000');
            }
            else if (character is >= '!' and <= '~')
            {
                builder.Append((char)(character + 0xFEE0));
            }
            else
            {
                var index = Array.IndexOf(HalfWidthKatakana, character);
                builder.Append(index >= 0 ? FullWidthKatakana[index] : character.ToString());
            }
        }
        return Text(builder.ToString().Normalize(NormalizationForm.FormC));
    }

    private static char FindHalfWidthKatakana(int runeValue)
    {
        if (runeValue == 0x3099)
        {
            return 'ﾞ';
        }
        if (runeValue == 0x309A)
        {
            return 'ﾟ';
        }
        var text = new Rune(runeValue).ToString();
        for (var index = 0; index < FullWidthKatakana.Length - 2; index++)
        {
            if (string.Equals(FullWidthKatakana[index], text, StringComparison.Ordinal))
            {
                return HalfWidthKatakana[index];
            }
        }
        return '\0';
    }

    private static FormulaEvaluationResult EvaluateArrayToText(FormulaFunctionInvocation invocation)
    {
        var format = 0;
        if (invocation.Arguments.Count == 2 &&
            !TryScalarInteger(invocation.Arguments[1], out format, out var error))
        {
            return error;
        }
        if (format is not (0 or 1))
        {
            return NumericError();
        }
        var argument = invocation.Arguments[0];
        var (rows, columns) = Shape(argument);
        if ((long)rows * columns != argument.Values.Count)
        {
            return InvalidValue();
        }
        var builder = new StringBuilder();
        if (format == 1)
        {
            builder.Append('{');
        }
        for (var row = 0; row < rows; row++)
        {
            if (row > 0)
            {
                builder.Append(format == 1 ? ';' : "; ");
            }
            for (var column = 0; column < columns; column++)
            {
                if (column > 0)
                {
                    builder.Append(format == 1 ? ',' : ", ");
                }
                var value = argument.Values[(row * columns) + column];
                builder.Append(FormatArrayValue(value, strict: format == 1));
                if (builder.Length > MaximumTextLength)
                {
                    return InvalidValue();
                }
            }
        }
        if (format == 1)
        {
            builder.Append('}');
        }
        return Text(builder.ToString());
    }

    private static (int Rows, int Columns) Shape(FormulaFunctionArgument argument)
    {
        if (argument.ArrayValue is { } array)
        {
            return (array.RowCount, array.ColumnCount);
        }
        if (argument.SourceDependency is { } source)
        {
            return (source.Range.RowCount, source.Range.ColumnCount);
        }
        return (1, 1);
    }

    private static string FormatArrayValue(CellValue value, bool strict) =>
        value.Kind switch
        {
            CellValueKind.Text => strict
                ? string.Concat("\"", ((string)value.RawValue!).Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
                : (string)value.RawValue!,
            CellValueKind.Boolean => (bool)value.RawValue! ? "TRUE" : "FALSE",
            CellValueKind.Blank => strict ? "\"\"" : string.Empty,
            CellValueKind.Error => value.RawValue?.ToString() ?? "#VALUE!",
            _ => FormulaValueCoercion.ToText(value),
        };

    private static FormulaEvaluationResult EvaluateBahtText(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarNumber(invocation.Arguments[0], out var number, out var error))
        {
            return error;
        }
        if (Math.Abs(number) > 999_999_999_999_999d)
        {
            return NumericError();
        }
        var rounded = Math.Round(Math.Abs(number), 2, MidpointRounding.AwayFromZero);
        var whole = (long)Math.Floor(rounded);
        var satang = (int)Math.Round((rounded - whole) * 100d, MidpointRounding.AwayFromZero);
        if (satang == 100)
        {
            whole++;
            satang = 0;
        }
        var builder = new StringBuilder();
        if (number < 0d)
        {
            builder.Append("ลบ");
        }
        builder.Append(ThaiIntegerWords(whole));
        builder.Append("บาท");
        if (satang == 0)
        {
            builder.Append("ถ้วน");
        }
        else
        {
            builder.Append(ThaiIntegerWords(satang));
            builder.Append("สตางค์");
        }
        return Text(builder.ToString());
    }

    private static string ThaiIntegerWords(long value)
    {
        if (value == 0)
        {
            return "ศูนย์";
        }
        if (value >= 1_000_000)
        {
            var high = value / 1_000_000;
            var low = value % 1_000_000;
            return string.Concat(ThaiIntegerWords(high), "ล้าน", low == 0 ? string.Empty : ThaiUnderMillion(low));
        }
        return ThaiUnderMillion(value);
    }

    private static string ThaiUnderMillion(long value)
    {
        string[] digits = ["", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];
        string[] positions = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน"];
        var builder = new StringBuilder();
        for (var position = 5; position >= 0; position--)
        {
            var divisor = (long)Math.Pow(10, position);
            var digit = (int)((value / divisor) % 10);
            if (digit == 0) continue;
            if (position == 1)
            {
                if (digit == 2) builder.Append("ยี่");
                else if (digit != 1) builder.Append(digits[digit]);
                builder.Append("สิบ");
            }
            else if (position == 0 && digit == 1 && value > 10)
            {
                builder.Append("เอ็ด");
            }
            else
            {
                builder.Append(digits[digit]);
                builder.Append(positions[position]);
            }
        }
        return builder.ToString();
    }

    private static FormulaEvaluationResult EvaluateConcatenate(FormulaFunctionInvocation invocation)
    {
        var builder = new StringBuilder();
        foreach (var argument in invocation.Arguments)
        {
            foreach (var value in argument.Values)
            {
                if (value.Kind == CellValueKind.Error)
                {
                    return FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
                }
                builder.Append(FormulaValueCoercion.ToText(value));
                if (builder.Length > MaximumTextLength) return InvalidValue();
            }
        }
        return Text(builder.ToString());
    }

    private static FormulaEvaluationResult EvaluateDollar(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarNumber(invocation.Arguments[0], out var number, out var error)) return error;
        var decimals = 2;
        if (invocation.Arguments.Count == 2 && !TryScalarInteger(invocation.Arguments[1], out decimals, out error)) return error;
        if (decimals is < -15 or > 15) return NumericError();
        var rounded = RoundDecimalPlaces(number, decimals);
        var body = Math.Abs(rounded).ToString(decimals > 0 ? "N" + decimals.ToString(CultureInfo.InvariantCulture) : "N0", CultureInfo.InvariantCulture);
        return Text(rounded < 0d ? string.Concat("($", body, ")") : string.Concat("$", body));
    }

    private static FormulaEvaluationResult EvaluateFixed(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarNumber(invocation.Arguments[0], out var number, out var error)) return error;
        var decimals = 2;
        if (invocation.Arguments.Count >= 2 && !TryScalarInteger(invocation.Arguments[1], out decimals, out error)) return error;
        var noCommas = false;
        if (invocation.Arguments.Count == 3 && !TryScalarBoolean(invocation.Arguments[2], out noCommas, out error)) return error;
        if (decimals is < -15 or > 15) return NumericError();
        var rounded = RoundDecimalPlaces(number, decimals);
        var format = decimals > 0 ? (noCommas ? "F" : "N") + decimals.ToString(CultureInfo.InvariantCulture) : noCommas ? "F0" : "N0";
        return Text(rounded.ToString(format, CultureInfo.InvariantCulture));
    }

    private static double RoundDecimalPlaces(double number, int decimals)
    {
        if (decimals >= 0) return Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        var factor = Math.Pow(10d, -decimals);
        return Math.Round(number / factor, 0, MidpointRounding.AwayFromZero) * factor;
    }

    private static FormulaEvaluationResult EvaluateLenB(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error)) return error;
        return Number(DbcsLength(text));
    }

    private static FormulaEvaluationResult EvaluateLeftRightB(FormulaFunctionInvocation invocation, bool left)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error)) return error;
        var count = 1;
        if (invocation.Arguments.Count == 2 && (!TryScalarInteger(invocation.Arguments[1], out count, out error) || count < 0)) return InvalidValue();
        return Text(SliceDbcs(text, count, left));
    }

    private static FormulaEvaluationResult EvaluateMidB(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error) ||
            !TryScalarInteger(invocation.Arguments[1], out var start, out error) ||
            !TryScalarInteger(invocation.Arguments[2], out var count, out error)) return error;
        if (start <= 0 || count < 0) return InvalidValue();
        return Text(SliceDbcs(text, start - 1, count));
    }

    private static FormulaEvaluationResult EvaluateReplaceB(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var source, out var error) ||
            !TryScalarInteger(invocation.Arguments[1], out var start, out error) ||
            !TryScalarInteger(invocation.Arguments[2], out var count, out error) ||
            !TryScalarText(invocation.Arguments[3], out var replacement, out error)) return error;
        if (start <= 0 || count < 0) return InvalidValue();
        var before = SliceDbcs(source, 0, start - 1);
        var consumed = DbcsLength(before);
        var removed = SliceDbcs(source, consumed, count);
        var after = SliceDbcs(source, consumed + DbcsLength(removed), int.MaxValue);
        return Text(string.Concat(before, replacement, after));
    }

    private static FormulaEvaluationResult EvaluateFindB(FormulaFunctionInvocation invocation, bool caseSensitive)
    {
        if (!TryScalarText(invocation.Arguments[0], out var find, out var error) ||
            !TryScalarText(invocation.Arguments[1], out var within, out error)) return error;
        var startByte = 1;
        if (invocation.Arguments.Count == 3 && !TryScalarInteger(invocation.Arguments[2], out startByte, out error)) return error;
        if (startByte <= 0 || startByte > DbcsLength(within) + 1) return InvalidValue();
        var startChar = SliceDbcs(within, 0, startByte - 1).Length;
        var index = caseSensitive ? within.IndexOf(find, startChar, StringComparison.Ordinal) : FindWildcardInsensitive(within, find, startChar);
        if (index < 0) return InvalidValue();
        return Number(DbcsLength(within[..index]) + 1d);
    }

    private static int FindWildcardInsensitive(string within, string pattern, int start)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?') && !pattern.Contains('~'))
            return within.IndexOf(pattern, start, StringComparison.OrdinalIgnoreCase);
        var regexPattern = new StringBuilder();
        var escaping = false;
        foreach (var ch in pattern)
        {
            if (escaping) { regexPattern.Append(Regex.Escape(ch.ToString())); escaping = false; }
            else if (ch == '~') escaping = true;
            else if (ch == '*') regexPattern.Append(".*?");
            else if (ch == '?') regexPattern.Append('.');
            else regexPattern.Append(Regex.Escape(ch.ToString()));
        }
        if (escaping) regexPattern.Append(Regex.Escape("~"));
        try
        {
            var match = Regex.Match(within[start..], regexPattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
            return match.Success ? start + match.Index : -1;
        }
        catch (RegexMatchTimeoutException) { return -1; }
    }

    private static int DbcsLength(string text) => text.EnumerateRunes().Sum(static rune => rune.Value <= 0x7F ? 1 : 2);

    private static string SliceDbcs(string text, int count, bool left)
    {
        if (count <= 0) return string.Empty;
        var total = DbcsLength(text);
        return left ? SliceDbcs(text, 0, count) : SliceDbcs(text, Math.Max(0, total - count), count);
    }

    private static string SliceDbcs(string text, int startBytes, int countBytes)
    {
        if (countBytes <= 0) return string.Empty;
        var builder = new StringBuilder();
        var offset = 0;
        var end = countBytes == int.MaxValue ? int.MaxValue : checked(startBytes + countBytes);
        foreach (var rune in text.EnumerateRunes())
        {
            var width = rune.Value <= 0x7F ? 1 : 2;
            var next = offset + width;
            if (next <= startBytes) { offset = next; continue; }
            if (offset < startBytes || next > end) { if (next > end) break; offset = next; continue; }
            builder.Append(rune.ToString());
            offset = next;
        }
        return builder.ToString();
    }

    private static FormulaEvaluationResult EvaluateRegexExtract(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error) || !TryScalarText(invocation.Arguments[1], out var pattern, out error)) return error;
        var returnMode = 0;
        if (invocation.Arguments.Count >= 3 && !TryScalarInteger(invocation.Arguments[2], out returnMode, out error)) return error;
        var caseInsensitive = false;
        if (invocation.Arguments.Count == 4 && !TryScalarBoolean(invocation.Arguments[3], out caseInsensitive, out error)) return error;
        if (returnMode != 0) return InvalidValue();
        try
        {
            var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None), RegexTimeout);
            return match.Success ? Text(match.Value) : NotAvailable();
        }
        catch (ArgumentException) { return InvalidValue(); }
        catch (RegexMatchTimeoutException) { return NumericError(); }
    }

    private static FormulaEvaluationResult EvaluateRegexReplace(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error) || !TryScalarText(invocation.Arguments[1], out var pattern, out error) || !TryScalarText(invocation.Arguments[2], out var replacement, out error)) return error;
        var occurrence = 0;
        if (invocation.Arguments.Count >= 4 && !TryScalarInteger(invocation.Arguments[3], out occurrence, out error)) return error;
        var caseInsensitive = false;
        if (invocation.Arguments.Count == 5 && !TryScalarBoolean(invocation.Arguments[4], out caseInsensitive, out error)) return error;
        if (occurrence < 0) return InvalidValue();
        try
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None), RegexTimeout);
            if (occurrence == 0) return Text(regex.Replace(text, replacement));
            var matches = regex.Matches(text);
            if (occurrence > matches.Count) return Text(text);
            var match = matches[occurrence - 1];
            return Text(string.Concat(text.AsSpan(0, match.Index), match.Result(replacement), text.AsSpan(match.Index + match.Length)));
        }
        catch (ArgumentException) { return InvalidValue(); }
        catch (RegexMatchTimeoutException) { return NumericError(); }
    }

    private static FormulaEvaluationResult EvaluateRegexTest(FormulaFunctionInvocation invocation)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error) || !TryScalarText(invocation.Arguments[1], out var pattern, out error)) return error;
        var caseInsensitive = false;
        if (invocation.Arguments.Count == 3 && !TryScalarBoolean(invocation.Arguments[2], out caseInsensitive, out error)) return error;
        try { return Boolean(Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None), RegexTimeout)); }
        catch (ArgumentException) { return InvalidValue(); }
        catch (RegexMatchTimeoutException) { return NumericError(); }
    }

    private static FormulaEvaluationResult EvaluateTextRelative(FormulaFunctionInvocation invocation, bool after)
    {
        if (!TryScalarText(invocation.Arguments[0], out var text, out var error) || !TryScalarText(invocation.Arguments[1], out var delimiter, out error)) return error;
        if (delimiter.Length == 0) return InvalidValue();
        var instance = 1;
        if (invocation.Arguments.Count >= 3 && !TryScalarInteger(invocation.Arguments[2], out instance, out error)) return error;
        if (instance == 0) return NumericError();
        var matchMode = 0;
        if (invocation.Arguments.Count >= 4 && !TryScalarInteger(invocation.Arguments[3], out matchMode, out error)) return error;
        if (matchMode is not (0 or 1)) return NumericError();
        var matchEnd = false;
        if (invocation.Arguments.Count >= 5 && !TryScalarBoolean(invocation.Arguments[4], out matchEnd, out error)) return error;
        string? ifNotFound = null;
        if (invocation.Arguments.Count == 6 && !TryScalarText(invocation.Arguments[5], out ifNotFound, out error)) return error;
        var comparison = matchMode == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var index = FindInstance(text, delimiter, instance, comparison);
        if (index < 0 && matchEnd) { index = instance > 0 ? text.Length : 0; delimiter = string.Empty; }
        if (index < 0) return ifNotFound is null ? NotAvailable() : Text(ifNotFound);
        return after ? Text(text[(index + delimiter.Length)..]) : Text(text[..index]);
    }

    private static int FindInstance(string text, string delimiter, int instance, StringComparison comparison)
    {
        if (instance > 0)
        {
            var start = 0;
            for (var occurrence = 0; occurrence < instance; occurrence++)
            {
                var found = text.IndexOf(delimiter, start, comparison);
                if (found < 0) return -1;
                if (occurrence == instance - 1) return found;
                start = found + delimiter.Length;
            }
            return -1;
        }
        var end = text.Length;
        for (var occurrence = 0; occurrence < -instance; occurrence++)
        {
            var found = text.LastIndexOf(delimiter, end - 1, end, comparison);
            if (found < 0) return -1;
            if (occurrence == -instance - 1) return found;
            end = found;
        }
        return -1;
    }

    private static bool TryScalarText(FormulaFunctionArgument argument, out string text, out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar) { text = string.Empty; error = InvalidValue(); return false; }
        text = FormulaValueCoercion.ToText(argument.ScalarValue);
        if (text.Length > MaximumTextLength) { error = InvalidValue(); return false; }
        error = default!;
        return true;
    }

    private static bool TryScalarNumber(FormulaFunctionArgument argument, out double value, out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar || !FormulaValueCoercion.TryNumber(argument.ScalarValue, out value, allowText: true) || !double.IsFinite(value))
        { value = default; error = InvalidValue(); return false; }
        error = default!; return true;
    }

    private static bool TryScalarInteger(FormulaFunctionArgument argument, out int value, out FormulaEvaluationResult error)
    {
        if (!TryScalarNumber(argument, out var number, out error) || number < int.MinValue || number > int.MaxValue)
        { value = default; error = NumericError(); return false; }
        value = checked((int)Math.Truncate(number)); return true;
    }

    private static bool TryScalarBoolean(FormulaFunctionArgument argument, out bool value, out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar || !FormulaValueCoercion.TryBoolean(argument.ScalarValue, out value))
        { value = default; error = InvalidValue(); return false; }
        error = default!; return true;
    }

    private static FormulaEvaluationResult Text(string value) => value.Length <= MaximumTextLength ? FormulaEvaluationResult.Success(CellValue.FromText(value)) : InvalidValue();
    private static FormulaEvaluationResult Number(double value) => double.IsFinite(value) ? FormulaEvaluationResult.Success(CellValue.FromNumber(value)) : NumericError();
    private static FormulaEvaluationResult Boolean(bool value) => FormulaEvaluationResult.Success(CellValue.FromBoolean(value));
    private static FormulaEvaluationResult InvalidValue() => FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
    private static FormulaEvaluationResult NotAvailable() => FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);
    private static FormulaEvaluationResult NumericError() => new(CellValue.FromError("#NUM!"), FormulaErrorCode.InvalidValue, Array.Empty<FormulaDependency>());
}
