using System.Text;

namespace NeraSpreadSheet.Core;

/// <summary>
/// Translates relative A1 references from one cell anchor to another while
/// preserving mixed/absolute references and text literals.
/// </summary>
public static class A1FormulaReferenceTranslator
{
    public static string Translate(
        string formula,
        CellAddress sourceCell,
        CellAddress targetCell)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        var rowDelta = targetCell.RowIndex - sourceCell.RowIndex;
        var columnDelta = targetCell.ColumnIndex - sourceCell.ColumnIndex;
        if (rowDelta == 0 && columnDelta == 0)
        {
            return formula;
        }

        var builder = new StringBuilder(formula.Length + 8);
        var inString = false;
        for (var index = 0; index < formula.Length;)
        {
            var character = formula[index];
            if (character == '"')
            {
                builder.Append(character);
                index++;
                if (inString &&
                    index < formula.Length &&
                    formula[index] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString &&
                IsReferenceBoundaryBefore(formula, index) &&
                TryReadReference(
                    formula,
                    index,
                    out var consumed,
                    out var reference))
            {
                builder.Append(TranslateReference(
                    reference,
                    rowDelta,
                    columnDelta));
                index += consumed;
                continue;
            }

            builder.Append(character);
            index++;
        }

        return builder.ToString();
    }

    private static bool TryReadReference(
        string text,
        int start,
        out int consumed,
        out ParsedReference reference)
    {
        consumed = 0;
        reference = default;
        var index = start;
        var absoluteColumn = false;
        var absoluteRow = false;

        if (index < text.Length && text[index] == '$')
        {
            absoluteColumn = true;
            index++;
        }

        var columnStart = index;
        while (index < text.Length &&
               char.IsAsciiLetter(text[index]) &&
               index - columnStart < 3)
        {
            index++;
        }

        if (index == columnStart ||
            index < text.Length && char.IsAsciiLetter(text[index]))
        {
            return false;
        }

        if (index < text.Length && text[index] == '$')
        {
            absoluteRow = true;
            index++;
        }

        var rowStart = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        if (index == rowStart ||
            !IsReferenceBoundaryAfter(text, index))
        {
            return false;
        }

        var token = text[start..index];
        if (!CellAddress.TryParseA1(token, out var address))
        {
            return false;
        }

        consumed = index - start;
        reference = new ParsedReference(
            address,
            absoluteRow,
            absoluteColumn);
        return true;
    }

    private static string TranslateReference(
        ParsedReference reference,
        int rowDelta,
        int columnDelta)
    {
        var row = reference.Address.RowIndex +
                  (reference.AbsoluteRow ? 0 : rowDelta);
        var column = reference.Address.ColumnIndex +
                     (reference.AbsoluteColumn ? 0 : columnDelta);
        if (row < 0 ||
            row >= SpreadsheetLimits.MaxRows ||
            column < 0 ||
            column >= SpreadsheetLimits.MaxColumns)
        {
            return "#REF!";
        }

        var translated = new CellAddress(row, column);
        var a1 = translated.ToA1();
        var split = 0;
        while (split < a1.Length &&
               char.IsAsciiLetter(a1[split]))
        {
            split++;
        }

        return string.Concat(
            reference.AbsoluteColumn ? "$" : string.Empty,
            a1.AsSpan(0, split),
            reference.AbsoluteRow ? "$" : string.Empty,
            a1.AsSpan(split));
    }

    private static bool IsReferenceBoundaryBefore(
        string text,
        int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = text[index - 1];
        return !char.IsAsciiLetterOrDigit(previous) &&
               previous is not '_' and not '.';
    }

    private static bool IsReferenceBoundaryAfter(
        string text,
        int index)
    {
        if (index >= text.Length)
        {
            return true;
        }

        var next = text[index];
        return !char.IsAsciiLetterOrDigit(next) &&
               next is not '_' and not '.';
    }

    private readonly record struct ParsedReference(
        CellAddress Address,
        bool AbsoluteRow,
        bool AbsoluteColumn);
}
