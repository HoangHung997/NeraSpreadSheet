using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public enum FormulaCriterionOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

/// <summary>
/// Immutable, reusable criterion used by conditional aggregate functions.
/// Text matching is ordinal case-insensitive and supports Excel-style '*',
/// '?' and '~' wildcard escaping for equality and inequality criteria.
/// </summary>
public sealed class FormulaCriterion
{
    private FormulaCriterion(
        FormulaCriterionOperator comparisonOperator,
        CellValue operand,
        string? textPattern,
        bool hasWildcards)
    {
        Operator = comparisonOperator;
        Operand = operand;
        TextPattern = textPattern;
        HasWildcards = hasWildcards;
    }

    public FormulaCriterionOperator Operator { get; }

    public CellValue Operand { get; }

    public string? TextPattern { get; }

    public bool HasWildcards { get; }

    public static FormulaCriterion Parse(CellValue criterion)
    {
        if (criterion.Kind != CellValueKind.Text)
        {
            return new FormulaCriterion(
                FormulaCriterionOperator.Equal,
                criterion,
                textPattern: null,
                hasWildcards: false);
        }

        var text = (string)criterion.RawValue!;
        var comparisonOperator = FormulaCriterionOperator.Equal;
        var operandText = text;
        foreach (var candidate in Operators)
        {
            if (!text.StartsWith(
                    candidate.Token,
                    StringComparison.Ordinal))
            {
                continue;
            }
            comparisonOperator = candidate.Operator;
            operandText = text[candidate.Token.Length..];
            break;
        }

        if (operandText.Length == 0)
        {
            return new FormulaCriterion(
                comparisonOperator,
                CellValue.Blank,
                operandText,
                hasWildcards: false);
        }
        if (double.TryParse(
                operandText,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var number) &&
            double.IsFinite(number))
        {
            return new FormulaCriterion(
                comparisonOperator,
                CellValue.FromNumber(number),
                textPattern: null,
                hasWildcards: false);
        }
        if (bool.TryParse(operandText, out var boolean))
        {
            return new FormulaCriterion(
                comparisonOperator,
                CellValue.FromBoolean(boolean),
                textPattern: null,
                hasWildcards: false);
        }
        if (DateTime.TryParse(
                operandText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
        {
            return new FormulaCriterion(
                comparisonOperator,
                CellValue.FromDateTime(dateTime),
                textPattern: null,
                hasWildcards: false);
        }

        return new FormulaCriterion(
            comparisonOperator,
            CellValue.FromText(operandText),
            operandText,
            HasUnescapedWildcard(operandText));
    }

    public bool Matches(CellValue candidate)
    {
        if (HasWildcards &&
            Operator is FormulaCriterionOperator.Equal or
                FormulaCriterionOperator.NotEqual)
        {
            var matched = candidate.Kind switch
            {
                CellValueKind.Blank => WildcardMatch(string.Empty, TextPattern!),
                CellValueKind.Text => WildcardMatch(
                    (string)candidate.RawValue!,
                    TextPattern!),
                _ => WildcardMatch(candidate.ToString(), TextPattern!),
            };
            return Operator == FormulaCriterionOperator.Equal
                ? matched
                : !matched;
        }

        var comparison = Compare(candidate, Operand);
        if (comparison is null)
        {
            return Operator == FormulaCriterionOperator.NotEqual;
        }
        return Operator switch
        {
            FormulaCriterionOperator.Equal => comparison.Value == 0,
            FormulaCriterionOperator.NotEqual => comparison.Value != 0,
            FormulaCriterionOperator.LessThan => comparison.Value < 0,
            FormulaCriterionOperator.LessThanOrEqual => comparison.Value <= 0,
            FormulaCriterionOperator.GreaterThan => comparison.Value > 0,
            FormulaCriterionOperator.GreaterThanOrEqual => comparison.Value >= 0,
            _ => throw new InvalidOperationException(
                "Unknown formula criterion operator."),
        };
    }

    private static int? Compare(CellValue candidate, CellValue operand)
    {
        if (operand.IsBlank)
        {
            return candidate.IsBlank
                ? 0
                : 1;
        }
        if (candidate.Kind == CellValueKind.Error ||
            operand.Kind == CellValueKind.Error)
        {
            if (candidate.Kind != CellValueKind.Error ||
                operand.Kind != CellValueKind.Error)
            {
                return null;
            }
            return string.Compare(
                Convert.ToString(
                    candidate.RawValue,
                    CultureInfo.InvariantCulture),
                Convert.ToString(
                    operand.RawValue,
                    CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
        }
        if (operand.Kind == CellValueKind.Number)
        {
            if (!TryRangeNumber(candidate, out var candidateNumber))
            {
                return null;
            }
            return candidateNumber.CompareTo((double)operand.RawValue!);
        }
        if (operand.Kind == CellValueKind.DateTime)
        {
            if (!TryRangeNumber(candidate, out var candidateNumber))
            {
                return null;
            }
            return candidateNumber.CompareTo(
                ((DateTime)operand.RawValue!).ToOADate());
        }
        if (operand.Kind == CellValueKind.Boolean)
        {
            return candidate.Kind == CellValueKind.Boolean
                ? ((bool)candidate.RawValue!).CompareTo(
                    (bool)operand.RawValue!)
                : null;
        }
        if (operand.Kind == CellValueKind.Text)
        {
            var candidateText = candidate.Kind switch
            {
                CellValueKind.Blank => string.Empty,
                CellValueKind.Text => (string)candidate.RawValue!,
                _ => candidate.ToString(),
            };
            return string.Compare(
                candidateText,
                (string)operand.RawValue!,
                StringComparison.OrdinalIgnoreCase);
        }
        return null;
    }

    private static bool TryRangeNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.DateTime:
                number = ((DateTime)value.RawValue!).ToOADate();
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static bool HasUnescapedWildcard(string pattern)
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

    private static bool WildcardMatch(string input, string pattern)
    {
        var tokens = TokenizePattern(pattern);
        var inputIndex = 0;
        var tokenIndex = 0;
        var starTokenIndex = -1;
        var starInputIndex = -1;
        while (inputIndex < input.Length)
        {
            if (tokenIndex < tokens.Count &&
                tokens[tokenIndex].Kind == PatternTokenKind.Single)
            {
                tokenIndex++;
                inputIndex++;
                continue;
            }
            if (tokenIndex < tokens.Count &&
                tokens[tokenIndex].Kind == PatternTokenKind.Literal &&
                CharactersEqual(
                    input[inputIndex],
                    tokens[tokenIndex].Character))
            {
                tokenIndex++;
                inputIndex++;
                continue;
            }
            if (tokenIndex < tokens.Count &&
                tokens[tokenIndex].Kind == PatternTokenKind.Many)
            {
                starTokenIndex = tokenIndex++;
                starInputIndex = inputIndex;
                continue;
            }
            if (starTokenIndex >= 0)
            {
                tokenIndex = starTokenIndex + 1;
                inputIndex = ++starInputIndex;
                continue;
            }
            return false;
        }

        while (tokenIndex < tokens.Count &&
               tokens[tokenIndex].Kind == PatternTokenKind.Many)
        {
            tokenIndex++;
        }
        return tokenIndex == tokens.Count;
    }

    private static List<PatternToken> TokenizePattern(string pattern)
    {
        var tokens = new List<PatternToken>(pattern.Length);
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '~' && index + 1 < pattern.Length)
            {
                tokens.Add(new PatternToken(
                    PatternTokenKind.Literal,
                    pattern[++index]));
            }
            else if (character == '*')
            {
                tokens.Add(new PatternToken(PatternTokenKind.Many));
            }
            else if (character == '?')
            {
                tokens.Add(new PatternToken(PatternTokenKind.Single));
            }
            else
            {
                tokens.Add(new PatternToken(
                    PatternTokenKind.Literal,
                    character));
            }
        }
        return tokens;
    }

    private static bool CharactersEqual(char left, char right) =>
        char.ToUpperInvariant(left) == char.ToUpperInvariant(right);

    private static readonly (
        string Token,
        FormulaCriterionOperator Operator)[] Operators =
    [
        ("<=", FormulaCriterionOperator.LessThanOrEqual),
        (">=", FormulaCriterionOperator.GreaterThanOrEqual),
        ("<>", FormulaCriterionOperator.NotEqual),
        ("=", FormulaCriterionOperator.Equal),
        ("<", FormulaCriterionOperator.LessThan),
        (">", FormulaCriterionOperator.GreaterThan),
    ];

    private enum PatternTokenKind
    {
        Literal,
        Single,
        Many,
    }

    private readonly record struct PatternToken(
        PatternTokenKind Kind,
        char Character = default);
}
