using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public enum FormulaCriteriaOperator
{
    Equal = 0,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

/// <summary>
/// Immutable criteria predicate shared by conditional aggregate families.
/// Criteria text uses invariant parsing and Excel-style comparison/wildcard
/// prefixes. Text comparison is ordinal and case-insensitive.
/// </summary>
public sealed class FormulaCriteria
{
    public const int MaximumCriteriaLength = 1024;

    private static readonly TimeSpan RegexTimeout =
        TimeSpan.FromMilliseconds(100d);

    private readonly Regex? _wildcardRegex;

    private FormulaCriteria(
        FormulaCriteriaOperator @operator,
        CellValue operand,
        Regex? wildcardRegex)
    {
        Operator = @operator;
        Operand = operand;
        _wildcardRegex = wildcardRegex;
    }

    public FormulaCriteriaOperator Operator { get; }

    public CellValue Operand { get; }

    public bool UsesWildcards => _wildcardRegex is not null;

    public static FormulaCriteria Parse(CellValue criteria)
    {
        if (criteria.Kind != CellValueKind.Text)
        {
            return new FormulaCriteria(
                FormulaCriteriaOperator.Equal,
                criteria,
                null);
        }

        var text = (string)criteria.RawValue!;
        if (text.Length > MaximumCriteriaLength)
        {
            throw new FormatException(
                $"Formula criteria exceeds the limit of " +
                $"{MaximumCriteriaLength:N0} characters.");
        }
        var (comparison, operandText) = ParseOperator(text);
        var hasWildcard =
            (comparison is FormulaCriteriaOperator.Equal or
                FormulaCriteriaOperator.NotEqual) &&
            ContainsUnescapedWildcard(operandText);
        var parsedOperandText = hasWildcard
            ? operandText
            : UnescapeWildcardLiterals(operandText);
        var operand = ParseOperand(parsedOperandText);
        var wildcardRegex = operand.Kind == CellValueKind.Text && hasWildcard
            ? CreateWildcardRegex(operandText)
            : null;
        return new FormulaCriteria(
            comparison,
            operand,
            wildcardRegex);
    }

    public bool Matches(CellValue candidate)
    {
        if (_wildcardRegex is not null)
        {
            var matched = candidate.Kind == CellValueKind.Text &&
                          _wildcardRegex.IsMatch(
                              (string)candidate.RawValue!);
            return Operator == FormulaCriteriaOperator.Equal
                ? matched
                : !matched;
        }

        if (Operand.Kind == CellValueKind.Blank)
        {
            return Operator switch
            {
                FormulaCriteriaOperator.Equal => candidate.IsBlank,
                FormulaCriteriaOperator.NotEqual => !candidate.IsBlank,
                _ => CompareText(candidate, string.Empty, Operator),
            };
        }
        if (Operand.Kind == CellValueKind.Error)
        {
            var equal = candidate.Kind == CellValueKind.Error &&
                        string.Equals(
                            FormulaValueCoercion.ToText(candidate),
                            FormulaValueCoercion.ToText(Operand),
                            StringComparison.OrdinalIgnoreCase);
            return Operator switch
            {
                FormulaCriteriaOperator.Equal => equal,
                FormulaCriteriaOperator.NotEqual => !equal,
                _ => false,
            };
        }
        if (TryComparableNumber(Operand, out var operandNumber))
        {
            if (!TryComparableNumber(candidate, out var candidateNumber))
            {
                return Operator == FormulaCriteriaOperator.NotEqual;
            }
            return Compare(
                candidateNumber.CompareTo(operandNumber),
                Operator);
        }
        if (Operand.Kind == CellValueKind.Boolean)
        {
            if (candidate.Kind != CellValueKind.Boolean)
            {
                return Operator == FormulaCriteriaOperator.NotEqual;
            }
            return Compare(
                ((bool)candidate.RawValue!).CompareTo(
                    (bool)Operand.RawValue!),
                Operator);
        }

        return CompareText(
            candidate,
            FormulaValueCoercion.ToText(Operand),
            Operator);
    }

    private static bool CompareText(
        CellValue candidate,
        string operand,
        FormulaCriteriaOperator @operator)
    {
        if (candidate.Kind != CellValueKind.Text)
        {
            return @operator == FormulaCriteriaOperator.NotEqual;
        }
        var comparison = string.Compare(
            (string)candidate.RawValue!,
            operand,
            StringComparison.OrdinalIgnoreCase);
        return Compare(comparison, @operator);
    }

    private static bool Compare(
        int comparison,
        FormulaCriteriaOperator @operator) =>
        @operator switch
        {
            FormulaCriteriaOperator.Equal => comparison == 0,
            FormulaCriteriaOperator.NotEqual => comparison != 0,
            FormulaCriteriaOperator.LessThan => comparison < 0,
            FormulaCriteriaOperator.LessThanOrEqual => comparison <= 0,
            FormulaCriteriaOperator.GreaterThan => comparison > 0,
            FormulaCriteriaOperator.GreaterThanOrEqual => comparison >= 0,
            _ => false,
        };

    private static bool TryComparableNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.DateTime:
                try
                {
                    number = ((DateTime)value.RawValue!).ToOADate();
                    return double.IsFinite(number);
                }
                catch (OverflowException)
                {
                    break;
                }
        }
        number = default;
        return false;
    }

    private static (FormulaCriteriaOperator Operator, string Operand)
        ParseOperator(string text)
    {
        if (text.StartsWith("<=", StringComparison.Ordinal))
        {
            return (FormulaCriteriaOperator.LessThanOrEqual, text[2..]);
        }
        if (text.StartsWith(">=", StringComparison.Ordinal))
        {
            return (FormulaCriteriaOperator.GreaterThanOrEqual, text[2..]);
        }
        if (text.StartsWith("<>", StringComparison.Ordinal))
        {
            return (FormulaCriteriaOperator.NotEqual, text[2..]);
        }
        if (text.Length > 0 && text[0] == '=')
        {
            return (FormulaCriteriaOperator.Equal, text[1..]);
        }
        if (text.Length > 0 && text[0] == '<')
        {
            return (FormulaCriteriaOperator.LessThan, text[1..]);
        }
        if (text.Length > 0 && text[0] == '>')
        {
            return (FormulaCriteriaOperator.GreaterThan, text[1..]);
        }
        return (FormulaCriteriaOperator.Equal, text);
    }

    private static CellValue ParseOperand(string text)
    {
        if (text.Length == 0)
        {
            return CellValue.Blank;
        }
        if (IsErrorCode(text))
        {
            return CellValue.FromError(text.ToUpperInvariant());
        }
        if (bool.TryParse(text, out var boolean))
        {
            return CellValue.FromBoolean(boolean);
        }
        if (double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var number) &&
            double.IsFinite(number))
        {
            return CellValue.FromNumber(number);
        }
        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.RoundtripKind,
                out var dateTime))
        {
            return CellValue.FromDateTime(dateTime);
        }
        return CellValue.FromText(text);
    }

    private static bool IsErrorCode(string text) =>
        text.Equals("#DIV/0!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#REF!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#NAME?", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#VALUE!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#CIRC!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#N/A", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#NUM!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#SPILL!", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("#CALC!", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsUnescapedWildcard(string pattern)
    {
        var escaped = false;
        foreach (var character in pattern)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '~')
            {
                escaped = true;
                continue;
            }
            if (character is '*' or '?')
            {
                return true;
            }
        }
        return false;
    }

    private static string UnescapeWildcardLiterals(string pattern)
    {
        var builder = new StringBuilder(pattern.Length);
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '~' &&
                index + 1 < pattern.Length &&
                pattern[index + 1] is '*' or '?' or '~')
            {
                builder.Append(pattern[++index]);
                continue;
            }
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static Regex CreateWildcardRegex(string pattern)
    {
        var builder = new StringBuilder("\\A");
        var escaped = false;
        foreach (var character in pattern)
        {
            if (escaped)
            {
                builder.Append(Regex.Escape(character.ToString()));
                escaped = false;
                continue;
            }
            if (character == '~')
            {
                escaped = true;
                continue;
            }
            builder.Append(character switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(character.ToString()),
            });
        }
        if (escaped)
        {
            builder.Append(Regex.Escape("~"));
        }
        builder.Append("\\z");
        return new Regex(
            builder.ToString(),
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.NonBacktracking,
            RegexTimeout);
    }
}
