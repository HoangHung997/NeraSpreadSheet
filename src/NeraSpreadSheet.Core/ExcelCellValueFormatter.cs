using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeraSpreadSheet.Core;

/// <summary>
/// Formats workbook values with Excel-compatible number-format semantics.
/// The formatter is platform neutral so every rendering backend displays the
/// same text for the same cell value and style.
/// </summary>
public static partial class ExcelCellValueFormatter
{
    public static string Format(
        CellValue value,
        string? formatCode,
        ExcelDateSystem dateSystem = ExcelDateSystem.Date1900,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var code = string.IsNullOrWhiteSpace(formatCode)
            ? "General"
            : formatCode.Trim();

        if (value.IsBlank)
        {
            return string.Empty;
        }

        if (value.Kind == CellValueKind.Text)
        {
            var text = Convert.ToString(value.RawValue, culture) ?? string.Empty;
            var sections = SplitSections(code);
            return sections.Count >= 4
                ? RenderTextSection(sections[3], text)
                : text;
        }

        if (value.Kind is CellValueKind.Boolean or CellValueKind.Error)
        {
            return value.ToString();
        }

        try
        {
            var numeric = value.Kind == CellValueKind.DateTime
                ? ToSerial((DateTime)value.RawValue!, dateSystem)
                : (double)value.RawValue!;
            var section = SelectNumericSection(code, numeric, out var magnitude);
            var normalized = RemoveDirectives(section);
            if (string.Equals(normalized, "General", StringComparison.OrdinalIgnoreCase))
            {
                return value.Kind == CellValueKind.DateTime
                    ? ((DateTime)value.RawValue!).ToString(culture)
                    : numeric.ToString("G15", culture);
            }

            if (IsDateTimeFormat(normalized))
            {
                return FormatDateTime(numeric, normalized, dateSystem, culture);
            }

            if (TryFormatFraction(magnitude, normalized, culture, out var fraction))
            {
                return fraction;
            }

            var dotNetFormat = NormalizeNumericFormat(normalized);
            return magnitude.ToString(dotNetFormat, culture);
        }
        catch (ArgumentException)
        {
            return value.ToString();
        }
        catch (FormatException)
        {
            return value.ToString();
        }
        catch (OverflowException)
        {
            return value.ToString();
        }
    }

    private static string SelectNumericSection(
        string formatCode,
        double value,
        out double magnitude)
    {
        var sections = SplitSections(formatCode);
        if (sections.Count == 0)
        {
            magnitude = value;
            return "General";
        }

        var conditionalSections = sections
            .Take(Math.Min(3, sections.Count))
            .Select(static section => new
            {
                Section = section,
                Condition = TryReadCondition(section),
            })
            .ToArray();
        if (conditionalSections.Any(static item => item.Condition is not null))
        {
            foreach (var item in conditionalSections)
            {
                if (item.Condition is null || item.Condition.Value.Matches(value))
                {
                    magnitude = value;
                    return item.Section;
                }
            }

            magnitude = value;
            return string.Empty;
        }

        if (value > 0d || sections.Count == 1)
        {
            magnitude = value;
            return sections[0];
        }

        if (value < 0d)
        {
            if (sections.Count >= 2)
            {
                magnitude = Math.Abs(value);
                return sections[1];
            }

            magnitude = value;
            return sections[0];
        }

        magnitude = 0d;
        return sections.Count >= 3 ? sections[2] : sections[0];
    }

    private static List<string> SplitSections(string formatCode)
    {
        var sections = new List<string>(4);
        var start = 0;
        var quoted = false;
        var escaped = false;
        for (var index = 0; index < formatCode.Length; index++)
        {
            var character = formatCode[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == ';' && !quoted)
            {
                sections.Add(formatCode[start..index]);
                start = index + 1;
            }
        }
        sections.Add(formatCode[start..]);
        return sections;
    }

    private static string RenderTextSection(string section, string text)
    {
        var result = new StringBuilder(section.Length + text.Length);
        var quoted = false;
        for (var index = 0; index < section.Length; index++)
        {
            var character = section[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == '\\' && index + 1 < section.Length)
            {
                result.Append(section[++index]);
            }
            else if (character == '@' && !quoted)
            {
                result.Append(text);
            }
            else if (character != '_' && character != '*')
            {
                result.Append(character);
            }
            else if (index + 1 < section.Length)
            {
                index++;
                if (character == '_')
                {
                    result.Append(' ');
                }
            }
        }
        return result.ToString();
    }

    private static string RemoveDirectives(string section) =>
        DirectiveRegex().Replace(section, static match =>
        {
            var directive = match.Value.AsSpan(1, match.Value.Length - 2);
            if (directive.Length == 0 || directive[0] != '$')
            {
                return string.Empty;
            }

            directive = directive[1..];
            var localeSeparator = directive.LastIndexOf('-');
            return localeSeparator < 0
                ? directive.ToString()
                : directive[..localeSeparator].ToString();
        });

    private static NumericCondition? TryReadCondition(string section)
    {
        var match = ConditionRegex().Match(section);
        if (!match.Success ||
            !double.TryParse(
                match.Groups[2].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var operand))
        {
            return null;
        }

        return new NumericCondition(match.Groups[1].Value, operand);
    }

    private static bool IsDateTimeFormat(string formatCode)
    {
        var visible = StripQuotedAndEscaped(formatCode).ToLowerInvariant();
        if (visible.Contains("am/pm", StringComparison.Ordinal) ||
            visible.Contains("a/p", StringComparison.Ordinal) ||
            ElapsedTimeRegex().IsMatch(visible))
        {
            return true;
        }
        return visible.Contains('y') ||
               visible.Contains('d') ||
               visible.Contains('h') ||
               visible.Contains('s') ||
               visible.Contains("m/", StringComparison.Ordinal) ||
               visible.Contains("/m", StringComparison.Ordinal) ||
               visible.Contains("m-", StringComparison.Ordinal) ||
               visible.Contains("-m", StringComparison.Ordinal) ||
               visible.Contains("m:", StringComparison.Ordinal) ||
               visible.Contains(":m", StringComparison.Ordinal);
    }

    private static string FormatDateTime(
        double serial,
        string formatCode,
        ExcelDateSystem dateSystem,
        CultureInfo culture)
    {
        if (ElapsedTimeRegex().IsMatch(formatCode))
        {
            return FormatElapsedTime(serial, formatCode, culture);
        }

        var date = FromSerial(serial, dateSystem);
        var translated = TranslateDateTimeFormat(formatCode);
        return date.ToString(translated, culture);
    }

    private static string TranslateDateTimeFormat(string formatCode)
    {
        var result = new StringBuilder(formatCode.Length + 8);
        var quoted = false;
        var previousToken = '\0';
        for (var index = 0; index < formatCode.Length;)
        {
            var character = formatCode[index];
            if (character == '"')
            {
                quoted = !quoted;
                result.Append(character);
                index++;
                continue;
            }
            if (quoted || character == '\\')
            {
                result.Append(character);
                if (character == '\\' && index + 1 < formatCode.Length)
                {
                    result.Append(formatCode[index + 1]);
                    index += 2;
                }
                else
                {
                    index++;
                }
                continue;
            }
            if (character is '_' or '*')
            {
                if (character == '_')
                {
                    result.Append("' '");
                }
                index += Math.Min(2, formatCode.Length - index);
                continue;
            }
            if (formatCode.AsSpan(index).StartsWith("AM/PM", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("tt");
                index += 5;
                previousToken = 't';
                continue;
            }
            if (formatCode.AsSpan(index).StartsWith("A/P", StringComparison.OrdinalIgnoreCase))
            {
                result.Append('t');
                index += 3;
                previousToken = 't';
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is not ('y' or 'm' or 'd' or 'h' or 's'))
            {
                result.Append(character);
                index++;
                continue;
            }

            var end = index + 1;
            while (end < formatCode.Length &&
                   char.ToLowerInvariant(formatCode[end]) == lower)
            {
                end++;
            }
            var count = end - index;
            var nextSignificant = NextToken(formatCode, end);
            switch (lower)
            {
                case 'y':
                    result.Append(count <= 2 ? "yy" : "yyyy");
                    break;
                case 'd':
                    result.Append(count switch
                    {
                        1 => "d",
                        2 => "dd",
                        3 => "ddd",
                        _ => "dddd",
                    });
                    break;
                case 'h':
                    result.Append(count >= 2 ? "HH" : "H");
                    break;
                case 's':
                    result.Append(count >= 2 ? "ss" : "s");
                    break;
                case 'm':
                    var minutes = previousToken == 'h' ||
                                  nextSignificant == 's' ||
                                  IsAdjacentToColon(formatCode, index, end);
                    result.Append(minutes
                        ? count >= 2 ? "mm" : "m"
                        : count switch
                        {
                            1 => "M",
                            2 => "MM",
                            3 => "MMM",
                            _ => "MMMM",
                        });
                    break;
            }
            previousToken = lower;
            index = end;
        }

        var translated = result.ToString();
        if (translated.Contains("tt", StringComparison.Ordinal))
        {
            translated = translated.Replace("HH", "hh", StringComparison.Ordinal)
                .Replace("H", "h", StringComparison.Ordinal);
        }
        return translated;
    }

    private static char NextToken(string code, int index)
    {
        for (; index < code.Length; index++)
        {
            var candidate = char.ToLowerInvariant(code[index]);
            if (candidate is 'y' or 'm' or 'd' or 'h' or 's')
            {
                return candidate;
            }
        }
        return '\0';
    }

    private static bool IsAdjacentToColon(string code, int start, int end) =>
        (start > 0 && code[start - 1] == ':') ||
        (end < code.Length && code[end] == ':');

    private static string FormatElapsedTime(double serial, string formatCode, CultureInfo culture)
    {
        const double secondsPerDay = 86_400d;
        var totalSeconds = Math.Abs(serial) * secondsPerDay;
        var sign = serial < 0d ? "-" : string.Empty;
        var roundedSeconds = checked((long)Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
        var hours = roundedSeconds / 3600L;
        var minutes = (roundedSeconds / 60L) % 60L;
        var seconds = roundedSeconds % 60L;
        var result = RemoveDirectives(formatCode);
        result = Regex.Replace(result, "\\[h+\\]", hours.ToString(culture), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "\\[m+\\]", (roundedSeconds / 60L).ToString(culture), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "\\[s+\\]", roundedSeconds.ToString(culture), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "mm", minutes.ToString("00", culture), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "ss", seconds.ToString("00", culture), RegexOptions.IgnoreCase);
        return sign + result.Replace("\\:", ":", StringComparison.Ordinal).Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    private static bool TryFormatFraction(
        double value,
        string formatCode,
        CultureInfo culture,
        out string formatted)
    {
        var match = FractionRegex().Match(formatCode);
        if (!match.Success)
        {
            formatted = string.Empty;
            return false;
        }

        var denominatorDigits = match.Groups["denominator"].Length;
        var fixedDenominator = match.Groups["fixed"].Success
            ? int.Parse(match.Groups["fixed"].Value, CultureInfo.InvariantCulture)
            : 0;
        var maxDenominator = fixedDenominator > 0
            ? fixedDenominator
            : Math.Min(9999, (int)Math.Pow(10, denominatorDigits) - 1);
        var whole = Math.Truncate(value);
        var fraction = Math.Abs(value - whole);
        if (fixedDenominator > 0)
        {
            var fixedNumerator = checked((int)Math.Round(fraction * fixedDenominator));
            formatted = FormatFraction(
                value,
                formatCode,
                culture,
                whole,
                fixedNumerator,
                fixedDenominator);
            return true;
        }

        FindBestFraction(fraction, maxDenominator, out var numerator, out var denominator);
        formatted = FormatFraction(
            value,
            formatCode,
            culture,
            whole,
            numerator,
            denominator);
        return true;
    }

    private static string FormatFraction(
        double value,
        string formatCode,
        CultureInfo culture,
        double whole,
        int numerator,
        int denominator)
    {
        if (numerator == denominator)
        {
            whole += Math.Sign(value);
            numerator = 0;
        }
        var sign = value < 0d && whole == 0d ? "-" : string.Empty;
        var wholeText = whole == 0d && !formatCode.Contains('#')
            ? "0"
            : whole == 0d ? string.Empty : whole.ToString("0", culture);
        var fractionText = numerator == 0
            ? string.Empty
            : $"{numerator.ToString(culture)}/{denominator.ToString(culture)}";
        return sign + string.Join(
            " ",
            new[] { wholeText, fractionText }.Where(static part => part.Length > 0));
    }

    private static void FindBestFraction(
        double value,
        int maxDenominator,
        out int numerator,
        out int denominator)
    {
        numerator = 0;
        denominator = 1;
        var bestError = double.MaxValue;
        for (var candidate = 1; candidate <= maxDenominator; candidate++)
        {
            var candidateNumerator = checked((int)Math.Round(value * candidate));
            var error = Math.Abs(value - ((double)candidateNumerator / candidate));
            if (error >= bestError)
            {
                continue;
            }
            bestError = error;
            numerator = candidateNumerator;
            denominator = candidate;
            if (error < 1e-12)
            {
                break;
            }
        }
    }

    private static string NormalizeNumericFormat(string formatCode)
    {
        var result = new StringBuilder(formatCode.Length);
        for (var index = 0; index < formatCode.Length; index++)
        {
            var character = formatCode[index];
            if (character is '_' or '*')
            {
                if (character == '_')
                {
                    result.Append(' ');
                }
                if (index + 1 < formatCode.Length)
                {
                    index++;
                }
                continue;
            }
            result.Append(character);
        }
        return result.ToString();
    }

    private static string StripQuotedAndEscaped(string value)
    {
        var result = new StringBuilder(value.Length);
        var quoted = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == '\\')
            {
                index++;
                continue;
            }
            if (!quoted)
            {
                result.Append(character);
            }
        }
        return result.ToString();
    }

    private static double ToSerial(DateTime dateTime, ExcelDateSystem dateSystem) =>
        dateSystem == ExcelDateSystem.Date1904
            ? (dateTime - new DateTime(1904, 1, 1)).TotalDays
            : dateTime.ToOADate();

    private static DateTime FromSerial(double serial, ExcelDateSystem dateSystem) =>
        dateSystem == ExcelDateSystem.Date1904
            ? new DateTime(1904, 1, 1).AddDays(serial)
            : DateTime.FromOADate(serial);

    [GeneratedRegex(@"\[(?!(?:h+|m+|s+)\])[^\]]+\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"\[(<=|>=|<>|=|<|>)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][-+]?\d+)?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionRegex();

    [GeneratedRegex(@"\[(?:h+|m+|s+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElapsedTimeRegex();

    [GeneratedRegex(@"(?:(?<whole>[#0?]+)\s+)?(?<numerator>[#?]+)/(?:(?<denominator>[#?]+)|(?<fixed>[1-9]\d*))", RegexOptions.CultureInvariant)]
    private static partial Regex FractionRegex();

    private readonly record struct NumericCondition(string Operator, double Operand)
    {
        public bool Matches(double value) => Operator switch
        {
            "<" => value < Operand,
            "<=" => value <= Operand,
            ">" => value > Operand,
            ">=" => value >= Operand,
            "=" => value == Operand,
            "<>" => value != Operand,
            _ => false,
        };
    }
}
