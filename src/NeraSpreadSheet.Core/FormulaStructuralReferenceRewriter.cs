using System.Text;

namespace NeraSpreadSheet.Core;

/// <summary>
/// Rewrites A1 references through structural worksheet transforms while
/// preserving absolute markers, quoted worksheet names and string literals.
/// </summary>
public static class FormulaStructuralReferenceRewriter
{
    private const string LocalWorksheetSentinel =
        "\u0001NERA_LOCAL_WORKSHEET\u0001";

    public static string RewriteLocal(
        string formula,
        WorksheetStructuralChange change) =>
        Rewrite(
            formula,
            LocalWorksheetSentinel,
            LocalWorksheetSentinel,
            change);

    public static string RewriteLocal(
        string formula,
        WorksheetAxisMove move) =>
        Rewrite(
            formula,
            LocalWorksheetSentinel,
            LocalWorksheetSentinel,
            move);

    public static string Rewrite(
        string formula,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetStructuralChange change) =>
        RewriteCore(
            formula,
            formulaWorksheetName,
            changedWorksheetName,
            change.Axis,
            change.TryMapIndex,
            change.TryMapInterval,
            throwOnDiscontiguousRange: false);

    public static string Rewrite(
        string formula,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetAxisMove move)
    {
        bool TryMapMoveIndex(
            int sourceIndex,
            out int targetIndex)
        {
            targetIndex = move.MapIndex(sourceIndex);
            return true;
        }

        return RewriteCore(
            formula,
            formulaWorksheetName,
            changedWorksheetName,
            move.Axis,
            TryMapMoveIndex,
            move.TryMapContiguousInterval,
            throwOnDiscontiguousRange: true);
    }

    private static string RewriteCore(
        string formula,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetAxis axis,
        TryMapIndexDelegate tryMapIndex,
        TryMapIntervalDelegate tryMapInterval,
        bool throwOnDiscontiguousRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentException.ThrowIfNullOrWhiteSpace(formulaWorksheetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedWorksheetName);
        ArgumentNullException.ThrowIfNull(tryMapIndex);
        ArgumentNullException.ThrowIfNull(tryMapInterval);

        var builder = new StringBuilder(formula.Length + 16);
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
                TryReadReferenceExpression(
                    formula,
                    index,
                    out var consumed,
                    out var expression))
            {
                builder.Append(RewriteExpression(
                    expression,
                    formulaWorksheetName,
                    changedWorksheetName,
                    axis,
                    tryMapIndex,
                    tryMapInterval,
                    throwOnDiscontiguousRange));
                index += consumed;
                continue;
            }

            builder.Append(character);
            index++;
        }

        return builder.ToString();
    }

    private static string RewriteExpression(
        ParsedReferenceExpression expression,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetAxis axis,
        TryMapIndexDelegate tryMapIndex,
        TryMapIntervalDelegate tryMapInterval,
        bool throwOnDiscontiguousRange)
    {
        var firstSheet = expression.FirstQualifier?.SheetName ??
            formulaWorksheetName;
        var secondSheet = expression.SecondQualifier?.SheetName ??
            expression.FirstQualifier?.SheetName ??
            formulaWorksheetName;

        if (!string.Equals(
                firstSheet,
                changedWorksheetName,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                secondSheet,
                changedWorksheetName,
                StringComparison.OrdinalIgnoreCase))
        {
            return expression.RawText;
        }

        if (expression.SecondReference is not { } second)
        {
            if (!TryMapReference(
                    expression.FirstReference,
                    axis,
                    tryMapIndex,
                    out var mapped))
            {
                return "#REF!";
            }

            return string.Concat(
                expression.FirstQualifier?.RawPrefix ?? string.Empty,
                FormatReference(mapped));
        }

        if (!TryMapRange(
                expression.FirstReference,
                second,
                axis,
                tryMapInterval,
                out var firstMapped,
                out var secondMapped))
        {
            if (throwOnDiscontiguousRange)
            {
                throw new InvalidOperationException(
                    "Cannot reorder because a formula range would become discontiguous.");
            }

            return "#REF!";
        }

        return string.Concat(
            expression.FirstQualifier?.RawPrefix ?? string.Empty,
            FormatReference(firstMapped),
            ":",
            expression.SecondQualifier?.RawPrefix ?? string.Empty,
            FormatReference(secondMapped));
    }

    private static bool TryMapReference(
        ParsedReference reference,
        WorksheetAxis axis,
        TryMapIndexDelegate tryMapIndex,
        out ParsedReference mapped)
    {
        var sourceIndex = axis == WorksheetAxis.Row
            ? reference.Address.RowIndex
            : reference.Address.ColumnIndex;
        if (!tryMapIndex(sourceIndex, out var targetIndex))
        {
            mapped = default;
            return false;
        }

        var address = axis == WorksheetAxis.Row
            ? new CellAddress(
                targetIndex,
                reference.Address.ColumnIndex)
            : new CellAddress(
                reference.Address.RowIndex,
                targetIndex);
        mapped = reference with { Address = address };
        return true;
    }

    private static bool TryMapRange(
        ParsedReference first,
        ParsedReference second,
        WorksheetAxis axis,
        TryMapIntervalDelegate tryMapInterval,
        out ParsedReference firstMapped,
        out ParsedReference secondMapped)
    {
        var firstIndex = axis == WorksheetAxis.Row
            ? first.Address.RowIndex
            : first.Address.ColumnIndex;
        var secondIndex = axis == WorksheetAxis.Row
            ? second.Address.RowIndex
            : second.Address.ColumnIndex;
        var ascending = firstIndex <= secondIndex;
        var start = Math.Min(firstIndex, secondIndex);
        var end = Math.Max(firstIndex, secondIndex);
        if (!tryMapInterval(
                start,
                end,
                out var mappedStart,
                out var mappedEnd))
        {
            firstMapped = default;
            secondMapped = default;
            return false;
        }

        var mappedFirstIndex = ascending
            ? mappedStart
            : mappedEnd;
        var mappedSecondIndex = ascending
            ? mappedEnd
            : mappedStart;
        firstMapped = first with
        {
            Address = axis == WorksheetAxis.Row
                ? new CellAddress(
                    mappedFirstIndex,
                    first.Address.ColumnIndex)
                : new CellAddress(
                    first.Address.RowIndex,
                    mappedFirstIndex),
        };
        secondMapped = second with
        {
            Address = axis == WorksheetAxis.Row
                ? new CellAddress(
                    mappedSecondIndex,
                    second.Address.ColumnIndex)
                : new CellAddress(
                    second.Address.RowIndex,
                    mappedSecondIndex),
        };
        return true;
    }

    private static bool TryReadReferenceExpression(
        string text,
        int start,
        out int consumed,
        out ParsedReferenceExpression expression)
    {
        consumed = 0;
        expression = default;
        var index = start;

        TryReadSheetQualifier(
            text,
            ref index,
            out var firstQualifier);
        if (!TryReadReference(
                text,
                ref index,
                out var firstReference))
        {
            return false;
        }

        ParsedSheetQualifier? secondQualifier = null;
        ParsedReference? secondReference = null;
        if (index < text.Length && text[index] == ':')
        {
            index++;
            TryReadSheetQualifier(
                text,
                ref index,
                out secondQualifier);
            if (!TryReadReference(
                    text,
                    ref index,
                    out var parsedSecond))
            {
                return false;
            }

            secondReference = parsedSecond;
        }

        if (!IsReferenceBoundaryAfter(text, index))
        {
            return false;
        }

        consumed = index - start;
        expression = new ParsedReferenceExpression(
            text[start..index],
            firstQualifier,
            firstReference,
            secondQualifier,
            secondReference);
        return true;
    }

    private static bool TryReadSheetQualifier(
        string text,
        ref int index,
        out ParsedSheetQualifier? qualifier)
    {
        qualifier = null;
        var start = index;
        if (index >= text.Length)
        {
            return false;
        }

        if (text[index] == '\'')
        {
            index++;
            var logical = new StringBuilder();
            var closed = false;
            while (index < text.Length)
            {
                if (text[index] != '\'')
                {
                    logical.Append(text[index]);
                    index++;
                    continue;
                }

                if (index + 1 < text.Length &&
                    text[index + 1] == '\'')
                {
                    logical.Append('\'');
                    index += 2;
                    continue;
                }

                index++;
                closed = true;
                break;
            }

            if (!closed ||
                index >= text.Length ||
                text[index] != '!')
            {
                index = start;
                return false;
            }

            index++;
            qualifier = new ParsedSheetQualifier(
                text[start..index],
                logical.ToString());
            return true;
        }

        while (index < text.Length &&
               IsUnquotedSheetCharacter(text[index]))
        {
            index++;
        }

        if (index == start ||
            index >= text.Length ||
            text[index] != '!')
        {
            index = start;
            return false;
        }

        var sheetName = text[start..index];
        index++;
        qualifier = new ParsedSheetQualifier(
            text[start..index],
            sheetName);
        return true;
    }

    private static bool TryReadReference(
        string text,
        ref int index,
        out ParsedReference reference)
    {
        reference = default;
        var start = index;
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
            index < text.Length &&
            char.IsAsciiLetter(text[index]))
        {
            index = start;
            return false;
        }

        if (index < text.Length && text[index] == '$')
        {
            absoluteRow = true;
            index++;
        }

        var rowStart = index;
        while (index < text.Length &&
               char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        if (index == rowStart)
        {
            index = start;
            return false;
        }

        var token = text[start..index];
        if (!CellAddress.TryParseA1(token, out var address))
        {
            index = start;
            return false;
        }

        reference = new ParsedReference(
            address,
            absoluteRow,
            absoluteColumn);
        return true;
    }

    private static string FormatReference(
        ParsedReference reference)
    {
        var a1 = reference.Address.ToA1();
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

    private static bool IsUnquotedSheetCharacter(
        char character) =>
        char.IsAsciiLetterOrDigit(character) ||
        character is '_' or '.';

    private delegate bool TryMapIndexDelegate(
        int sourceIndex,
        out int targetIndex);

    private delegate bool TryMapIntervalDelegate(
        int start,
        int end,
        out int mappedStart,
        out int mappedEnd);

    private readonly record struct ParsedReference(
        CellAddress Address,
        bool AbsoluteRow,
        bool AbsoluteColumn);

    private readonly record struct ParsedSheetQualifier(
        string RawPrefix,
        string SheetName);

    private readonly record struct ParsedReferenceExpression(
        string RawText,
        ParsedSheetQualifier? FirstQualifier,
        ParsedReference FirstReference,
        ParsedSheetQualifier? SecondQualifier,
        ParsedReference? SecondReference);
}
