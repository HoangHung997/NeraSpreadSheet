namespace NeraSpreadSheet.Core;

public enum ConditionalFormattingRuleType
{
    CellIs,
    Expression,
}

public enum ConditionalFormattingOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    NotBetween,
}

public sealed record ConditionalFormattingRule
{
    public const int MaxRangesPerRule = 1_024;

    public ConditionalFormattingRule(
        Guid id,
        IEnumerable<CellRange> ranges,
        ConditionalFormattingRuleType type,
        ConditionalFormattingOperator @operator,
        string formula1,
        string? formula2,
        int differentialStyleId,
        int priority,
        bool stopIfTrue = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Conditional-formatting rule IDs cannot be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(ranges);
        var normalizedRanges = ranges
            .Distinct()
            .OrderBy(static range => range.Top)
            .ThenBy(static range => range.Left)
            .ThenBy(static range => range.Bottom)
            .ThenBy(static range => range.Right)
            .ToArray();
        if (normalizedRanges.Length == 0)
        {
            throw new ArgumentException(
                "A conditional-formatting rule must target at least one range.",
                nameof(ranges));
        }

        if (normalizedRanges.Length > MaxRangesPerRule)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ranges),
                $"A conditional-formatting rule cannot target more than " +
                $"{MaxRangesPerRule} ranges.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (!Enum.IsDefined(@operator))
        {
            throw new ArgumentOutOfRangeException(nameof(@operator));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(differentialStyleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priority);

        var normalizedFormula1 = NormalizeFormula(
            formula1,
            nameof(formula1));
        string? normalizedFormula2 = null;
        if (type == ConditionalFormattingRuleType.Expression)
        {
            if (formula2 is not null)
            {
                throw new ArgumentException(
                    "Expression rules accept exactly one formula.",
                    nameof(formula2));
            }

            @operator = ConditionalFormattingOperator.Equal;
        }
        else if (@operator is ConditionalFormattingOperator.Between or
                 ConditionalFormattingOperator.NotBetween)
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

        Id = id;
        Ranges = normalizedRanges;
        Type = type;
        Operator = @operator;
        Formula1 = normalizedFormula1;
        Formula2 = normalizedFormula2;
        DifferentialStyleId = differentialStyleId;
        Priority = priority;
        StopIfTrue = stopIfTrue;
    }

    public Guid Id { get; }

    public IReadOnlyList<CellRange> Ranges { get; }

    public ConditionalFormattingRuleType Type { get; }

    public ConditionalFormattingOperator Operator { get; }

    public string Formula1 { get; }

    public string? Formula2 { get; }

    public int DifferentialStyleId { get; }

    public int Priority { get; }

    public bool StopIfTrue { get; }

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

    internal ConditionalFormattingRule Copy() => new(
        Id,
        Ranges,
        Type,
        Operator,
        Formula1,
        Formula2,
        DifferentialStyleId,
        Priority,
        StopIfTrue);

    internal ConditionalFormattingRule WithMappedState(
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
            DifferentialStyleId,
            Priority,
            StopIfTrue);

    private static string NormalizeFormula(
        string? formula,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            formula,
            parameterName);
        var trimmed = formula.Trim();
        return trimmed.StartsWith('=')
            ? trimmed
            : $"={trimmed}";
    }
}

public sealed class DifferentialStyleCatalog
{
    private readonly List<CellStylePatch> _styles = [];
    private readonly Dictionary<CellStylePatch, int> _ids = [];

    public int Count => _styles.Count;

    public CellStylePatch Get(int styleId)
    {
        if ((uint)styleId >= (uint)_styles.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(styleId));
        }

        return _styles[styleId];
    }

    public int Intern(CellStylePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        Validate(patch);
        if (_ids.TryGetValue(patch, out var existing))
        {
            return existing;
        }

        var id = _styles.Count;
        _styles.Add(patch);
        _ids.Add(patch, id);
        return id;
    }

    public IReadOnlyList<CellStylePatch> Snapshot() =>
        _styles.ToArray();

    internal void Restore(
        IEnumerable<CellStylePatch> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        _styles.Clear();
        _ids.Clear();
        foreach (var style in styles)
        {
            Intern(style);
        }
    }

    private static void Validate(CellStylePatch patch)
    {
        if (patch.IsEmpty)
        {
            throw new ArgumentException(
                "Differential styles cannot be empty.",
                nameof(patch));
        }

        var resolved = patch.Apply(CellStyle.Default);
        if (resolved == CellStyle.Default)
        {
            throw new ArgumentException(
                "Differential styles must change at least one property.",
                nameof(patch));
        }

        _ = new CellStyleCatalog().Intern(resolved);
    }
}

internal sealed class WorksheetConditionalFormattingCollection
{
    public const int MaxRulesPerWorksheet = 100_000;

    private readonly List<ConditionalFormattingRule> _rules = [];

    public int Count => _rules.Count;

    public IReadOnlyList<ConditionalFormattingRule> Rules =>
        _rules
            .OrderBy(static rule => rule.Priority)
            .Select(static rule => rule.Copy())
            .ToArray();

    public void Add(
        ConditionalFormattingRule rule,
        DifferentialStyleCatalog differentialStyles)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(differentialStyles);
        if (_rules.Count >= MaxRulesPerWorksheet)
        {
            throw new InvalidOperationException(
                $"A worksheet cannot contain more than " +
                $"{MaxRulesPerWorksheet} conditional-formatting rules.");
        }

        if (rule.DifferentialStyleId >= differentialStyles.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rule),
                "The rule references a differential style that does not exist.");
        }

        if (_rules.Any(candidate => candidate.Id == rule.Id))
        {
            throw new InvalidOperationException(
                $"A conditional-formatting rule with ID '{rule.Id}' " +
                "already exists.");
        }

        if (_rules.Any(candidate =>
                candidate.Priority == rule.Priority))
        {
            throw new InvalidOperationException(
                $"Conditional-formatting priority {rule.Priority} " +
                "is already used.");
        }

        _rules.Add(rule.Copy());
    }

    public bool Remove(
        Guid id,
        out ConditionalFormattingRule? removed)
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

    public ConditionalFormattingRule[] Capture() =>
        _rules
            .Select(static rule => rule.Copy())
            .ToArray();

    public void Restore(
        IEnumerable<ConditionalFormattingRule> rules,
        DifferentialStyleCatalog differentialStyles)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(differentialStyles);
        _rules.Clear();
        foreach (var rule in rules.OrderBy(static rule => rule.Priority))
        {
            Add(rule, differentialStyles);
        }
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

    public ConditionalFormattingRule[] CreateStructuralRules(
        WorksheetStructuralChange change)
    {
        var mapped = new List<ConditionalFormattingRule>(_rules.Count);
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

            var orderedRanges = OrderRanges(ranges);
            var formula1 =
                FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula1,
                    change);
            var formula2 = rule.Formula2 is null
                ? null
                : FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula2,
                    change);
            mapped.Add(rule.WithMappedState(
                orderedRanges,
                formula1,
                formula2));
        }

        return [.. mapped];
    }

    public ConditionalFormattingRule[] CreateAxisMoveRules(
        WorksheetAxisMove move)
    {
        var mapped = new List<ConditionalFormattingRule>(_rules.Count);
        foreach (var rule in _rules)
        {
            var ranges = new List<CellRange>(rule.Ranges.Count);
            foreach (var range in rule.Ranges)
            {
                if (!move.TryMapUniformRange(
                        range,
                        out var target))
                {
                    throw new InvalidOperationException(
                        "Cannot reorder because a conditional-formatting " +
                        "range is not one uniform translation.");
                }

                ranges.Add(target);
            }

            var orderedRanges = OrderRanges(ranges);
            var formula1 =
                FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula1,
                    move);
            var formula2 = rule.Formula2 is null
                ? null
                : FormulaStructuralReferenceRewriter.RewriteLocal(
                    rule.Formula2,
                    move);
            mapped.Add(rule.WithMappedState(
                orderedRanges,
                formula1,
                formula2));
        }

        return [.. mapped];
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
}
