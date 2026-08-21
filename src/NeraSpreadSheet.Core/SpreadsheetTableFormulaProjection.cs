namespace NeraSpreadSheet.Core;

public enum SpreadsheetTableTotalsFunction
{
    None = 0,
    Average,
    CountNumbers,
    Count,
    Maximum,
    Minimum,
    Sum,
    Custom,
}

public static class SpreadsheetTableFormulaProjection
{
    public const int MaxProjectedFormulaCells = 1_000_000;

    public static void ProjectAll(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var tables = worksheet.Tables
            .Select(static table => table.Copy())
            .ToArray();
        ValidateProjectionSize(tables);
        foreach (var table in tables)
        {
            ProjectCore(worksheet, table);
        }
    }

    public static void RefreshMetadataFromCells(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var sourceTables = worksheet.Tables
            .Select(static table => table.Copy())
            .ToArray();
        ValidateProjectionSize(sourceTables);
        var changedRanges = new List<CellRange>();
        var refreshedTables = sourceTables
            .Select(table =>
            {
                var refreshed = RefreshTableMetadata(
                    worksheet,
                    table);
                if (!ReferenceEquals(refreshed, table))
                {
                    changedRanges.Add(table.Range);
                }
                return refreshed;
            })
            .ToArray();
        if (changedRanges.Count > 0)
        {
            worksheet.RestoreTables(
                refreshedTables,
                Union(changedRanges));
        }
    }

    public static void Project(
        Worksheet worksheet,
        SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(table);
        ValidateProjectionSize([table]);
        ProjectCore(worksheet, table);
    }

    public static void Synchronize(
        Worksheet worksheet,
        SpreadsheetTable previous,
        SpreadsheetTable next)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        if (previous.Id != next.Id)
        {
            throw new ArgumentException(
                "Table projection synchronization requires matching table identifiers.",
                nameof(next));
        }

        ValidateProjectionSize([next]);
        ClearRemovedMetadata(worksheet, previous, next);
        ProjectCore(worksheet, next);
    }

    public static SpreadsheetTable WithCalculatedColumnFormula(
        SpreadsheetTable table,
        Guid columnId,
        string? formula) =>
        ReplaceColumn(
            table,
            columnId,
            column => new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                formula,
                column.TotalsRowFormula,
                column.TotalsRowLabel));

    public static SpreadsheetTable WithTotalsRowFormula(
        SpreadsheetTable table,
        Guid columnId,
        string? formula) =>
        ReplaceColumn(
            table,
            columnId,
            column => new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                column.CalculatedColumnFormula,
                formula,
                formula is null
                    ? column.TotalsRowLabel
                    : null));

    public static SpreadsheetTable WithTotalsRowLabel(
        SpreadsheetTable table,
        Guid columnId,
        string? label) =>
        ReplaceColumn(
            table,
            columnId,
            column => new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                column.CalculatedColumnFormula,
                label is null
                    ? column.TotalsRowFormula
                    : null,
                label));

    public static SpreadsheetTable WithTotalsRowFunction(
        SpreadsheetTable table,
        Guid columnId,
        SpreadsheetTableTotalsFunction function,
        string? customFormula = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        var formula = CreateTotalsFormula(
            table,
            columnId,
            function,
            customFormula);
        return WithTotalsRowFormula(table, columnId, formula);
    }

    public static string? CreateTotalsFormula(
        SpreadsheetTable table,
        Guid columnId,
        SpreadsheetTableTotalsFunction function,
        string? customFormula = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!Enum.IsDefined(function))
        {
            throw new ArgumentOutOfRangeException(nameof(function));
        }

        if (function == SpreadsheetTableTotalsFunction.None)
        {
            return null;
        }
        if (function == SpreadsheetTableTotalsFunction.Custom)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(customFormula);
            return NormalizeFormula(customFormula);
        }
        if (customFormula is not null)
        {
            throw new ArgumentException(
                "A custom totals formula is valid only for the Custom totals function.",
                nameof(customFormula));
        }

        var column = table.Columns.FirstOrDefault(candidate =>
            candidate.Id == columnId)
            ?? throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        var functionNumber = function switch
        {
            SpreadsheetTableTotalsFunction.Average => 101,
            SpreadsheetTableTotalsFunction.CountNumbers => 102,
            SpreadsheetTableTotalsFunction.Count => 103,
            SpreadsheetTableTotalsFunction.Maximum => 104,
            SpreadsheetTableTotalsFunction.Minimum => 105,
            SpreadsheetTableTotalsFunction.Sum => 109,
            _ => throw new ArgumentOutOfRangeException(nameof(function)),
        };
        var escapedColumnName = column.Name.Replace(
            "]",
            "]]",
            StringComparison.Ordinal);
        return $"=SUBTOTAL({functionNumber},{table.Name}[{escapedColumnName}])";
    }

    public static void ApplyReplacement(
        Worksheet worksheet,
        SpreadsheetTable previous,
        SpreadsheetTable next)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        if (previous.Id != next.Id)
        {
            throw new ArgumentException(
                "A replacement table must retain the original table identifier.",
                nameof(next));
        }

        var tables = worksheet.Tables
            .Select(table => table.Id == previous.Id
                ? next.Copy()
                : table.Copy())
            .ToArray();
        if (!tables.Any(table => table.Id == previous.Id))
        {
            throw new KeyNotFoundException(
                $"Table '{previous.Id}' was not found.");
        }

        worksheet.RestoreTables(
            tables,
            Union(previous.Range, next.Range));
        Synchronize(worksheet, previous, next);
    }

    public static void RewriteA1Metadata(
        Worksheet worksheet,
        WorksheetStructuralChange change,
        CellRange signalRange)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        RewriteA1Metadata(
            worksheet,
            formula => FormulaStructuralReferenceRewriter.Rewrite(
                formula,
                worksheet.Name,
                worksheet.Name,
                change),
            signalRange);
    }

    public static void RewriteA1Metadata(
        Worksheet worksheet,
        WorksheetAxisMove move,
        CellRange signalRange)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        RewriteA1Metadata(
            worksheet,
            formula => FormulaStructuralReferenceRewriter.Rewrite(
                formula,
                worksheet.Name,
                worksheet.Name,
                move),
            signalRange);
    }

    public static SpreadsheetTable RewriteStructuredReferences(
        SpreadsheetTable table,
        Func<string, string> rewrite)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(rewrite);
        var changed = false;
        var columns = new SpreadsheetTableColumn[table.Columns.Count];
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var calculated = RewriteOptional(
                column.CalculatedColumnFormula,
                rewrite);
            var totals = RewriteOptional(
                column.TotalsRowFormula,
                rewrite);
            changed |= !string.Equals(
                           calculated,
                           column.CalculatedColumnFormula,
                           StringComparison.Ordinal) ||
                       !string.Equals(
                           totals,
                           column.TotalsRowFormula,
                           StringComparison.Ordinal);
            columns[index] = new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                calculated,
                totals,
                column.TotalsRowLabel);
        }

        return changed
            ? table.WithColumnsAndRange(
                columns,
                table.Range,
                table.AutoFilter)
            : table;
    }

    private static SpreadsheetTable RefreshTableMetadata(
        Worksheet worksheet,
        SpreadsheetTable table)
    {
        var changed = false;
        var columns = new SpreadsheetTableColumn[table.Columns.Count];
        for (var columnIndex = 0;
             columnIndex < table.Columns.Count;
             columnIndex++)
        {
            var column = table.Columns[columnIndex];
            var calculated = column.CalculatedColumnFormula;
            if (calculated is not null &&
                table.DataRange is { } dataRange)
            {
                var anchor = new CellAddress(
                    dataRange.Top,
                    table.Range.Left + columnIndex);
                var candidates = new Dictionary<
                    string,
                    FormulaCandidate>(StringComparer.Ordinal);
                for (var row = dataRange.Top;
                     row <= dataRange.Bottom;
                     row++)
                {
                    var address = new CellAddress(
                        row,
                        anchor.ColumnIndex);
                    if (worksheet.GetCell(address).Formula is not { } formula)
                    {
                        continue;
                    }

                    var normalized = A1FormulaReferenceTranslator.Translate(
                        formula,
                        address,
                        anchor);
                    if (candidates.TryGetValue(
                            normalized,
                            out var candidate))
                    {
                        candidates[normalized] = candidate with
                        {
                            Count = candidate.Count + 1,
                        };
                    }
                    else
                    {
                        candidates.Add(
                            normalized,
                            new FormulaCandidate(1, row));
                    }
                }

                if (candidates.Count > 0)
                {
                    calculated = candidates
                        .OrderByDescending(static pair =>
                            pair.Value.Count)
                        .ThenBy(static pair =>
                            pair.Value.FirstRow)
                        .Select(static pair => pair.Key)
                        .First();
                }
            }

            var totals = column.TotalsRowFormula;
            if (totals is not null && table.HasTotalsRow)
            {
                var totalsAddress = new CellAddress(
                    table.Range.Bottom,
                    table.Range.Left + columnIndex);
                totals = worksheet.GetCell(totalsAddress).Formula ??
                    totals;
            }

            changed |= !string.Equals(
                           calculated,
                           column.CalculatedColumnFormula,
                           StringComparison.Ordinal) ||
                       !string.Equals(
                           totals,
                           column.TotalsRowFormula,
                           StringComparison.Ordinal);
            columns[columnIndex] = new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                calculated,
                totals,
                column.TotalsRowLabel);
        }

        return changed
            ? table.WithColumnsAndRange(
                columns,
                table.Range,
                table.AutoFilter)
            : table;
    }

    private static void ProjectCore(
        Worksheet worksheet,
        SpreadsheetTable table)
    {
        var updates = new Dictionary<CellAddress, CellData>();
        if (table.DataRange is { } dataRange)
        {
            for (var columnIndex = 0;
                 columnIndex < table.Columns.Count;
                 columnIndex++)
            {
                var column = table.Columns[columnIndex];
                if (column.CalculatedColumnFormula is not { } formula)
                {
                    continue;
                }

                var source = new CellAddress(
                    dataRange.Top,
                    table.Range.Left + columnIndex);
                for (var row = dataRange.Top;
                     row <= dataRange.Bottom;
                     row++)
                {
                    var address = new CellAddress(
                        row,
                        source.ColumnIndex);
                    var translated = A1FormulaReferenceTranslator.Translate(
                        formula,
                        source,
                        address);
                    var current = worksheet.GetCell(address);
                    updates[address] = new CellData(
                        string.Equals(
                            current.Formula,
                            translated,
                            StringComparison.Ordinal)
                            ? current.Value
                            : CellValue.Blank,
                        translated,
                        current.StyleId);
                }
            }
        }

        if (table.HasTotalsRow)
        {
            for (var columnIndex = 0;
                 columnIndex < table.Columns.Count;
                 columnIndex++)
            {
                var column = table.Columns[columnIndex];
                var address = new CellAddress(
                    table.Range.Bottom,
                    table.Range.Left + columnIndex);
                var current = worksheet.GetCell(address);
                if (column.TotalsRowFormula is { } formula)
                {
                    updates[address] = new CellData(
                        string.Equals(
                            current.Formula,
                            formula,
                            StringComparison.Ordinal)
                            ? current.Value
                            : CellValue.Blank,
                        formula,
                        current.StyleId);
                }
                else if (column.TotalsRowLabel is { } label)
                {
                    updates[address] = new CellData(
                        CellValue.FromText(label),
                        styleId: current.StyleId);
                }
            }
        }

        if (updates.Count > 0)
        {
            worksheet.SetCells(updates);
        }
    }

    private static void ClearRemovedMetadata(
        Worksheet worksheet,
        SpreadsheetTable previous,
        SpreadsheetTable next)
    {
        var updates = new Dictionary<CellAddress, CellData>();
        var nextColumns = next.Columns.ToDictionary(
            static column => column.Id);
        if (previous.DataRange is { } previousDataRange)
        {
            for (var columnIndex = 0;
                 columnIndex < previous.Columns.Count;
                 columnIndex++)
            {
                var previousColumn = previous.Columns[columnIndex];
                if (previousColumn.CalculatedColumnFormula is null ||
                    nextColumns.TryGetValue(
                        previousColumn.Id,
                        out var nextColumn) &&
                    nextColumn.CalculatedColumnFormula is not null)
                {
                    continue;
                }

                for (var row = previousDataRange.Top;
                     row <= previousDataRange.Bottom;
                     row++)
                {
                    var address = new CellAddress(
                        row,
                        previous.Range.Left + columnIndex);
                    var current = worksheet.GetCell(address);
                    if (current.Formula is not null)
                    {
                        updates[address] = new CellData(
                            current.Value,
                            styleId: current.StyleId);
                    }
                }
            }
        }

        if (previous.HasTotalsRow)
        {
            for (var columnIndex = 0;
                 columnIndex < previous.Columns.Count;
                 columnIndex++)
            {
                var previousColumn = previous.Columns[columnIndex];
                nextColumns.TryGetValue(
                    previousColumn.Id,
                    out var nextColumn);
                var address = new CellAddress(
                    previous.Range.Bottom,
                    previous.Range.Left + columnIndex);
                var current = worksheet.GetCell(address);
                if (previousColumn.TotalsRowFormula is not null &&
                    nextColumn?.TotalsRowFormula is null &&
                    current.Formula is not null)
                {
                    updates[address] = new CellData(
                        current.Value,
                        styleId: current.StyleId);
                }
                else if (previousColumn.TotalsRowLabel is { } previousLabel &&
                         nextColumn?.TotalsRowLabel is null &&
                         current.Formula is null &&
                         current.Value.Kind == CellValueKind.Text &&
                         string.Equals(
                             current.Value.ToString(),
                             previousLabel,
                             StringComparison.Ordinal))
                {
                    updates[address] = new CellData(
                        CellValue.Blank,
                        styleId: current.StyleId);
                }
            }
        }

        if (updates.Count > 0)
        {
            worksheet.SetCells(updates);
        }
    }

    private static SpreadsheetTable ReplaceColumn(
        SpreadsheetTable table,
        Guid columnId,
        Func<SpreadsheetTableColumn, SpreadsheetTableColumn> replace)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(replace);
        var index = table.GetColumnIndex(columnId);
        var columns = table.Columns
            .Select(static column => column.Copy())
            .ToArray();
        columns[index] = replace(columns[index]);
        return table.WithColumnsAndRange(
            columns,
            table.Range,
            table.AutoFilter);
    }

    private static void RewriteA1Metadata(
        Worksheet worksheet,
        Func<string, string> rewrite,
        CellRange signalRange)
    {
        var changed = false;
        var tables = worksheet.Tables
            .Select(table =>
            {
                var rewritten = RewriteStructuredReferences(
                    table,
                    rewrite);
                changed |= !ReferenceEquals(rewritten, table);
                return rewritten;
            })
            .ToArray();
        if (changed)
        {
            worksheet.RestoreTables(tables, signalRange);
        }
    }

    private static void ValidateProjectionSize(
        SpreadsheetTable[] tables)
    {
        long projected = 0L;
        foreach (var table in tables)
        {
            if (table.DataRange is { } dataRange)
            {
                projected += (long)dataRange.RowCount *
                    table.Columns.Count(column =>
                        column.CalculatedColumnFormula is not null);
            }
            if (table.HasTotalsRow)
            {
                projected += table.Columns.Count(column =>
                    column.TotalsRowFormula is not null ||
                    column.TotalsRowLabel is not null);
            }
            if (projected > MaxProjectedFormulaCells)
            {
                throw new InvalidOperationException(
                    $"Table formula projection is limited to {MaxProjectedFormulaCells} cells per operation.");
            }
        }
    }

    private static string? RewriteOptional(
        string? formula,
        Func<string, string> rewrite) =>
        formula is null
            ? null
            : rewrite(formula);

    private static string NormalizeFormula(string formula)
    {
        var normalized = formula.Trim();
        return normalized.StartsWith('=')
            ? normalized
            : $"={normalized}";
    }

    private static CellRange Union(
        IEnumerable<CellRange> ranges)
    {
        var materialized = ranges.ToArray();
        if (materialized.Length == 0)
        {
            return new CellRange(default, default);
        }
        return new CellRange(
            new CellAddress(
                materialized.Min(static range => range.Top),
                materialized.Min(static range => range.Left)),
            new CellAddress(
                materialized.Max(static range => range.Bottom),
                materialized.Max(static range => range.Right)));
    }

    private static CellRange Union(
        CellRange left,
        CellRange right) =>
        new(
            new CellAddress(
                Math.Min(left.Top, right.Top),
                Math.Min(left.Left, right.Left)),
            new CellAddress(
                Math.Max(left.Bottom, right.Bottom),
                Math.Max(left.Right, right.Right)));

    private readonly record struct FormulaCandidate(
        int Count,
        int FirstRow);
}
