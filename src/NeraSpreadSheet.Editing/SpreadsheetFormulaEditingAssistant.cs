using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing;

public sealed record FormulaFunctionSuggestion(
    string Name,
    int MinimumArguments,
    int MaximumArguments,
    string Signature,
    string Description)
{
    public string DisplayText => Signature;
}

public sealed record FormulaFunctionArgumentHelp(
    string Name,
    string Description,
    bool IsOptional = false,
    bool IsRepeating = false);

public sealed record FormulaFunctionHelp(
    string Name,
    string Signature,
    string Description,
    IReadOnlyList<FormulaFunctionArgumentHelp> Arguments);

public sealed record FormulaFunctionHelpContext(
    FormulaFunctionHelp Function,
    int ActiveArgumentIndex)
{
    public FormulaFunctionArgumentHelp? ActiveArgument =>
        Function.Arguments.Count == 0
            ? null
            : Function.Arguments[Math.Min(
                ActiveArgumentIndex,
                Function.Arguments.Count - 1)];
}

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
    private readonly Dictionary<string, FormulaFunctionHelp> _helpByName;

    public SpreadsheetFormulaEditingAssistant(
        IEnumerable<FormulaFunctionDescriptor>? descriptors = null)
    {
        var includeEngineOwnedFunctions = descriptors is null;
        descriptors ??= new BuiltInFormulaFunctionRegistry().Descriptors;
        var selected = descriptors
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
            .Select(static item => new CatalogEntry(
                item.Name,
                item.Descriptor.MinimumArguments,
                item.Descriptor.MaximumArguments))
            .ToList();
        if (includeEngineOwnedFunctions)
        {
            var existing = selected
                .Select(static item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            selected.AddRange(FormulaFunctionHelpCatalog.EngineOwnedFunctions
                .Where(item => existing.Add(item.Name)));
        }
        _helpByName = selected.ToDictionary(
            static item => item.Name,
            static item => FormulaFunctionHelpCatalog.Create(
                item.Name,
                item.MinimumArguments,
                item.MaximumArguments),
            StringComparer.OrdinalIgnoreCase);
        _catalog = selected
            .Select(item => new FormulaFunctionSuggestion(
                item.Name,
                item.MinimumArguments,
                item.MaximumArguments,
                _helpByName[item.Name].Signature,
                _helpByName[item.Name].Description))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Gets help for every registered formula name and alias.
    /// </summary>
    public IReadOnlyList<FormulaFunctionHelp> FunctionHelp =>
        _catalog.Select(item => _helpByName[item.Name]).ToArray();

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
    /// Returns the innermost function invocation and logical argument at the
    /// caret. Strings, quoted sheet names, array constants and structured
    /// references do not create false argument separators.
    /// </summary>
    public FormulaFunctionHelpContext? GetFunctionHelp(
        string text,
        int caretIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateCaret(text, caretIndex);
        if (!text.StartsWith('='))
        {
            return null;
        }

        var stack = new Stack<InvocationFrame>();
        var inString = false;
        var inSheetName = false;
        var bracketDepth = 0;
        var braceDepth = 0;
        for (var index = 1; index < caretIndex; index++)
        {
            var character = text[index];
            if (inString)
            {
                if (character == '"')
                {
                    if (index + 1 < caretIndex && text[index + 1] == '"')
                    {
                        index++;
                    }
                    else
                    {
                        inString = false;
                    }
                }
                continue;
            }
            if (inSheetName)
            {
                if (character == '\'')
                {
                    if (index + 1 < caretIndex && text[index + 1] == '\'')
                    {
                        index++;
                    }
                    else
                    {
                        inSheetName = false;
                    }
                }
                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '\'':
                    inSheetName = true;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth = Math.Max(0, braceDepth - 1);
                    break;
                case '(' when bracketDepth == 0 && braceDepth == 0:
                    stack.Push(new InvocationFrame(
                        FindFunctionNameBefore(text, index),
                        0));
                    break;
                case ')' when bracketDepth == 0 && braceDepth == 0:
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                    }
                    break;
                case ',' or ';' when bracketDepth == 0 && braceDepth == 0:
                    if (stack.TryPop(out var frame))
                    {
                        stack.Push(frame with
                        {
                            ArgumentIndex = checked(frame.ArgumentIndex + 1),
                        });
                    }
                    break;
            }
        }

        foreach (var frame in stack)
        {
            if (frame.FunctionName is { } name &&
                _helpByName.TryGetValue(name, out var help))
            {
                return new FormulaFunctionHelpContext(
                    help,
                    frame.ArgumentIndex);
            }
        }
        return null;
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

    private static string? FindFunctionNameBefore(string text, int parenthesis)
    {
        var end = parenthesis;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }
        var start = FindIdentifierStart(text, end);
        return start == end ? null : text[start..end];
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

    private readonly record struct InvocationFrame(
        string? FunctionName,
        int ArgumentIndex);

    internal readonly record struct CatalogEntry(
        string Name,
        int MinimumArguments,
        int MaximumArguments);
}
