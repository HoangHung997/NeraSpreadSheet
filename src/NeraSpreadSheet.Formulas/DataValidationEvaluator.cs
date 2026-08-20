using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed record DataValidationEvaluationResult(
    bool IsValid,
    DataValidationRule? Rule,
    DataValidationErrorStyle ErrorStyle,
    string? ErrorTitle,
    string? ErrorMessage)
{
    public bool HasRule => Rule is not null;

    public static DataValidationEvaluationResult Valid(
        DataValidationRule? rule = null) =>
        new(
            true,
            rule,
            rule?.ErrorStyle ?? DataValidationErrorStyle.Stop,
            null,
            null);

    public static DataValidationEvaluationResult Invalid(
        DataValidationRule rule) =>
        new(
            false,
            rule,
            rule.ErrorStyle,
            rule.ShowErrorMessage ? rule.ErrorTitle : null,
            rule.ShowErrorMessage
                ? rule.Error ?? "The value is not valid for this cell."
                : null);
}

public static class DataValidationEvaluator
{
    private static readonly NeraFormulaEngine FormulaEngine = new();

    public static DataValidationEvaluationResult Evaluate(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!worksheet.TryGetDataValidationRule(address, out var rule) ||
            rule is null)
        {
            return DataValidationEvaluationResult.Valid();
        }

        if (candidate.Kind == CellValueKind.Blank)
        {
            return rule.AllowBlank
                ? DataValidationEvaluationResult.Valid(rule)
                : DataValidationEvaluationResult.Invalid(rule);
        }

        var isValid = rule.Type switch
        {
            DataValidationType.Whole =>
                MatchesWhole(worksheet, address, candidate, rule),
            DataValidationType.Decimal =>
                MatchesDecimal(worksheet, address, candidate, rule),
            DataValidationType.Date =>
                MatchesDate(worksheet, address, candidate, rule),
            DataValidationType.Time =>
                MatchesTime(worksheet, address, candidate, rule),
            DataValidationType.TextLength =>
                MatchesTextLength(worksheet, address, candidate, rule),
            DataValidationType.List =>
                MatchesList(worksheet, address, candidate, rule),
            DataValidationType.Custom =>
                MatchesCustom(worksheet, address, candidate, rule),
            _ => false,
        };

        return isValid
            ? DataValidationEvaluationResult.Valid(rule)
            : DataValidationEvaluationResult.Invalid(rule);
    }

    private static bool MatchesWhole(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (!TryNumber(candidate, out var value) ||
            Math.Truncate(value) != value)
        {
            return false;
        }

        return MatchesOperator(
            worksheet,
            address,
            candidate,
            rule,
            value,
            TryNumber);
    }

    private static bool MatchesDecimal(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (!TryNumber(candidate, out var value))
        {
            return false;
        }

        return MatchesOperator(
            worksheet,
            address,
            candidate,
            rule,
            value,
            TryNumber);
    }

    private static bool MatchesDate(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (!TryDateSerial(candidate, out var value))
        {
            return false;
        }

        return MatchesOperator(
            worksheet,
            address,
            candidate,
            rule,
            value,
            TryDateSerial);
    }

    private static bool MatchesTime(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (!TryTimeSerial(candidate, out var value))
        {
            return false;
        }

        return MatchesOperator(
            worksheet,
            address,
            candidate,
            rule,
            value,
            TryTimeSerial);
    }

    private static bool MatchesTextLength(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (candidate.Kind == CellValueKind.Error)
        {
            return false;
        }

        var value = candidate.ToString().Length;
        return MatchesOperator(
            worksheet,
            address,
            candidate,
            rule,
            value,
            TryNumber);
    }

    private static bool MatchesList(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        if (candidate.Kind == CellValueKind.Error)
        {
            return false;
        }

        var candidateText = candidate.ToString();
        if (!TryResolveListValues(
                worksheet,
                address,
                candidate,
                rule,
                out var values))
        {
            return false;
        }

        return values.Any(value => string.Equals(
            value,
            candidateText,
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCustom(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule)
    {
        var result = EvaluateFormula(
            worksheet,
            address,
            candidate,
            rule.Anchor,
            rule.Formula1);
        return result.IsSuccess &&
               TryBoolean(result.Value, out var value) &&
               value;
    }

    private static bool MatchesOperator(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule,
        double value,
        TryConvertDelegate converter)
    {
        if (rule.Operator is not { } @operator)
        {
            return false;
        }

        var first = EvaluateFormula(
            worksheet,
            address,
            candidate,
            rule.Anchor,
            rule.Formula1);
        if (!first.IsSuccess ||
            !converter(first.Value, out var firstValue))
        {
            return false;
        }

        if (@operator is DataValidationOperator.Between or
            DataValidationOperator.NotBetween)
        {
            if (rule.Formula2 is null)
            {
                return false;
            }

            var second = EvaluateFormula(
                worksheet,
                address,
                candidate,
                rule.Anchor,
                rule.Formula2);
            if (!second.IsSuccess ||
                !converter(second.Value, out var secondValue))
            {
                return false;
            }

            var minimum = Math.Min(firstValue, secondValue);
            var maximum = Math.Max(firstValue, secondValue);
            var between = value >= minimum && value <= maximum;
            return @operator == DataValidationOperator.Between
                ? between
                : !between;
        }

        return @operator switch
        {
            DataValidationOperator.Equal => value == firstValue,
            DataValidationOperator.NotEqual => value != firstValue,
            DataValidationOperator.GreaterThan => value > firstValue,
            DataValidationOperator.LessThan => value < firstValue,
            DataValidationOperator.GreaterThanOrEqual => value >= firstValue,
            DataValidationOperator.LessThanOrEqual => value <= firstValue,
            _ => false,
        };
    }

    private static bool TryResolveListValues(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        DataValidationRule rule,
        out string[] values)
    {
        var translated = A1FormulaReferenceTranslator.Translate(
            rule.Formula1,
            rule.Anchor,
            address);
        if (TryParseLiteralList(translated, out values))
        {
            return true;
        }

        if (TryParseRangeReference(
                translated,
                worksheet.Name,
                out var range))
        {
            var materialized = new List<string>();
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    var sourceAddress = new CellAddress(row, column);
                    var value = sourceAddress == address
                        ? candidate
                        : worksheet.GetCell(sourceAddress).Value;
                    if (!value.IsBlank && value.Kind != CellValueKind.Error)
                    {
                        materialized.Add(value.ToString());
                    }
                }
            }

            values = materialized.ToArray();
            return true;
        }

        var result = FormulaEngine.Evaluate(
            translated,
            new SnapshotEvaluationContext(
                worksheet,
                address,
                candidate));
        if (!result.IsSuccess || result.Value.Kind == CellValueKind.Error)
        {
            values = [];
            return false;
        }

        values = result.Value.ToString()
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(static value => value.Length > 0)
            .ToArray();
        return values.Length > 0;
    }

    private static bool TryParseLiteralList(
        string formula,
        out string[] values)
    {
        var expression = formula.StartsWith('=')
            ? formula[1..].Trim()
            : formula.Trim();
        if (expression.Length < 2 ||
            expression[0] != '"' ||
            expression[^1] != '"')
        {
            values = [];
            return false;
        }

        var decoded = new StringBuilder(expression.Length - 2);
        for (var index = 1; index < expression.Length - 1; index++)
        {
            if (expression[index] == '"' &&
                index + 1 < expression.Length - 1 &&
                expression[index + 1] == '"')
            {
                decoded.Append('"');
                index++;
                continue;
            }

            decoded.Append(expression[index]);
        }

        values = decoded.ToString()
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(static value => value.Length > 0)
            .ToArray();
        return values.Length > 0;
    }

    private static bool TryParseRangeReference(
        string formula,
        string worksheetName,
        out CellRange range)
    {
        var expression = formula.StartsWith('=')
            ? formula[1..].Trim()
            : formula.Trim();
        var bang = FindBangOutsideQuotes(expression);
        if (bang >= 0)
        {
            var qualifier = expression[..bang].Trim();
            var parsedName = qualifier.Length >= 2 &&
                             qualifier[0] == '\'' &&
                             qualifier[^1] == '\''
                ? qualifier[1..^1].Replace("''", "'", StringComparison.Ordinal)
                : qualifier;
            if (!string.Equals(
                    parsedName,
                    worksheetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                range = default;
                return false;
            }

            expression = expression[(bang + 1)..];
        }

        var separator = expression.IndexOf(':');
        var firstText = separator < 0
            ? expression
            : expression[..separator];
        var secondText = separator < 0
            ? expression
            : expression[(separator + 1)..];
        if (!CellAddress.TryParseA1(firstText, out var first) ||
            !CellAddress.TryParseA1(secondText, out var second))
        {
            range = default;
            return false;
        }

        range = new CellRange(first, second);
        return true;
    }

    private static int FindBangOutsideQuotes(string expression)
    {
        var inQuote = false;
        for (var index = 0; index < expression.Length; index++)
        {
            if (expression[index] == '\'')
            {
                if (inQuote &&
                    index + 1 < expression.Length &&
                    expression[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                inQuote = !inQuote;
                continue;
            }

            if (!inQuote && expression[index] == '!')
            {
                return index;
            }
        }

        return -1;
    }

    private static FormulaEvaluationResult EvaluateFormula(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellValue candidate,
        CellAddress anchor,
        string formula)
    {
        var translated = A1FormulaReferenceTranslator.Translate(
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
            new SnapshotEvaluationContext(
                worksheet,
                address,
                candidate));
    }

    private static bool TryNumber(CellValue value, out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return double.IsFinite(number);
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            case CellValueKind.Text:
                return double.TryParse(
                    Convert.ToString(
                        value.RawValue,
                        CultureInfo.InvariantCulture),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out number) &&
                    double.IsFinite(number);
            default:
                number = 0d;
                return false;
        }
    }

    private static bool TryDateSerial(CellValue value, out double serial)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            serial = ((DateTime)value.RawValue!).ToOADate();
            return double.IsFinite(serial);
        }

        return TryNumber(value, out serial);
    }

    private static bool TryTimeSerial(CellValue value, out double serial)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            serial = ((DateTime)value.RawValue!).TimeOfDay.TotalDays;
            return true;
        }

        if (!TryNumber(value, out serial))
        {
            return false;
        }

        return serial >= 0d && serial < 1d;
    }

    private static bool TryBoolean(CellValue value, out bool result)
    {
        switch (value.Kind)
        {
            case CellValueKind.Boolean:
                result = (bool)value.RawValue!;
                return true;
            case CellValueKind.Number:
                result = Math.Abs((double)value.RawValue!) > double.Epsilon;
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

    private delegate bool TryConvertDelegate(
        CellValue value,
        out double converted);

    private sealed class SnapshotEvaluationContext
        : IFormulaEvaluationContext
    {
        private readonly WorksheetSnapshot _worksheet;
        private readonly CellAddress _candidateAddress;
        private readonly CellValue _candidateValue;

        public SnapshotEvaluationContext(
            WorksheetSnapshot worksheet,
            CellAddress candidateAddress,
            CellValue candidateValue)
        {
            _worksheet = worksheet;
            _candidateAddress = candidateAddress;
            _candidateValue = candidateValue;
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

            return address == _candidateAddress
                ? _candidateValue
                : _worksheet.GetCell(address).Value;
        }
    }
}
