using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Resolves conditional-formatting patches against an immutable worksheet
/// snapshot. Matching rules are evaluated in ascending priority order; the
/// resulting patches are applied from low to high precedence so higher
/// priority properties win while non-conflicting properties compose.
/// </summary>
public static class ConditionalFormattingEvaluator
{
    private static readonly NeraFormulaEngine FormulaEngine = new();

    public static CellStyle ResolveStyle(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellStyle baseStyle)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(baseStyle);

        var matched = new List<CellStylePatch>();
        foreach (var rule in
                 worksheet.EnumerateConditionalFormattingRules(address))
        {
            if (!Matches(
                    worksheet,
                    address,
                    rule))
            {
                continue;
            }

            matched.Add(
                worksheet.GetDifferentialStyle(
                    rule.DifferentialStyleId));
            if (rule.StopIfTrue)
            {
                break;
            }
        }

        var resolved = baseStyle;
        for (var index = matched.Count - 1;
             index >= 0;
             index--)
        {
            resolved = matched[index].Apply(resolved);
        }

        return resolved;
    }

    private static bool Matches(
        WorksheetSnapshot worksheet,
        CellAddress address,
        ConditionalFormattingRule rule) =>
        rule.Type switch
        {
            ConditionalFormattingRuleType.Expression =>
                MatchesExpression(
                    worksheet,
                    address,
                    rule),
            ConditionalFormattingRuleType.CellIs =>
                MatchesCellIs(
                    worksheet,
                    address,
                    rule),
            _ => false,
        };

    private static bool MatchesExpression(
        WorksheetSnapshot worksheet,
        CellAddress address,
        ConditionalFormattingRule rule)
    {
        var result = EvaluateFormula(
            worksheet,
            address,
            rule.Anchor,
            rule.Formula1);
        return result.IsSuccess &&
               TryBoolean(
                   result.Value,
                   out var value) &&
               value;
    }

    private static bool MatchesCellIs(
        WorksheetSnapshot worksheet,
        CellAddress address,
        ConditionalFormattingRule rule)
    {
        var current = worksheet.GetCell(address).Value;
        if (current.Kind == CellValueKind.Error)
        {
            return false;
        }

        var first = EvaluateFormula(
            worksheet,
            address,
            rule.Anchor,
            rule.Formula1);
        if (!first.IsSuccess)
        {
            return false;
        }

        var firstComparison = Compare(
            current,
            first.Value);
        if (firstComparison is null)
        {
            return false;
        }

        return rule.Operator switch
        {
            ConditionalFormattingOperator.Equal =>
                firstComparison == 0,
            ConditionalFormattingOperator.NotEqual =>
                firstComparison != 0,
            ConditionalFormattingOperator.GreaterThan =>
                firstComparison > 0,
            ConditionalFormattingOperator.GreaterThanOrEqual =>
                firstComparison >= 0,
            ConditionalFormattingOperator.LessThan =>
                firstComparison < 0,
            ConditionalFormattingOperator.LessThanOrEqual =>
                firstComparison <= 0,
            ConditionalFormattingOperator.Between =>
                MatchesBetween(
                    worksheet,
                    address,
                    rule,
                    current,
                    first.Value,
                    negate: false),
            ConditionalFormattingOperator.NotBetween =>
                MatchesBetween(
                    worksheet,
                    address,
                    rule,
                    current,
                    first.Value,
                    negate: true),
            _ => false,
        };
    }

    private static bool MatchesBetween(
        WorksheetSnapshot worksheet,
        CellAddress address,
        ConditionalFormattingRule rule,
        CellValue current,
        CellValue first,
        bool negate)
    {
        if (rule.Formula2 is null)
        {
            return false;
        }

        var second = EvaluateFormula(
            worksheet,
            address,
            rule.Anchor,
            rule.Formula2);
        if (!second.IsSuccess)
        {
            return false;
        }

        var compareFirst = Compare(
            current,
            first);
        var compareSecond = Compare(
            current,
            second.Value);
        if (compareFirst is null ||
            compareSecond is null)
        {
            return false;
        }

        var lowerToUpper = Compare(
            first,
            second.Value);
        if (lowerToUpper is null)
        {
            return false;
        }

        var between = lowerToUpper <= 0
            ? compareFirst >= 0 && compareSecond <= 0
            : compareSecond >= 0 && compareFirst <= 0;
        return negate ? !between : between;
    }

    private static FormulaEvaluationResult EvaluateFormula(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellAddress anchor,
        string formula)
    {
        var translated =
            A1FormulaReferenceTranslator.Translate(
                formula,
                anchor,
                address);
        if (translated.Contains(
                "#REF!",
                StringComparison.OrdinalIgnoreCase))
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidReference);
        }

        return FormulaEngine.Evaluate(
            translated,
            new SnapshotEvaluationContext(worksheet));
    }

    private static int? Compare(
        CellValue left,
        CellValue right)
    {
        if (left.Kind == CellValueKind.Error ||
            right.Kind == CellValueKind.Error)
        {
            return null;
        }

        if (TryNumber(left, out var leftNumber) &&
            TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (left.Kind == CellValueKind.DateTime &&
            right.Kind == CellValueKind.DateTime)
        {
            return ((DateTime)left.RawValue!)
                .CompareTo((DateTime)right.RawValue!);
        }

        if (left.Kind == CellValueKind.Boolean &&
            right.Kind == CellValueKind.Boolean)
        {
            return ((bool)left.RawValue!)
                .CompareTo((bool)right.RawValue!);
        }

        return string.Compare(
            left.ToString(),
            right.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue!
                    ? 1d
                    : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            case CellValueKind.Text:
                return double.TryParse(
                    Convert.ToString(
                        value.RawValue,
                        CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number);
            default:
                number = 0d;
                return false;
        }
    }

    private static bool TryBoolean(
        CellValue value,
        out bool result)
    {
        switch (value.Kind)
        {
            case CellValueKind.Boolean:
                result = (bool)value.RawValue!;
                return true;
            case CellValueKind.Number:
                result = Math.Abs(
                    (double)value.RawValue!) >
                    double.Epsilon;
                return true;
            case CellValueKind.Blank:
                result = false;
                return true;
            case CellValueKind.Text:
                return bool.TryParse(
                    Convert.ToString(
                        value.RawValue,
                        CultureInfo.InvariantCulture),
                    out result);
            default:
                result = false;
                return false;
        }
    }

    private sealed class SnapshotEvaluationContext
        : IFormulaEvaluationContext
    {
        private readonly WorksheetSnapshot _worksheet;

        public SnapshotEvaluationContext(
            WorksheetSnapshot worksheet)
        {
            _worksheet = worksheet;
        }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address)
        {
            if (worksheetName is not null &&
                !string.Equals(
                    worksheetName,
                    _worksheet.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CellValue.FromError("#REF!");
            }

            return _worksheet.GetCell(address).Value;
        }
    }
}
