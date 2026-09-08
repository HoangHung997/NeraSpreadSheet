using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Source spans for direct A1 references. This is a lexical locator,
/// not a formula parser: each candidate is validated by the shared analyzer.</summary>
internal sealed record EditableFormulaReference(FormulaTextSpan Span, string Prefix, string? WorksheetName,
    CellRange Range, bool FirstColumnAbsolute, bool FirstRowAbsolute, bool LastColumnAbsolute, bool LastRowAbsolute, bool HasRange)
{
    public string Format(CellRange range) => Prefix + FormatAddress(range.TopLeft, FirstColumnAbsolute, FirstRowAbsolute) +
        (HasRange || range.TopLeft != range.BottomRight ? ":" + FormatAddress(range.BottomRight, LastColumnAbsolute, LastRowAbsolute) : string.Empty);
    private static string FormatAddress(CellAddress address, bool columnAbsolute, bool rowAbsolute)
    {
        var value = address.ToA1(); var split = 0;
        while (split < value.Length && char.IsAsciiLetter(value[split])) split++;
        return (columnAbsolute ? "$" : string.Empty) + value[..split] + (rowAbsolute ? "$" : string.Empty) + value[split..];
    }
    public static IReadOnlyList<EditableFormulaReference> Locate(string text)
    {
        var result = new List<EditableFormulaReference>();
        if (!text.StartsWith('=')) return result;
        for (var index = 1; index < text.Length;)
        {
            if (text[index] == '"') { index = SkipQuote(text, index, '"'); continue; }
            if (text[index] == '[') { index = SkipBrackets(text, index); continue; }
            if (index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '.' or '!' or ':')) { index++; continue; }
            var start = index; var cellStart = index;
            if (text[index] == '\'')
            {
                var end = SkipQuote(text, index, '\'');
                if (end >= text.Length || text[end] != '!') { index = end; continue; }
                cellStart = end + 1;
            }
            else
            {
                var end = index;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '.')) end++;
                if (end < text.Length && text[end] == '!') cellStart = end + 1;
            }
            if (!ReadCell(text, cellStart, out var endCell, out var first, out var firstColumnAbsolute, out var firstRowAbsolute)) { index++; continue; }
            var last = first; var lastColumnAbsolute = firstColumnAbsolute; var lastRowAbsolute = firstRowAbsolute;
            var rangeEnd = endCell; var colon = endCell;
            while (colon < text.Length && char.IsWhiteSpace(text[colon])) colon++;
            var hasRange = colon < text.Length && text[colon] == ':';
            if (hasRange)
            {
                var next = colon + 1; while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                if (!ReadCell(text, next, out rangeEnd, out last, out lastColumnAbsolute, out lastRowAbsolute)) { index = next; continue; }
            }
            var after = rangeEnd; while (after < text.Length && char.IsWhiteSpace(text[after])) after++;
            if (rangeEnd < text.Length && (char.IsLetterOrDigit(text[rangeEnd]) || text[rangeEnd] is '_' or '.' or '[' or '!') || after < text.Length && text[after] == '(')
            { index = rangeEnd; continue; }
            var token = text[start..rangeEnd];
            if (FormulaReferenceAnalyzer.TryGetReferences("=" + token, out var references) && references.Count == 1)
            {
                var reference = references[0];
                // Reversed ranges remain displayable but are not normalized by a drag.
                if (first == reference.Range.TopLeft && last == reference.Range.BottomRight)
                    result.Add(new(new FormulaTextSpan(start, rangeEnd - start), text[start..cellStart], reference.WorksheetName,
                        reference.Range, firstColumnAbsolute, firstRowAbsolute, lastColumnAbsolute, lastRowAbsolute, hasRange));
            }
            index = Math.Max(index + 1, rangeEnd);
        }
        return result;
    }
    private static bool ReadCell(string text, int start, out int end, out CellAddress address, out bool columnAbsolute, out bool rowAbsolute)
    {
        end = start; address = default; columnAbsolute = false; rowAbsolute = false;
        if (end < text.Length && text[end] == '$') { columnAbsolute = true; end++; }
        var letters = end;
        while (end < text.Length && char.IsAsciiLetter(text[end])) end++;
        if (end == letters || end - letters > 3) return false;
        if (end < text.Length && text[end] == '$') { rowAbsolute = true; end++; }
        var digits = end;
        while (end < text.Length && char.IsAsciiDigit(text[end])) end++;
        if (digits == end) return false;
        return CellAddress.TryParseA1(text[start..end], out address);
    }
    private static int SkipQuote(string text, int start, char quote)
    {
        for (var index = start + 1; index < text.Length; index++)
            if (text[index] == quote)
            {
                if (index + 1 < text.Length && text[index + 1] == quote) index++;
                else return index + 1;
            }
        return text.Length;
    }
    private static int SkipBrackets(string text, int start)
    {
        var depth = 0;
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] == '\'' && index + 1 < text.Length) { index++; continue; }
            if (text[index] == '[') depth++;
            else if (text[index] == ']' && --depth == 0) return index + 1;
        }
        return text.Length;
    }
}
