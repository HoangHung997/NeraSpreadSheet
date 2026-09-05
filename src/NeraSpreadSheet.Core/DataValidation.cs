namespace NeraSpreadSheet.Core;

public enum DataValidationType
{
    Whole,
    Decimal,
    List,
    Date,
    Time,
    TextLength,
    Custom,
}

public enum DataValidationOperator
{
    Between,
    NotBetween,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
}

public enum DataValidationErrorStyle
{
    Stop,
    Warning,
    Information,
}

public sealed record DataValidationRule
{
    public const int MaxRangesPerRule = 1_024;
    public const int MaxFormulaLength = 8_192;
    public const int MaxTitleLength = 32;
    public const int MaxMessageLength = 255;

    public DataValidationRule(
        Guid id,
        IEnumerable<CellRange> ranges,
        DataValidationType type,
        DataValidationOperator? @operator,
        string formula1,
        string? formula2 = null,
        bool allowBlank = true,
        bool showInputMessage = false,
        string? promptTitle = null,
        string? prompt = null,
        bool showErrorMessage = true,
        DataValidationErrorStyle errorStyle = DataValidationErrorStyle.Stop,
        string? errorTitle = null,
        string? error = null,
        bool showDropDown = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Data-validation rule IDs cannot be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(ranges);
        var normalizedRanges = OrderRanges(ranges);
        if (normalizedRanges.Length == 0)
        {
            throw new ArgumentException(
                "A data-validation rule must target at least one range.",
                nameof(ranges));
        }

        if (normalizedRanges.Length > MaxRangesPerRule)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ranges),
                $"A data-validation rule cannot target more than " +
                $"{MaxRangesPerRule} ranges.");
        }

        EnsureRangesDoNotOverlap(normalizedRanges, nameof(ranges));
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (@operator is not null && !Enum.IsDefined(@operator.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(@operator));
        }

        if (!Enum.IsDefined(errorStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(errorStyle));
        }

        var normalizedFormula1 = NormalizeFormula(
            formula1,
            nameof(formula1));
        string? normalizedFormula2 = null;
        if (type is DataValidationType.List or DataValidationType.Custom)
        {
            if (@operator is not null)
            {
                throw new ArgumentException(
                    "List and custom validation rules do not accept an operator.",
                    nameof(@operator));
            }

            if (formula2 is not null)
            {
                throw new ArgumentException(
                    "List and custom validation rules accept exactly one formula.",
                    nameof(formula2));
            }
        }
        else
        {
            if (@operator is null)
            {
                throw new ArgumentException(
                    "Numeric, date, time and text-length rules require an operator.",
                    nameof(@operator));
            }

            if (@operator is DataValidationOperator.Between or
                DataValidationOperator.NotBetween)
            {
                normalizedFormula2 = NormalizeFormula(
                    formula2,
                    nameof(formula2));
            }
            else if (formula2 is not null)
            {
                throw new ArgumentException(
                    "Only Between and NotBetween rules accept a second formula.",
                    nameof(formula2));
            }
        }

        Id = id;
        Ranges = normalizedRanges;
        Type = type;
        Operator = @operator;
        Formula1 = normalizedFormula1;
        Formula2 = normalizedFormula2;
        AllowBlank = allowBlank;
        ShowInputMessage = showInputMessage;
        PromptTitle = NormalizeText(
            promptTitle,
            MaxTitleLength,
            nameof(promptTitle));
        Prompt = NormalizeText(
            prompt,
            MaxMessageLength,
            nameof(prompt));
        ShowErrorMessage = showErrorMessage;
        ErrorStyle = errorStyle;
        ErrorTitle = NormalizeText(
            errorTitle,
            MaxTitleLength,
            nameof(errorTitle));
        Error = NormalizeText(
            error,
            MaxMessageLength,
            nameof(error));
        ShowDropDown = showDropDown;
    }

    public Guid Id { get; }

    public IReadOnlyList<CellRange> Ranges { get; }

    public DataValidationType Type { get; }

    public DataValidationOperator? Operator { get; }

    public string Formula1 { get; }

    public string? Formula2 { get; }

    public bool AllowBlank { get; }

    public bool ShowInputMessage { get; }

    public string? PromptTitle { get; }

    public string? Prompt { get; }

    public bool ShowErrorMessage { get; }

    public DataValidationErrorStyle ErrorStyle { get; }

    public string? ErrorTitle { get; }

    public string? Error { get; }

    /// <summary>
    /// True when the list dropdown arrow should be visible. SpreadsheetML's
    /// showDropDown attribute has the inverse meaning and is translated by the
    /// OpenXml adapter.
    /// </summary>
    public bool ShowDropDown { get; }

    public CellAddress Anchor => Ranges[0].TopLeft;

    public bool AppliesTo(CellAddress address)
    {
        foreach (var range in Ranges)
        {
            if (range.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    internal DataValidationRule Copy() => new(
        Id,
        Ranges,
        Type,
        Operator,
        Formula1,
        Formula2,
        AllowBlank,
        ShowInputMessage,
        PromptTitle,
        Prompt,
        ShowErrorMessage,
        ErrorStyle,
        ErrorTitle,
        Error,
        ShowDropDown);

    internal DataValidationRule WithMappedState(
        IEnumerable<CellRange> ranges,
        string formula1,
        string? formula2) =>
        new(
            Id,
            ranges,
            Type,
            Operator,
            formula1,
            formula2,
            AllowBlank,
            ShowInputMessage,
            PromptTitle,
            Prompt,
            ShowErrorMessage,
            ErrorStyle,
            ErrorTitle,
            Error,
            ShowDropDown);

    private static CellRange[] OrderRanges(
        IEnumerable<CellRange> ranges) =>
        ranges
            .Distinct()
            .OrderBy(static range => range.Top)
            .ThenBy(static range => range.Left)
            .ThenBy(static range => range.Bottom)
            .ThenBy(static range => range.Right)
            .ToArray();

    private static void EnsureRangesDoNotOverlap(
        CellRange[] ranges,
        string parameterName)
    {
        for (var left = 0; left < ranges.Length; left++)
        {
            for (var right = left + 1; right < ranges.Length; right++)
            {
                if (ranges[left].Intersects(ranges[right]))
                {
                    throw new ArgumentException(
                        "Ranges within one data-validation rule cannot overlap.",
                        parameterName);
                }
            }
        }
    }

    private static string NormalizeFormula(
        string? formula,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            formula,
            parameterName);
        var trimmed = formula.Trim();
        var normalized = trimmed.StartsWith('=')
            ? trimmed
            : $"={trimmed}";
        if (normalized.Length > MaxFormulaLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Validation formulas cannot exceed {MaxFormulaLength} characters.");
        }

        if (ContainsInvalidReferenceOutsideString(normalized))
        {
            throw new ArgumentException(
                "Data-validation formulas cannot contain #REF!.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Text cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static bool ContainsInvalidReferenceOutsideString(string formula)
    {
        var inString = false;
        for (var index = 0; index < formula.Length; index++)
        {
            if (formula[index] == '"')
            {
                if (inString &&
                    index + 1 < formula.Length &&
                    formula[index + 1] == '"')
                {
                    index++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString &&
                index + 5 <= formula.Length &&
                formula.AsSpan(index, 5)
                    .Equals("#REF!", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class WorksheetDataValidationCollection
{
    public const int MaxRulesPerWorksheet = 100_000;

    private readonly List<DataValidationRule> _rules = [];

    public int Count => _rules.Count;

    public IReadOnlyList<DataValidationRule> Rules =>
        _rules.Select(static rule => rule.Copy()).ToArray();

    public void Add(DataValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_rules.Count >= MaxRulesPerWorksheet)
        {
            throw new InvalidOperationException(
                $"A worksheet cannot contain more than " +
                $"{MaxRulesPerWorksheet} data-validation rules.");
        }

        if (_rules.Any(candidate => candidate.Id == rule.Id))
        {
            throw new InvalidOperationException(
                $"A data-validation rule with ID '{rule.Id}' already exists.");
        }

        EnsureNoTargetOverlap(_rules, rule);
        _rules.Add(rule.Copy());
    }

    public bool Remove(Guid id, out DataValidationRule? removed)
    {
        var index = _rules.FindIndex(rule => rule.Id == id);
        if (index < 0)
        {
            removed = null;
            return false;
        }

        removed = _rules[index].Copy();
        _rules.RemoveAt(index);
        return true;
    }

    public bool TryGetRule(
        CellAddress address,
        out DataValidationRule? rule)
    {
        foreach (var candidate in _rules)
        {
            if (candidate.AppliesTo(address))
            {
                rule = candidate.Copy();
                return true;
            }
        }

        rule = null;
        return false;
    }

    public DataValidationRule[] Capture() =>
        _rules.Select(static rule => rule.Copy()).ToArray();

    public void Restore(IEnumerable<DataValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var materialized = rules
            .Select(static rule => rule.Copy())
            .ToArray();
        ValidateRuleSet(materialized);
        _rules.Clear();
        _rules.AddRange(materialized);
    }

    public CellRange ExpandSignalRange(CellRange source)
    {
        if (_rules.Count == 0)
        {
            return source;
        }

        var top = source.Top;
        var left = source.Left;
        var bottom = source.Bottom;
        var right = source.Right;
        foreach (var rule in _rules)
        {
            foreach (var range in rule.Ranges)
            {
                top = Math.Min(top, range.Top);
                left = Math.Min(left, range.Left);
                bottom = Math.Max(bottom, range.Bottom);
                right = Math.Max(right, range.Right);
            }
        }

        return new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right));
    }

    public DataValidationRule[] CreateStructuralRules(
        WorksheetStructuralChange change)
    {
        var mapped = new List<DataValidationRule>(_rules.Count);
        foreach (var rule in _rules)
        {
            var ranges = new List<CellRange>(rule.Ranges.Count);
            foreach (var range in rule.Ranges)
            {
                if (change.TryMapRange(range, out var target))
                {
                    ranges.Add(target);
                }
            }

            if (ranges.Count == 0)
            {
                continue;
            }

            var formula1 = FormulaStructuralReferenceRewriter.RewriteLocal(
                rule.Formula1,
                change);
            var formula2 = rule.Formula2 is null
                ? null
                : FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula2,
                    change);
            mapped.Add(rule.WithMappedState(
                OrderRanges(ranges),
                formula1,
                formula2));
        }

        var result = mapped.ToArray();
        ValidateRuleSet(result);
        return result;
    }

    public DataValidationRule[] CreateAxisMoveRules(
        WorksheetAxisMove move)
    {
        var mapped = new List<DataValidationRule>(_rules.Count);
        foreach (var rule in _rules)
        {
            var ranges = new List<CellRange>(rule.Ranges.Count);
            foreach (var range in rule.Ranges)
            {
                if (!move.TryMapUniformRange(range, out var target))
                {
                    throw new InvalidOperationException(
                        "Cannot reorder because a data-validation range is " +
                        "not one uniform translation.");
                }

                ranges.Add(target);
            }

            var formula1 = FormulaStructuralReferenceRewriter.RewriteLocal(
                rule.Formula1,
                move);
            var formula2 = rule.Formula2 is null
                ? null
                : FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula2,
                    move);
            mapped.Add(rule.WithMappedState(
                OrderRanges(ranges),
                formula1,
                formula2));
        }

        var result = mapped.ToArray();
        ValidateRuleSet(result);
        return result;
    }

    private static CellRange[] OrderRanges(
        IEnumerable<CellRange> ranges) =>
        ranges
            .Distinct()
            .OrderBy(static range => range.Top)
            .ThenBy(static range => range.Left)
            .ThenBy(static range => range.Bottom)
            .ThenBy(static range => range.Right)
            .ToArray();

    private static void ValidateRuleSet(DataValidationRule[] rules)
    {
        if (rules.Length > MaxRulesPerWorksheet)
        {
            throw new InvalidOperationException(
                $"A worksheet cannot contain more than " +
                $"{MaxRulesPerWorksheet} data-validation rules.");
        }

        var identifiers = new HashSet<Guid>();
        for (var index = 0; index < rules.Length; index++)
        {
            if (!identifiers.Add(rules[index].Id))
            {
                throw new InvalidOperationException(
                    $"A data-validation rule with ID '{rules[index].Id}' " +
                    "already exists.");
            }

            EnsureNoTargetOverlap(rules.Take(index), rules[index]);
        }
    }

    private static void EnsureNoTargetOverlap(
        IEnumerable<DataValidationRule> existingRules,
        DataValidationRule candidate)
    {
        foreach (var existing in existingRules)
        {
            foreach (var existingRange in existing.Ranges)
            {
                foreach (var candidateRange in candidate.Ranges)
                {
                    if (existingRange.Intersects(candidateRange))
                    {
                        throw new InvalidOperationException(
                            "Data-validation rules cannot target overlapping cells.");
                    }
                }
            }
        }
    }
}
