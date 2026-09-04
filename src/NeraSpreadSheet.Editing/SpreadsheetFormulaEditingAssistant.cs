using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing;

public sealed record FormulaFunctionSuggestion(
    string Name,
    int MinimumArguments,
    int MaximumArguments);

public readonly record struct FormulaTextSpan(int Start, int Length)
{
    public int End => checked(Start + Length);
}

public sealed record FormulaTextEditResult(
    string Text,
    int CaretIndex,
    FormulaTextSpan InsertedSpan);

/// <summary>
/// Provides host-neutral formula completion and point-mode reference insertion.
/// Native hosts remain responsible for presenting suggestions and mapping a
/// pointer drag to a <see cref="CellRange"/>.
/// </summary>
public sealed class SpreadsheetFormulaEditingAssistant
{
    private readonly FormulaFunctionSuggestion[] _catalog;

    public SpreadsheetFormulaEditingAssistant(
        IEnumerable<FormulaFunctionDescriptor>? descriptors = null)
    {
        descriptors ??= new BuiltInFormulaFunctionRegistry().Descriptors;
        _catalog = descriptors
            .SelectMany(static descriptor =>
                descriptor.EnumerateFormulaNames().Select(name => new
                {
                    Name = name,
                    Descriptor = descriptor,
                }))
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static item => item.Descriptor.Version)
                .First())
            .Select(static item => new FormulaFunctionSuggestion(
                item.Name,
                item.Descriptor.MinimumArguments,
                item.Descriptor.MaximumArguments))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns function names matching the identifier immediately before the
    /// caret. Suggestions are produced only for formula text beginning with '='.
    /// </summary>
    public IReadOnlyList<FormulaFunctionSuggestion> GetSuggestions(
        string text,
        int caretIndex,
        int maximumResults = 12)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        ValidateCaret(text, caretIndex);
        if (!text.StartsWith('=') || caretIndex <= 1)
        {
            return Array.Empty<FormulaFunctionSuggestion>();
        }

        var start = FindIdentifierStart(text, caretIndex);
        if (start == caretIndex ||
            (start > 0 && text[start - 1] == '!'))
        {
            return Array.Empty<FormulaFunctionSuggestion>();
        }

        var prefix = text[start..caretIndex];
        return _catalog
            .Where(item => item.Name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            .Take(maximumResults)
            .ToArray();
    }

    /// <summary>
    /// Replaces the identifier before the caret with a selected function name
    /// and appends an opening parenthesis when one is not already present.
    /// </summary>
    public static FormulaTextEditResult ApplySuggestion(
        string text,
        int caretIndex,
        FormulaFunctionSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(suggestion);
        ValidateCaret(text, caretIndex);
        var start = FindIdentifierStart(text, caretIndex);
        var suffixStartsWithParenthesis =
            caretIndex < text.Length && text[caretIndex] == '(';
        var replacement = suffixStartsWithParenthesis
            ? suggestion.Name
            : $"{suggestion.Name}(";
        var result = string.Concat(
            text.AsSpan(0, start),
            replacement,
            text.AsSpan(caretIndex));
        var inserted = new FormulaTextSpan(start, replacement.Length);
        return new FormulaTextEditResult(
            result,
            inserted.End,
            inserted);
    }

    /// <summary>
    /// Inserts an A1 reference at the caret or replaces a provisional reference
    /// previously inserted by point mode.
    /// </summary>
    public static FormulaTextEditResult InsertReference(
        string text,
        int caretIndex,
        CellRange range,
        string? worksheetName = null,
        FormulaTextSpan? provisionalSpan = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateCaret(text, caretIndex);
        if (!text.StartsWith('='))
        {
            throw new ArgumentException(
                "Point-mode references require formula text beginning with '='.",
                nameof(text));
        }

        var span = provisionalSpan ?? new FormulaTextSpan(caretIndex, 0);
        ValidateSpan(text, span);
        var reference = FormatReference(range, worksheetName);
        var result = string.Concat(
            text.AsSpan(0, span.Start),
            reference,
            text.AsSpan(span.End));
        var inserted = new FormulaTextSpan(span.Start, reference.Length);
        return new FormulaTextEditResult(
            result,
            inserted.End,
            inserted);
    }

    private static string FormatReference(
        CellRange range,
        string? worksheetName)
    {
        var address = range.TopLeft == range.BottomRight
            ? range.TopLeft.ToA1()
            : $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";
        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            return address;
        }

        var escaped = worksheetName.Replace("'", "''", StringComparison.Ordinal);
        return $"'{escaped}'!{address}";
    }

    private static int FindIdentifierStart(string text, int caretIndex)
    {
        var start = caretIndex;
        while (start > 0 && IsFunctionNameCharacter(text[start - 1]))
        {
            start--;
        }
        return start;
    }

    private static bool IsFunctionNameCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '.' or '_';

    private static void ValidateCaret(string text, int caretIndex)
    {
        if (caretIndex < 0 || caretIndex > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(caretIndex));
        }
    }

    private static void ValidateSpan(string text, FormulaTextSpan span)
    {
        if (span.Start < 0 || span.Length < 0 || span.End > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(span));
        }
    }
}
