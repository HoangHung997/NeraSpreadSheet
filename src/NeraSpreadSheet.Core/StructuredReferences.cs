using System.Text;

namespace NeraSpreadSheet.Core;

public static class StructuredReferenceFormulaTranslator
{
    public static string Translate(
        string formula,
        Workbook workbook,
        Worksheet currentWorksheet,
        CellAddress currentAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(currentWorksheet);
        var builder = new StringBuilder(formula.Length + 32);
        var index = 0;
        while (index < formula.Length)
        {
            if (formula[index] == '"')
            {
                CopyStringLiteral(formula, builder, ref index);
                continue;
            }

            if (IsIdentifierStart(formula[index]))
            {
                var identifierStart = index;
                index++;
                while (index < formula.Length &&
                       IsIdentifierPart(formula[index]))
                {
                    index++;
                }

                var identifier = formula[identifierStart..index];
                if (index < formula.Length &&
                    formula[index] == '[' &&
                    TryReadBracketExpression(
                        formula,
                        ref index,
                        out var expression))
                {
                    builder.Append(Resolve(
                        workbook,
                        currentWorksheet,
                        currentAddress,
                        identifier,
                        expression));
                    continue;
                }

                builder.Append(identifier);
                continue;
            }

            if (formula[index] == '[' &&
                TryReadBracketExpression(
                    formula,
                    ref index,
                    out var implicitExpression))
            {
                builder.Append(Resolve(
                    workbook,
                    currentWorksheet,
                    currentAddress,
                    tableName: null,
                    implicitExpression));
                continue;
            }

            builder.Append(formula[index++]);
        }

        return builder.ToString();
    }

    private static string Resolve(
        Workbook workbook,
        Worksheet currentWorksheet,
        CellAddress currentAddress,
        string? tableName,
        string expression)
    {
        Worksheet tableWorksheet;
        SpreadsheetTable table;
        if (tableName is null)
        {
            if (!currentWorksheet.TryGetTable(
                    currentAddress,
                    out var containingTable) ||
                containingTable is null)
            {
                return "#REF!";
            }

            tableWorksheet = currentWorksheet;
            table = containingTable;
        }
        else if (!workbook.TryGetTable(
                     tableName,
                     out var resolvedWorksheet,
                     out var resolvedTable) ||
                 resolvedWorksheet is null ||
                 resolvedTable is null)
        {
            return "#REF!";
        }
        else
        {
            tableWorksheet = resolvedWorksheet;
            table = resolvedTable;
        }

        if (!StructuredReferenceSpec.TryParse(
                expression,
                tableName is null,
                out var spec))
        {
            return "#REF!";
        }

        if (!TryResolveRange(
                table,
                spec,
                currentAddress.RowIndex,
                out var range))
        {
            return "#REF!";
        }

        var reference = range.TopLeft == range.BottomRight
            ? ToAbsoluteA1(range.TopLeft)
            : $"{ToAbsoluteA1(range.TopLeft)}:{ToAbsoluteA1(range.BottomRight)}";
        if (ReferenceEquals(tableWorksheet, currentWorksheet))
        {
            return reference;
        }

        return $"'{EscapeWorksheetName(tableWorksheet.Name)}'!{reference}";
    }

    private static bool TryResolveRange(
        SpreadsheetTable table,
        StructuredReferenceSpec spec,
        int currentRow,
        out CellRange range)
    {
        var left = table.Range.Left;
        var right = table.Range.Right;
        if (spec.FirstColumnName is not null)
        {
            if (!table.TryGetColumn(
                    spec.FirstColumnName,
                    out var firstColumn) ||
                firstColumn is null)
            {
                range = default;
                return false;
            }

            left = table.Range.Left + table.GetColumnIndex(firstColumn.Id);
            right = left;
            if (spec.LastColumnName is not null)
            {
                if (!table.TryGetColumn(
                        spec.LastColumnName,
                        out var lastColumn) ||
                    lastColumn is null)
                {
                    range = default;
                    return false;
                }

                right = table.Range.Left +
                        table.GetColumnIndex(lastColumn.Id);
                if (right < left)
                {
                    (left, right) = (right, left);
                }
            }
        }

        int top;
        int bottom;
        switch (spec.Area)
        {
            case TableReferenceArea.All:
                top = table.Range.Top;
                bottom = table.Range.Bottom;
                break;
            case TableReferenceArea.Data:
                if (table.DataRange is not { } dataRange)
                {
                    range = default;
                    return false;
                }
                top = dataRange.Top;
                bottom = dataRange.Bottom;
                break;
            case TableReferenceArea.Headers:
                if (!table.HasHeaders)
                {
                    range = default;
                    return false;
                }
                top = table.Range.Top;
                bottom = table.Range.Top;
                break;
            case TableReferenceArea.Totals:
                if (!table.HasTotalsRow)
                {
                    range = default;
                    return false;
                }
                top = table.Range.Bottom;
                bottom = table.Range.Bottom;
                break;
            case TableReferenceArea.ThisRow:
                if (table.DataRange is not { } currentDataRange ||
                    currentRow < currentDataRange.Top ||
                    currentRow > currentDataRange.Bottom)
                {
                    range = default;
                    return false;
                }
                top = currentRow;
                bottom = currentRow;
                break;
            default:
                range = default;
                return false;
        }

        range = new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right));
        return true;
    }

    private static bool TryReadBracketExpression(
        string formula,
        ref int index,
        out string expression)
    {
        if (index >= formula.Length || formula[index] != '[')
        {
            expression = string.Empty;
            return false;
        }

        var start = index;
        var depth = 0;
        while (index < formula.Length)
        {
            var character = formula[index++];
            if (character == '[')
            {
                depth++;
            }
            else if (character == ']')
            {
                depth--;
                if (depth == 0)
                {
                    expression = formula[start..index];
                    return true;
                }
            }
        }

        index = start;
        expression = string.Empty;
        return false;
    }

    private static void CopyStringLiteral(
        string formula,
        StringBuilder builder,
        ref int index)
    {
        builder.Append(formula[index++]);
        while (index < formula.Length)
        {
            var character = formula[index++];
            builder.Append(character);
            if (character != '"')
            {
                continue;
            }

            if (index < formula.Length && formula[index] == '"')
            {
                builder.Append(formula[index++]);
                continue;
            }

            return;
        }
    }

    private static string ToAbsoluteA1(CellAddress address)
    {
        var a1 = address.ToA1();
        var digitIndex = 0;
        while (digitIndex < a1.Length &&
               char.IsAsciiLetter(a1[digitIndex]))
        {
            digitIndex++;
        }

        return $"${a1[..digitIndex]}${a1[digitIndex..]}";
    }

    private static string EscapeWorksheetName(string name) =>
        name.Replace("'", "''", StringComparison.Ordinal);

    private static bool IsIdentifierStart(char character) =>
        char.IsLetter(character) || character is '_' or '\\';

    private static bool IsIdentifierPart(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '.' or '\\';

    private sealed record StructuredReferenceSpec(
        TableReferenceArea Area,
        string? FirstColumnName,
        string? LastColumnName)
    {
        public static bool TryParse(
            string expression,
            bool isImplicit,
            out StructuredReferenceSpec spec)
        {
            if (expression.Length < 2 ||
                expression[0] != '[' ||
                expression[^1] != ']')
            {
                spec = null!;
                return false;
            }

            var inner = expression[1..^1].Trim();
            if (inner.Length == 0)
            {
                spec = null!;
                return false;
            }

            var area = isImplicit
                ? TableReferenceArea.ThisRow
                : TableReferenceArea.Data;
            string? firstColumn = null;
            string? lastColumn = null;
            var tokens = Tokenize(inner);
            foreach (var token in tokens)
            {
                var normalized = NormalizeToken(token.Text);
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (TryParseArea(normalized, out var parsedArea))
                {
                    area = parsedArea;
                    continue;
                }

                if (normalized[0] == '@')
                {
                    area = TableReferenceArea.ThisRow;
                    normalized = NormalizeToken(normalized[1..]);
                    if (normalized.Length == 0)
                    {
                        continue;
                    }
                }

                if (firstColumn is null)
                {
                    firstColumn = UnescapeColumnName(normalized);
                }
                else if (token.Separator == ':' ||
                         lastColumn is null)
                {
                    lastColumn = UnescapeColumnName(normalized);
                }
                else
                {
                    spec = null!;
                    return false;
                }
            }

            spec = new StructuredReferenceSpec(
                area,
                firstColumn,
                lastColumn);
            return true;
        }

        private static List<ReferenceToken> Tokenize(string expression)
        {
            var tokens = new List<ReferenceToken>();
            var start = 0;
            var depth = 0;
            var separator = '\0';
            for (var index = 0; index <= expression.Length; index++)
            {
                var atEnd = index == expression.Length;
                var character = atEnd ? '\0' : expression[index];
                if (!atEnd && character == '[')
                {
                    depth++;
                    continue;
                }
                if (!atEnd && character == ']')
                {
                    depth--;
                    continue;
                }
                if (!atEnd && depth > 0)
                {
                    continue;
                }
                if (!atEnd && character is not ',' and not ':')
                {
                    continue;
                }

                tokens.Add(new ReferenceToken(
                    expression[start..index].Trim(),
                    separator));
                separator = character;
                start = index + 1;
            }

            return tokens;
        }

        private static string NormalizeToken(string token)
        {
            var normalized = token.Trim();
            while (normalized.Length >= 2 &&
                   normalized[0] == '[' &&
                   normalized[^1] == ']')
            {
                normalized = normalized[1..^1].Trim();
            }

            return normalized;
        }

        private static bool TryParseArea(
            string token,
            out TableReferenceArea area)
        {
            if (string.Equals(
                    token,
                    "#All",
                    StringComparison.OrdinalIgnoreCase))
            {
                area = TableReferenceArea.All;
                return true;
            }
            if (string.Equals(
                    token,
                    "#Data",
                    StringComparison.OrdinalIgnoreCase))
            {
                area = TableReferenceArea.Data;
                return true;
            }
            if (string.Equals(
                    token,
                    "#Headers",
                    StringComparison.OrdinalIgnoreCase))
            {
                area = TableReferenceArea.Headers;
                return true;
            }
            if (string.Equals(
                    token,
                    "#Totals",
                    StringComparison.OrdinalIgnoreCase))
            {
                area = TableReferenceArea.Totals;
                return true;
            }
            if (string.Equals(
                    token,
                    "#This Row",
                    StringComparison.OrdinalIgnoreCase))
            {
                area = TableReferenceArea.ThisRow;
                return true;
            }

            area = default;
            return false;
        }

        private static string UnescapeColumnName(string name) =>
            name.Replace("]]", "]", StringComparison.Ordinal);

        private readonly record struct ReferenceToken(
            string Text,
            char Separator);
    }
}

public static class StructuredReferenceFormulaRewriter
{
    public static string RenameTable(
        string formula,
        string oldName,
        string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        var validatedNewName = TableNameRules.ValidateTableName(newName);
        return Rewrite(formula, (tableName, expression) =>
            string.Equals(
                tableName,
                oldName,
                StringComparison.OrdinalIgnoreCase)
                ? validatedNewName + expression
                : tableName + expression);
    }

    public static string RenameColumn(
        string formula,
        string tableName,
        string oldName,
        string newName,
        bool rewriteImplicitReferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        var validatedNewName = TableNameRules.ValidateColumnName(newName);
        return Rewrite(formula, (candidateTableName, expression) =>
        {
            var applies = candidateTableName.Length == 0
                ? rewriteImplicitReferences
                : string.Equals(
                    candidateTableName,
                    tableName,
                    StringComparison.OrdinalIgnoreCase);
            return candidateTableName +
                   (applies
                       ? ReplaceColumnTokens(
                           expression,
                           oldName,
                           validatedNewName)
                       : expression);
        });
    }

    private static string Rewrite(
        string formula,
        Func<string, string, string> replacement)
    {
        var builder = new StringBuilder(formula.Length + 16);
        var index = 0;
        while (index < formula.Length)
        {
            if (formula[index] == '"')
            {
                CopyStringLiteral(formula, builder, ref index);
                continue;
            }

            if (IsIdentifierStart(formula[index]))
            {
                var start = index++;
                while (index < formula.Length &&
                       IsIdentifierPart(formula[index]))
                {
                    index++;
                }

                var identifier = formula[start..index];
                if (index < formula.Length &&
                    formula[index] == '[' &&
                    TryReadBracketExpression(
                        formula,
                        ref index,
                        out var expression))
                {
                    builder.Append(replacement(identifier, expression));
                    continue;
                }

                builder.Append(identifier);
                continue;
            }

            if (formula[index] == '[' &&
                TryReadBracketExpression(
                    formula,
                    ref index,
                    out var implicitExpression))
            {
                builder.Append(replacement(
                    string.Empty,
                    implicitExpression));
                continue;
            }

            builder.Append(formula[index++]);
        }

        return builder.ToString();
    }

    private static string ReplaceColumnTokens(
        string expression,
        string oldName,
        string newName)
    {
        var escapedOld = oldName.Replace("]", "]]", StringComparison.Ordinal);
        var escapedNew = newName.Replace("]", "]]", StringComparison.Ordinal);
        var result = ReplaceOrdinalIgnoreCase(
            expression,
            $"[{escapedOld}]",
            $"[{escapedNew}]");
        if (string.Equals(
                result,
                $"[{escapedOld}]",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"[{escapedNew}]";
        }

        result = ReplaceOrdinalIgnoreCase(
            result,
            $"@{escapedOld}",
            $"@{escapedNew}");
        return result;
    }

    private static string ReplaceOrdinalIgnoreCase(
        string source,
        string oldValue,
        string newValue)
    {
        var builder = new StringBuilder(source.Length);
        var searchStart = 0;
        while (searchStart < source.Length)
        {
            var index = source.IndexOf(
                oldValue,
                searchStart,
                StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                builder.Append(source.AsSpan(searchStart));
                break;
            }

            builder.Append(source.AsSpan(searchStart, index - searchStart));
            builder.Append(newValue);
            searchStart = index + oldValue.Length;
        }

        return builder.Length == 0 && source.Length == 0
            ? source
            : builder.ToString();
    }

    private static bool TryReadBracketExpression(
        string formula,
        ref int index,
        out string expression)
    {
        var start = index;
        var depth = 0;
        while (index < formula.Length)
        {
            var character = formula[index++];
            if (character == '[')
            {
                depth++;
            }
            else if (character == ']')
            {
                depth--;
                if (depth == 0)
                {
                    expression = formula[start..index];
                    return true;
                }
            }
        }

        index = start;
        expression = string.Empty;
        return false;
    }

    private static void CopyStringLiteral(
        string formula,
        StringBuilder builder,
        ref int index)
    {
        builder.Append(formula[index++]);
        while (index < formula.Length)
        {
            var character = formula[index++];
            builder.Append(character);
            if (character != '"')
            {
                continue;
            }
            if (index < formula.Length && formula[index] == '"')
            {
                builder.Append(formula[index++]);
                continue;
            }
            return;
        }
    }

    private static bool IsIdentifierStart(char character) =>
        char.IsLetter(character) || character is '_' or '\\';

    private static bool IsIdentifierPart(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '.' or '\\';
}
