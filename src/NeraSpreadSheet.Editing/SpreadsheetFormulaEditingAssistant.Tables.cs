using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>A bounded completion item whose Table/column identities are resolved again when applied.</summary>
public sealed record FormulaStructuredReferenceSuggestion(
    Guid TableId,
    Guid? ColumnId,
    TableReferenceArea Area,
    string DisplayText,
    FormulaTextSpan ReplacementSpan,
    string SourceText);

public sealed partial class SpreadsheetFormulaEditingAssistant
{
    /// <summary>
    /// Reports whether point-mode insertion is outside a quoted literal or an
    /// existing structured token. A provisional span is validated against the draft.
    /// This query never reads cells or changes workbook/history state.
    /// </summary>
    public static bool CanInsertReference(string text, int caretIndex, FormulaTextSpan? provisionalSpan = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.StartsWith('=') || caretIndex < 0 || caretIndex > text.Length) return false;
        var span = provisionalSpan ?? new FormulaTextSpan(caretIndex, 0);
        return span.Start >= 0 && span.Length >= 0 && span.Start <= text.Length &&
            span.Length <= text.Length - span.Start && IsOutsideLiteralAndReference(text, span.Start);
    }

    /// <summary>
    /// Completes a Table name or a simple Table[column]/[@column] fragment.
    /// Results are bounded to 256 metadata items and never enumerate worksheet cells.
    /// </summary>
    public static IReadOnlyList<FormulaStructuredReferenceSuggestion> GetStructuredReferenceSuggestions(
        string text, int caretIndex, Workbook workbook, Worksheet worksheet,
        CellAddress formulaAddress, int maximumResults = 12)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateWorkbookContext(workbook, worksheet);
        ValidateCaret(text, caretIndex);
        if (maximumResults is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(maximumResults));
        if (!text.StartsWith('=')) return [];

        var bracket = text.LastIndexOf('[', Math.Max(0, caretIndex - 1), caretIndex);
        if (bracket >= 1 && IsOutsideLiteralAndReference(text, bracket) &&
            !text.AsSpan(bracket + 1, caretIndex - bracket - 1).Contains(']'))
        {
            var start = FindIdentifierStart(text, bracket);
            var name = text[start..bracket];
            var prefix = text[(bracket + 1)..caretIndex];
            var area = TableReferenceArea.Data;
            if (prefix.StartsWith('@'))
            {
                area = TableReferenceArea.ThisRow;
                prefix = prefix[1..];
            }
            SpreadsheetTable? table;
            if (name.Length == 0)
            {
                if (!worksheet.TryGetTable(formulaAddress, out table)) return [];
            }
            else if (!workbook.TryGetTable(name, out var owner, out table) ||
                     area == TableReferenceArea.ThisRow && !ReferenceEquals(owner, worksheet)) return [];
            if (table is null || area == TableReferenceArea.ThisRow &&
                table.DataRange?.Contains(formulaAddress) != true) return [];
            var span = new FormulaTextSpan(start, caretIndex - start +
                (caretIndex < text.Length && text[caretIndex] == ']' ? 1 : 0));
            return table.Columns.Where(column => column.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(maximumResults).Select(column => new FormulaStructuredReferenceSuggestion(
                    table.Id, column.Id, area, column.Name, span, text)).ToArray();
        }
        if (!IsOutsideLiteralAndReference(text, caretIndex)) return [];
        var identifierStart = FindIdentifierStart(text, caretIndex);
        if (identifierStart == caretIndex || identifierStart > 0 && text[identifierStart - 1] == '!') return [];
        var tablePrefix = text[identifierStart..caretIndex];
        return workbook.Tables.Where(table => table.Name.StartsWith(tablePrefix, StringComparison.OrdinalIgnoreCase))
            .Take(maximumResults).Select(table => new FormulaStructuredReferenceSuggestion(
                table.Id, null, TableReferenceArea.Data, table.Name,
                new FormulaTextSpan(identifierStart, caretIndex - identifierStart), text)).ToArray();
    }

    /// <summary>
    /// Applies a suggestion using current stable identities. Stale text or deleted
    /// identities are rejected; workbook/history state is never changed by completion.
    /// </summary>
    public static FormulaTextEditResult ApplyStructuredReferenceSuggestion(
        string text, Workbook workbook, Worksheet worksheet, CellAddress formulaAddress,
        FormulaStructuredReferenceSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        ValidateWorkbookContext(workbook, worksheet);
        if (!string.Equals(text, suggestion.SourceText, StringComparison.Ordinal))
            throw new InvalidOperationException("The completion text has changed.");
        var table = workbook.Tables.FirstOrDefault(candidate => candidate.Id == suggestion.TableId)
            ?? throw new InvalidOperationException("The completion Table no longer exists.");
        if (suggestion.ColumnId is { } candidateId && !table.TryGetColumn(candidateId, out _))
            throw new InvalidOperationException("The completion column no longer exists.");
        var index = suggestion.ColumnId is { } id ? table.GetColumnIndex(id) : -1;
        if (suggestion.Area == TableReferenceArea.ThisRow &&
            (!worksheet.TryGetTable(formulaAddress, out var containing) || containing?.Id != table.Id ||
             table.DataRange?.Contains(formulaAddress) != true))
            throw new InvalidOperationException("Current-row completion requires the owning Table data row.");
        return InsertStructuredText(text, suggestion.ReplacementSpan,
            FormatStructuredReference(table, suggestion.Area, index, index));
    }

    /// <summary>
    /// Inserts a structured reference when the selection exactly represents a Table
    /// area or owning current row. Other selections use the existing A1 insertion path.
    /// Hosts retain the returned provisional span during a drag; no cells are read.
    /// </summary>
    public static FormulaTextEditResult InsertReference(
        string text, int caretIndex, Workbook workbook, Worksheet worksheet,
        CellAddress formulaAddress, Worksheet referenceWorksheet, CellRange range,
        FormulaTextSpan? provisionalSpan = null)
    {
        ValidateWorkbookContext(workbook, worksheet);
        ValidateWorkbookContext(workbook, referenceWorksheet);
        ValidateCaret(text, caretIndex);
        if (!text.StartsWith('=')) throw new ArgumentException("Point mode requires a formula.", nameof(text));
        var span = provisionalSpan ?? new FormulaTextSpan(caretIndex, 0);
        ValidateSpan(text, span);
        if (!IsOutsideLiteralAndReference(text, span.Start))
            throw new ArgumentException("A reference cannot be inserted inside a literal or structured token.", nameof(text));
        if (referenceWorksheet.TryGetTable(range.TopLeft, out var table) && table is not null &&
            table.Range.Contains(range.BottomRight))
        {
            TableReferenceArea? area = null;
            if (ReferenceEquals(worksheet, referenceWorksheet) && range.Top == range.Bottom &&
                range.Top == formulaAddress.RowIndex && table.DataRange?.Contains(formulaAddress) == true)
                area = TableReferenceArea.ThisRow;
            else if (table.DataRange is { } data && range.Top == data.Top && range.Bottom == data.Bottom)
                area = TableReferenceArea.Data;
            else if (range.Top == table.Range.Top && range.Bottom == table.Range.Bottom)
                area = TableReferenceArea.All;
            else if (table.HasHeaders && range.Top == table.Range.Top && range.Bottom == range.Top)
                area = TableReferenceArea.Headers;
            else if (table.HasTotalsRow && range.Top == table.Range.Bottom && range.Bottom == range.Top)
                area = TableReferenceArea.Totals;
            if (area.HasValue)
                return InsertStructuredText(text, span, FormatStructuredReference(table, area.Value,
                    range.Left - table.Range.Left, range.Right - table.Range.Left));
        }
        return InsertReference(text, caretIndex, range,
            ReferenceEquals(worksheet, referenceWorksheet) ? null : referenceWorksheet.Name, span);
    }

    private static string FormatStructuredReference(SpreadsheetTable table, TableReferenceArea area, int first, int last)
    {
        var selector = area switch
        {
            TableReferenceArea.All => "#All",
            TableReferenceArea.Data => "#Data",
            TableReferenceArea.Headers => "#Headers",
            TableReferenceArea.Totals => "#Totals",
            TableReferenceArea.ThisRow => "#This Row",
            _ => throw new ArgumentOutOfRangeException(nameof(area)),
        };
        if (first < 0) return $"{table.Name}[{selector}]";
        var firstName = StructuredReferenceFormulaTranslator.EscapeColumnName(table.Columns[first].Name);
        var lastName = StructuredReferenceFormulaTranslator.EscapeColumnName(table.Columns[last].Name);
        var columns = first == last ? $"[{firstName}]" : $"[{firstName}]:[{lastName}]";
        return $"{table.Name}[[{selector}],{columns}]";
    }

    private static FormulaTextEditResult InsertStructuredText(string text, FormulaTextSpan span, string reference)
    {
        ValidateSpan(text, span);
        var result = string.Concat(text.AsSpan(0, span.Start), reference, text.AsSpan(span.End));
        var inserted = new FormulaTextSpan(span.Start, reference.Length);
        return new(result, inserted.End, inserted);
    }

    private static void ValidateWorkbookContext(Workbook workbook, Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!workbook.Worksheets.Contains(worksheet))
            throw new ArgumentException("The worksheet must belong to the workbook.", nameof(worksheet));
    }

    private static bool IsOutsideLiteralAndReference(string text, int end)
    {
        char quote = '\0';
        var depth = 0;
        for (var index = 0; index < end; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character != quote) continue;
                if (index + 1 < end && text[index + 1] == quote) index++;
                else quote = '\0';
            }
            else if (depth > 0 && character == '\'' && index + 1 < end) index++;
            else if (character is '"' or '\'') quote = character;
            else if (character == '[') depth++;
            else if (character == ']') depth--;
        }
        return quote == '\0' && depth == 0;
    }
}
