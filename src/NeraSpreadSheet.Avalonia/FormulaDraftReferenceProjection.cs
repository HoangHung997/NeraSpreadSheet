using System.Text;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Balances an ephemeral preview of an unfinished draft. All reference
/// semantics still come from the shared analyzer. Never evaluates or changes text.</summary>
internal static class FormulaDraftReferenceProjection
{
    private const int MaximumPreviewLength = 32768;
    private const int MaximumDepth = 128;

    public static IReadOnlyList<FormulaDependency> GetReferences(string text, Workbook workbook,
        Worksheet worksheet, CellAddress address, FormulaDependency? provisional)
    {
        if (FormulaReferenceAnalyzer.TryGetReferences(text, workbook, worksheet, address, out var references)) return references;
        if (text.Length <= MaximumPreviewLength)
        {
            if (TryCreatePreview(text, false, out var preview) &&
                FormulaReferenceAnalyzer.TryGetReferences(preview, workbook, worksheet, address, out references)) return AddProvisional(references, provisional, worksheet);
            if (TryCreatePreview(text, true, out preview) &&
                FormulaReferenceAnalyzer.TryGetReferences(preview, workbook, worksheet, address, out references)) return AddProvisional(references, provisional, worksheet);
        }
        return provisional is { } value ? [value] : [];
    }
    private static IReadOnlyList<FormulaDependency> AddProvisional(IReadOnlyList<FormulaDependency> references, FormulaDependency? provisional, Worksheet worksheet)
    {
        if (provisional is not { } value || references.Any(reference => reference.Range == value.Range &&
            string.Equals(reference.WorksheetName ?? worksheet.Name, value.WorksheetName ?? worksheet.Name, StringComparison.OrdinalIgnoreCase))) return references;
        return references.Append(value).ToArray();
    }
    private static bool TryCreatePreview(string text, bool discardLastOperand, out string preview)
    {
        preview = string.Empty;
        if (!text.StartsWith('=')) return false;
        var operandStart = 1; var parentheses = 0; var brackets = 0; var braces = 0; var quote = '\0';
        for (var index = 1; index < text.Length; index++)
        {
            var ch = text[index];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    if (index + 1 < text.Length && text[index + 1] == quote) index++;
                    else quote = '\0';
                }
                continue;
            }
            if (brackets > 0)
            {
                if (ch == '\'' && index + 1 < text.Length) index++;
                else if (ch == '[') brackets++;
                else if (ch == ']') brackets--;
                continue;
            }
            if (ch is '"' or '\'') { quote = ch; continue; }
            if (ch == '[') { brackets++; continue; }
            if (ch == '{') { braces++; continue; }
            if (ch == '}') { if (--braces < 0) return false; continue; }
            if (braces > 0) continue;
            if (ch == '(')
            {
                if (++parentheses > MaximumDepth) return false;
                operandStart = index + 1;
            }
            else if (ch == ')') { if (--parentheses < 0) return false; }
            else if (ch is ',' or ';' or '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>') operandStart = index + 1;
        }
        var length = discardLastOperand || quote != '\0' || brackets != 0 || braces != 0 ? operandStart : text.Length;
        var prefix = text[..length].TrimEnd();
        parentheses = 0; brackets = 0; braces = 0; quote = '\0';
        for (var index = 1; index < prefix.Length; index++)
        {
            var ch = prefix[index];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    if (index + 1 < prefix.Length && prefix[index + 1] == quote) index++;
                    else quote = '\0';
                }
                continue;
            }
            if (brackets > 0)
            {
                if (ch == '\'' && index + 1 < prefix.Length) index++;
                else if (ch == '[') brackets++;
                else if (ch == ']') brackets--;
                continue;
            }
            if (ch is '"' or '\'') { quote = ch; continue; }
            if (ch == '[') { brackets++; continue; }
            if (ch == '{') { braces++; continue; }
            if (ch == '}') { braces--; continue; }
            if (braces > 0) continue;
            if (ch == '(') parentheses++;
            else if (ch == ')') parentheses--;
        }
        if (quote != '\0' || brackets != 0 || braces != 0 || parentheses is < 0 or > MaximumDepth) return false;
        var result = new StringBuilder(prefix);
        if (prefix.Length == 1 || prefix[^1] is '(' or ',' or ';' or '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>') result.Append('0');
        result.Append(')', parentheses);
        preview = result.ToString();
        return !string.Equals(preview, text, StringComparison.Ordinal);
    }
}
