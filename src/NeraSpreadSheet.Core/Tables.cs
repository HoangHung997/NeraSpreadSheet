using System.Globalization;

namespace NeraSpreadSheet.Core;

public enum TableReferenceArea
{
    All = 0,
    Data,
    Headers,
    Totals,
    ThisRow,
}

public enum TableFilterComparisonOperator
{
    Equal = 0,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    BeginsWith,
    EndsWith,
    Contains,
    DoesNotContain,
    IsBlank,
    IsNotBlank,
    OnDate,
    BeforeDate,
    AfterDate,
    ThisWeek,
    LastWeek,
    NextWeek,
    ThisMonth,
    LastMonth,
    NextMonth,
    ThisYear,
    LastYear,
    NextYear,
}

public sealed record TableFilterCondition(
    TableFilterComparisonOperator Operator,
    CellValue Value);

public sealed class TableFilterColumn
{
    private readonly CellValue[] _values;
    private readonly SpreadsheetFilterDateGroup[] _dateGroups;

    public TableFilterColumn(
        Guid columnId,
        IEnumerable<CellValue>? values = null,
        bool includeBlank = false,
        TableFilterCondition? firstCondition = null,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true,
        IEnumerable<SpreadsheetFilterDateGroup>? dateGroups = null,
        SpreadsheetTopBottomFilter? topBottom = null,
        SpreadsheetDynamicFilter? dynamicFilter = null,
        SpreadsheetColorFilter? colorFilter = null,
        SpreadsheetIconFilter? iconFilter = null)
    {
        if (columnId == Guid.Empty)
        {
            throw new ArgumentException(
                "A table-filter column identifier cannot be empty.",
                nameof(columnId));
        }

        ColumnId = columnId;
        _values = values?.Distinct().ToArray() ?? [];
        _dateGroups = dateGroups?.Distinct().ToArray() ?? [];
        IncludeBlank = includeBlank;
        FirstCondition = firstCondition;
        SecondCondition = secondCondition;
        CombineWithAnd = combineWithAnd;
        TopBottom = topBottom;
        DynamicFilter = dynamicFilter;
        ColorFilter = colorFilter;
        IconFilter = iconFilter;
        var definitionCount = (_values.Length > 0 || includeBlank || _dateGroups.Length > 0 ? 1 : 0) +
                              (firstCondition is not null ? 1 : 0) +
                              (topBottom is not null ? 1 : 0) +
                              (dynamicFilter is not null ? 1 : 0) +
                              (colorFilter is not null ? 1 : 0) +
                              (iconFilter is not null ? 1 : 0);
        if (definitionCount == 0)
        {
            throw new ArgumentException(
                "A table filter requires one filter definition.",
                nameof(values));
        }
        if (definitionCount > 1)
        {
            throw new ArgumentException(
                "A filter column cannot combine different filter definition kinds.",
                nameof(values));
        }
        if (secondCondition is not null && firstCondition is null)
        {
            throw new ArgumentException(
                "A second custom condition requires a first condition.",
                nameof(secondCondition));
        }
    }

    public Guid ColumnId { get; }

    public IReadOnlyList<CellValue> Values => _values;

    public bool IncludeBlank { get; }

    public TableFilterCondition? FirstCondition { get; }

    public TableFilterCondition? SecondCondition { get; }

    public bool CombineWithAnd { get; }

    /// <summary>Gets date-group selections combined with value selections by OR.</summary>
    public IReadOnlyList<SpreadsheetFilterDateGroup> DateGroups => _dateGroups;

    /// <summary>Gets the Top/Bottom criterion, when present.</summary>
    public SpreadsheetTopBottomFilter? TopBottom { get; }

    /// <summary>Gets the dynamic date or average criterion, when present.</summary>
    public SpreadsheetDynamicFilter? DynamicFilter { get; }

    /// <summary>Gets the resolved cell color criterion, when present.</summary>
    public SpreadsheetColorFilter? ColorFilter { get; }

    /// <summary>Gets the icon-set criterion, when present.</summary>
    public SpreadsheetIconFilter? IconFilter { get; }

    public bool Matches(CellValue value) => Matches(value, ExcelDateSystem.Date1900);

    internal bool Matches(CellValue value, ExcelDateSystem dateSystem)
    {
        if (_values.Length > 0 || _dateGroups.Length > 0)
        {
            if (value.IsBlank)
            {
                return IncludeBlank ||
                       _values.Any(static candidate => candidate.IsBlank);
            }

            if (_values.Any(candidate =>
                    TableValueComparer.Compare(candidate, value) == 0))
            {
                return true;
            }

            return SpreadsheetFilterDate.TryGetDate(
                       value,
                       dateSystem,
                       out var date) &&
                   _dateGroups.Any(group => group.Matches(date));
        }

        if (value.IsBlank && IncludeBlank)
        {
            return true;
        }

        if (DynamicFilter is not null)
        {
            return SpreadsheetFilterEvaluator.MatchesDynamic(
                value,
                DynamicFilter,
                dateSystem,
                aggregateAverage: null);
        }

        var first = FirstCondition is not null &&
                    MatchesCondition(value, FirstCondition, dateSystem);
        if (SecondCondition is null)
        {
            return first;
        }

        var second = MatchesCondition(value, SecondCondition, dateSystem);
        return CombineWithAnd
            ? first && second
            : first || second;
    }

    public TableFilterColumn Copy() => new(
        ColumnId,
        _values,
        IncludeBlank,
        FirstCondition,
        SecondCondition,
        CombineWithAnd,
        _dateGroups,
        TopBottom,
        DynamicFilter,
        ColorFilter,
        IconFilter);

    private static bool MatchesCondition(
        CellValue value,
        TableFilterCondition condition,
        ExcelDateSystem dateSystem) =>
        SpreadsheetFilterPredicate.Matches(value, condition, dateSystem);
}

internal static class SpreadsheetFilterPredicate
{
    public static bool Matches(
        CellValue value,
        TableFilterCondition condition,
        ExcelDateSystem dateSystem = ExcelDateSystem.Date1900)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (condition.Operator == TableFilterComparisonOperator.IsBlank)
        {
            return value.IsBlank;
        }
        if (condition.Operator == TableFilterComparisonOperator.IsNotBlank)
        {
            return !value.IsBlank;
        }
        if (value.IsBlank)
        {
            return false;
        }

        if (condition.Operator is
            TableFilterComparisonOperator.BeginsWith or
            TableFilterComparisonOperator.EndsWith or
            TableFilterComparisonOperator.Contains or
            TableFilterComparisonOperator.DoesNotContain)
        {
            var source = GetText(value);
            var requested = GetText(condition.Value);
            return condition.Operator switch
            {
                TableFilterComparisonOperator.BeginsWith =>
                    source.StartsWith(
                        requested,
                        StringComparison.OrdinalIgnoreCase),
                TableFilterComparisonOperator.EndsWith =>
                    source.EndsWith(
                        requested,
                        StringComparison.OrdinalIgnoreCase),
                TableFilterComparisonOperator.Contains =>
                    source.Contains(
                        requested,
                        StringComparison.OrdinalIgnoreCase),
                TableFilterComparisonOperator.DoesNotContain =>
                    !source.Contains(
                        requested,
                        StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        if (condition.Operator is
            TableFilterComparisonOperator.OnDate or
            TableFilterComparisonOperator.BeforeDate or
            TableFilterComparisonOperator.AfterDate or
            TableFilterComparisonOperator.ThisWeek or
            TableFilterComparisonOperator.LastWeek or
            TableFilterComparisonOperator.NextWeek or
            TableFilterComparisonOperator.ThisMonth or
            TableFilterComparisonOperator.LastMonth or
            TableFilterComparisonOperator.NextMonth or
            TableFilterComparisonOperator.ThisYear or
            TableFilterComparisonOperator.LastYear or
            TableFilterComparisonOperator.NextYear)
        {
            return TryGetDate(value, dateSystem, out var candidate) &&
                   TryGetDate(condition.Value, dateSystem, out var reference) &&
                   MatchesDate(
                       candidate.Date,
                       reference.Date,
                       condition.Operator);
        }

        var comparison = TableValueComparer.Compare(
            value,
            condition.Value);
        return condition.Operator switch
        {
            TableFilterComparisonOperator.Equal => comparison == 0,
            TableFilterComparisonOperator.NotEqual => comparison != 0,
            TableFilterComparisonOperator.GreaterThan => comparison > 0,
            TableFilterComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            TableFilterComparisonOperator.LessThan => comparison < 0,
            TableFilterComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => false,
        };
    }

    private static string GetText(CellValue value) =>
        value.Kind == CellValueKind.Text
            ? (string)value.RawValue!
            : value.ToString();

    private static bool TryGetDate(
        CellValue value,
        ExcelDateSystem dateSystem,
        out DateTime date)
    {
        switch (value.Kind)
        {
            case CellValueKind.DateTime:
                date = (DateTime)value.RawValue!;
                return true;
            case CellValueKind.Number:
                try
                {
                    date = dateSystem == ExcelDateSystem.Date1904
                        ? new DateTime(1904, 1, 1).AddDays((double)value.RawValue!)
                        : DateTime.FromOADate(
                            (double)value.RawValue! < 60d
                                ? (double)value.RawValue! + 1d
                                : (double)value.RawValue!);
                    return true;
                }
                catch (ArgumentException)
                {
                    date = default;
                    return false;
                }
            case CellValueKind.Text:
                return DateTime.TryParse(
                    (string)value.RawValue!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeLocal,
                    out date);
            default:
                date = default;
                return false;
        }
    }

    private static bool MatchesDate(
        DateTime candidate,
        DateTime reference,
        TableFilterComparisonOperator comparisonOperator)
    {
        if (comparisonOperator == TableFilterComparisonOperator.OnDate)
        {
            return candidate == reference;
        }
        if (comparisonOperator == TableFilterComparisonOperator.BeforeDate)
        {
            return candidate < reference;
        }
        if (comparisonOperator == TableFilterComparisonOperator.AfterDate)
        {
            return candidate > reference;
        }

        var (start, end) = comparisonOperator switch
        {
            TableFilterComparisonOperator.ThisWeek =>
                CreateWeekWindow(reference, 0),
            TableFilterComparisonOperator.LastWeek =>
                CreateWeekWindow(reference, -1),
            TableFilterComparisonOperator.NextWeek =>
                CreateWeekWindow(reference, 1),
            TableFilterComparisonOperator.ThisMonth =>
                CreateMonthWindow(reference, 0),
            TableFilterComparisonOperator.LastMonth =>
                CreateMonthWindow(reference, -1),
            TableFilterComparisonOperator.NextMonth =>
                CreateMonthWindow(reference, 1),
            TableFilterComparisonOperator.ThisYear =>
                CreateYearWindow(reference, 0),
            TableFilterComparisonOperator.LastYear =>
                CreateYearWindow(reference, -1),
            TableFilterComparisonOperator.NextYear =>
                CreateYearWindow(reference, 1),
            _ => (DateTime.MaxValue, DateTime.MinValue),
        };
        return candidate >= start && candidate < end;
    }

    private static (DateTime Start, DateTime End)
        CreateWeekWindow(DateTime reference, int offset)
    {
        var daysSinceMonday =
            ((int)reference.DayOfWeek -
             (int)DayOfWeek.Monday + 7) % 7;
        var start = reference
            .AddDays(-daysSinceMonday + (offset * 7))
            .Date;
        return (start, start.AddDays(7));
    }

    private static (DateTime Start, DateTime End)
        CreateMonthWindow(DateTime reference, int offset)
    {
        var start = new DateTime(
            reference.Year,
            reference.Month,
            1).AddMonths(offset);
        return (start, start.AddMonths(1));
    }

    private static (DateTime Start, DateTime End)
        CreateYearWindow(DateTime reference, int offset)
    {
        var start = new DateTime(
            reference.Year + offset,
            1,
            1);
        return (start, start.AddYears(1));
    }
}

public sealed class TableAutoFilter
{
    private readonly TableFilterColumn[] _columns;

    public TableAutoFilter(
        IEnumerable<TableFilterColumn> columns,
        SpreadsheetFilterSortState? sortState = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns
            .Select(static column =>
                (column ?? throw new ArgumentException(
                    "A table filter cannot contain a null column.",
                    nameof(columns))).Copy())
            .ToArray();
        if (_columns.Select(static column => column.ColumnId)
            .Distinct()
            .Count() != _columns.Length)
        {
            throw new ArgumentException(
                "A table filter cannot contain duplicate column identifiers.",
                nameof(columns));
        }
        SortState = sortState?.Copy();
    }

    public IReadOnlyList<TableFilterColumn> Columns => _columns;

    /// <summary>Gets the optional sort metadata for the Table data range.</summary>
    public SpreadsheetFilterSortState? SortState { get; }

    public TableAutoFilter Copy() => new(_columns, SortState);

    /// <summary>Returns a copy with replacement sort metadata.</summary>
    public TableAutoFilter WithSortState(SpreadsheetFilterSortState? sortState) =>
        new(_columns, sortState);

    public TableAutoFilter WithoutColumns(
        IReadOnlySet<Guid> removedColumnIds) =>
        new(_columns.Where(column =>
            !removedColumnIds.Contains(column.ColumnId)), SortState);
}

public sealed class SpreadsheetTableColumn
{
    public SpreadsheetTableColumn(
        Guid id,
        string name,
        string? calculatedColumnFormula = null,
        string? totalsRowFormula = null,
        string? totalsRowLabel = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A table-column identifier cannot be empty.",
                nameof(id));
        }

        Id = id;
        Name = TableNameRules.ValidateColumnName(name);
        CalculatedColumnFormula = NormalizeFormula(
            calculatedColumnFormula);
        TotalsRowFormula = NormalizeFormula(totalsRowFormula);
        TotalsRowLabel = NormalizeOptionalText(
            totalsRowLabel,
            SpreadsheetTable.MaxColumnNameLength,
            nameof(totalsRowLabel));
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? CalculatedColumnFormula { get; }

    public string? TotalsRowFormula { get; }

    public string? TotalsRowLabel { get; }

    public SpreadsheetTableColumn Copy() => new(
        Id,
        Name,
        CalculatedColumnFormula,
        TotalsRowFormula,
        TotalsRowLabel);

    public SpreadsheetTableColumn Rename(string name) => new(
        Id,
        name,
        CalculatedColumnFormula,
        TotalsRowFormula,
        TotalsRowLabel);

    internal SpreadsheetTableColumn RewriteA1References(
        string currentWorksheetName,
        WorksheetStructuralChange change) =>
        new(
            Id,
            Name,
            RewriteFormula(
                CalculatedColumnFormula,
                currentWorksheetName,
                change),
            RewriteFormula(
                TotalsRowFormula,
                currentWorksheetName,
                change),
            TotalsRowLabel);

    private static string? RewriteFormula(
        string? formula,
        string currentWorksheetName,
        WorksheetStructuralChange change) =>
        formula is null
            ? null
            : FormulaStructuralReferenceRewriter.Rewrite(
                formula,
                currentWorksheetName,
                currentWorksheetName,
                change);

    private static string? NormalizeFormula(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var normalized = formula.Trim();
        if (normalized.Length > SpreadsheetTable.MaxFormulaLength)
        {
            throw new ArgumentException(
                $"Table formulas cannot exceed {SpreadsheetTable.MaxFormulaLength} characters.",
                nameof(formula));
        }

        return normalized.StartsWith('=')
            ? normalized
            : $"={normalized}";
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}

public sealed class SpreadsheetTable
{
    public const int MaxNameLength = 255;
    public const int MaxColumnNameLength = 255;
    public const int MaxFormulaLength = 8192;

    private readonly SpreadsheetTableColumn[] _columns;

    public SpreadsheetTable(
        Guid id,
        string name,
        CellRange range,
        IEnumerable<SpreadsheetTableColumn> columns,
        bool hasHeaders = true,
        bool hasTotalsRow = false,
        string? styleName = "TableStyleMedium2",
        bool showFirstColumn = false,
        bool showLastColumn = false,
        bool showRowStripes = true,
        bool showColumnStripes = false,
        TableAutoFilter? autoFilter = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A table identifier cannot be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(columns);
        Id = id;
        Name = TableNameRules.ValidateTableName(name);
        Range = range;
        HasHeaders = hasHeaders;
        HasTotalsRow = hasTotalsRow;
        StyleName = NormalizeStyleName(styleName);
        ShowFirstColumn = showFirstColumn;
        ShowLastColumn = showLastColumn;
        ShowRowStripes = showRowStripes;
        ShowColumnStripes = showColumnStripes;
        _columns = columns
            .Select(static column =>
                (column ?? throw new ArgumentException(
                    "A table cannot contain a null column.",
                    nameof(columns))).Copy())
            .ToArray();
        if (_columns.Length == 0)
        {
            throw new ArgumentException(
                "A table requires at least one column.",
                nameof(columns));
        }
        if (_columns.Length != range.ColumnCount)
        {
            throw new ArgumentException(
                "The table-column count must match the table range width.",
                nameof(columns));
        }
        if (_columns.Select(static column => column.Id)
            .Distinct()
            .Count() != _columns.Length)
        {
            throw new ArgumentException(
                "Table-column identifiers must be unique.",
                nameof(columns));
        }
        if (_columns.Select(static column => column.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != _columns.Length)
        {
            throw new ArgumentException(
                "Table-column names must be unique without regard to case.",
                nameof(columns));
        }

        var metadataRows = (hasHeaders ? 1 : 0) +
                           (hasTotalsRow ? 1 : 0);
        if (range.RowCount < Math.Max(1, metadataRows))
        {
            throw new ArgumentException(
                "The table range is too small for its header/totals configuration.",
                nameof(range));
        }

        if (autoFilter is not null)
        {
            var knownIds = _columns
                .Select(static column => column.Id)
                .ToHashSet();
            if (autoFilter.Columns.Any(column =>
                    !knownIds.Contains(column.ColumnId)))
            {
                throw new ArgumentException(
                    "The table filter references an unknown table column.",
                    nameof(autoFilter));
            }
            if (autoFilter.SortState?.Conditions.Any(condition =>
                    condition.ColumnOffset >= range.ColumnCount) == true)
            {
                throw new ArgumentException(
                    "A table sort condition must be inside the table range.",
                    nameof(autoFilter));
            }
        }

        AutoFilter = autoFilter?.Copy();
    }

    public Guid Id { get; }

    public string Name { get; }

    public CellRange Range { get; }

    public bool HasHeaders { get; }

    public bool HasTotalsRow { get; }

    public string? StyleName { get; }

    public bool ShowFirstColumn { get; }

    public bool ShowLastColumn { get; }

    public bool ShowRowStripes { get; }

    public bool ShowColumnStripes { get; }

    public TableAutoFilter? AutoFilter { get; }

    public IReadOnlyList<SpreadsheetTableColumn> Columns => _columns;

    public CellRange? HeaderRange => HasHeaders
        ? new CellRange(
            new CellAddress(Range.Top, Range.Left),
            new CellAddress(Range.Top, Range.Right))
        : null;

    public CellRange? TotalsRange => HasTotalsRow
        ? new CellRange(
            new CellAddress(Range.Bottom, Range.Left),
            new CellAddress(Range.Bottom, Range.Right))
        : null;

    public CellRange? DataRange
    {
        get
        {
            var top = Range.Top + (HasHeaders ? 1 : 0);
            var bottom = Range.Bottom - (HasTotalsRow ? 1 : 0);
            return top <= bottom
                ? new CellRange(
                    new CellAddress(top, Range.Left),
                    new CellAddress(bottom, Range.Right))
                : null;
        }
    }

    public SpreadsheetTable Copy() => new(
        Id,
        Name,
        Range,
        _columns,
        HasHeaders,
        HasTotalsRow,
        StyleName,
        ShowFirstColumn,
        ShowLastColumn,
        ShowRowStripes,
        ShowColumnStripes,
        AutoFilter);

    public SpreadsheetTable Rename(string name) => new(
        Id,
        name,
        Range,
        _columns,
        HasHeaders,
        HasTotalsRow,
        StyleName,
        ShowFirstColumn,
        ShowLastColumn,
        ShowRowStripes,
        ShowColumnStripes,
        AutoFilter);

    public SpreadsheetTable RenameColumn(
        Guid columnId,
        string name)
    {
        var index = Array.FindIndex(
            _columns,
            column => column.Id == columnId);
        if (index < 0)
        {
            throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        }

        var replacement = _columns
            .Select((column, candidateIndex) =>
                candidateIndex == index
                    ? column.Rename(name)
                    : column.Copy())
            .ToArray();
        return WithColumnsAndRange(replacement, Range, AutoFilter);
    }

    public SpreadsheetTable WithAutoFilter(
        TableAutoFilter? autoFilter) =>
        new(
            Id,
            Name,
            Range,
            _columns,
            HasHeaders,
            HasTotalsRow,
            StyleName,
            ShowFirstColumn,
            ShowLastColumn,
            ShowRowStripes,
            ShowColumnStripes,
            autoFilter);

    public bool TryGetColumn(
        string name,
        out SpreadsheetTableColumn? column)
    {
        column = _columns.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
        return column is not null;
    }

    public bool TryGetColumn(
        Guid id,
        out SpreadsheetTableColumn? column)
    {
        column = _columns.FirstOrDefault(candidate =>
            candidate.Id == id);
        return column is not null;
    }

    public int GetColumnIndex(Guid columnId)
    {
        var index = Array.FindIndex(
            _columns,
            column => column.Id == columnId);
        if (index < 0)
        {
            throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        }

        return index;
    }

    public bool TryGetReferenceRange(
        TableReferenceArea area,
        Guid? columnId,
        int currentRow,
        out CellRange range)
    {
        var left = Range.Left;
        var right = Range.Right;
        if (columnId is Guid requestedColumnId)
        {
            var columnIndex = GetColumnIndex(requestedColumnId);
            left = Range.Left + columnIndex;
            right = left;
        }

        switch (area)
        {
            case TableReferenceArea.All:
                range = new CellRange(
                    new CellAddress(Range.Top, left),
                    new CellAddress(Range.Bottom, right));
                return true;
            case TableReferenceArea.Data:
                if (DataRange is not { } dataRange)
                {
                    range = default;
                    return false;
                }
                range = new CellRange(
                    new CellAddress(dataRange.Top, left),
                    new CellAddress(dataRange.Bottom, right));
                return true;
            case TableReferenceArea.Headers:
                if (!HasHeaders)
                {
                    range = default;
                    return false;
                }
                range = new CellRange(
                    new CellAddress(Range.Top, left),
                    new CellAddress(Range.Top, right));
                return true;
            case TableReferenceArea.Totals:
                if (!HasTotalsRow)
                {
                    range = default;
                    return false;
                }
                range = new CellRange(
                    new CellAddress(Range.Bottom, left),
                    new CellAddress(Range.Bottom, right));
                return true;
            case TableReferenceArea.ThisRow:
                if (DataRange is not { } currentDataRange ||
                    currentRow < currentDataRange.Top ||
                    currentRow > currentDataRange.Bottom)
                {
                    range = default;
                    return false;
                }
                range = new CellRange(
                    new CellAddress(currentRow, left),
                    new CellAddress(currentRow, right));
                return true;
            default:
                range = default;
                return false;
        }
    }

    public bool IsRowVisible(
        WorksheetSnapshot worksheet,
        int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (DataRange is not { } dataRange ||
            rowIndex < dataRange.Top ||
            rowIndex > dataRange.Bottom ||
            AutoFilter is null)
        {
            return true;
        }

        foreach (var filter in AutoFilter.Columns)
        {
            var columnIndex = GetColumnIndex(filter.ColumnId);
            if (!worksheet.MatchesFilter(
                    dataRange,
                    Range.Left + columnIndex,
                    filter,
                    rowIndex))
            {
                return false;
            }
        }

        return true;
    }

    internal SpreadsheetTable WithColumnsAndRange(
        IEnumerable<SpreadsheetTableColumn> columns,
        CellRange range,
        TableAutoFilter? autoFilter) =>
        new(
            Id,
            Name,
            range,
            columns,
            HasHeaders,
            HasTotalsRow,
            StyleName,
            ShowFirstColumn,
            ShowLastColumn,
            ShowRowStripes,
            ShowColumnStripes,
            autoFilter);

    internal SpreadsheetTable RewriteA1References(
        string currentWorksheetName,
        WorksheetStructuralChange change) =>
        WithColumnsAndRange(
            _columns.Select(column =>
                column.RewriteA1References(
                    currentWorksheetName,
                    change)),
            Range,
            AutoFilter);

    private static string? NormalizeStyleName(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        var normalized = styleName.Trim();
        if (normalized.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Table style names cannot exceed {MaxNameLength} characters.",
                nameof(styleName));
        }

        return normalized;
    }
}

internal sealed class WorksheetTableCollection
{
    public const int MaxTablesPerWorksheet = 4096;

    private readonly List<SpreadsheetTable> _tables = [];

    public int Count => _tables.Count;

    public IReadOnlyList<SpreadsheetTable> Tables => _tables;

    public void Add(SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (_tables.Count >= MaxTablesPerWorksheet)
        {
            throw new InvalidOperationException(
                $"A worksheet cannot contain more than {MaxTablesPerWorksheet} tables.");
        }
        if (_tables.Any(candidate => candidate.Id == table.Id))
        {
            throw new InvalidOperationException(
                $"A table with identifier '{table.Id}' already exists.");
        }
        if (_tables.Any(candidate => string.Equals(
                candidate.Name,
                table.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A table named '{table.Name}' already exists on this worksheet.");
        }
        if (_tables.Any(candidate => candidate.Range.Intersects(table.Range)))
        {
            throw new InvalidOperationException(
                "Worksheet tables cannot overlap.");
        }

        _tables.Add(table.Copy());
        Sort();
    }

    public bool Remove(Guid id, out SpreadsheetTable? removed)
    {
        var index = _tables.FindIndex(table => table.Id == id);
        if (index < 0)
        {
            removed = null;
            return false;
        }

        removed = _tables[index];
        _tables.RemoveAt(index);
        return true;
    }

    public bool TryGet(string name, out SpreadsheetTable? table)
    {
        table = _tables.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            name,
            StringComparison.OrdinalIgnoreCase));
        return table is not null;
    }

    public bool TryGet(Guid id, out SpreadsheetTable? table)
    {
        table = _tables.FirstOrDefault(candidate => candidate.Id == id);
        return table is not null;
    }

    public bool TryGet(CellAddress address, out SpreadsheetTable? table)
    {
        table = _tables.FirstOrDefault(candidate =>
            candidate.Range.Contains(address));
        return table is not null;
    }

    public SpreadsheetTable[] Capture() =>
        _tables.Select(static table => table.Copy()).ToArray();

    public void Restore(IEnumerable<SpreadsheetTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var materialized = tables
            .Select(static table =>
                (table ?? throw new InvalidDataException(
                    "A table collection cannot contain a null table.")).Copy())
            .ToArray();
        ValidateSet(materialized);
        _tables.Clear();
        _tables.AddRange(materialized);
        Sort();
    }

    public SpreadsheetTable[] CreateStructuralTables(
        WorksheetStructuralChange change,
        string worksheetName)
    {
        var result = new List<SpreadsheetTable>(_tables.Count);
        foreach (var table in _tables)
        {
            var transformed = Transform(table, change, worksheetName);
            if (transformed is not null)
            {
                result.Add(transformed);
            }
        }

        ValidateSet(result);
        return result.ToArray();
    }

    public SpreadsheetTable[] CreateAxisMoveTables(
        WorksheetAxisMove move)
    {
        var result = new List<SpreadsheetTable>(_tables.Count);
        foreach (var table in _tables)
        {
            if (!move.TryMapUniformRange(table.Range, out var mappedRange))
            {
                throw new InvalidOperationException(
                    $"Cannot reorder because table '{table.Name}' would not remain one uniform translation.");
            }

            result.Add(table.WithColumnsAndRange(
                table.Columns,
                mappedRange,
                table.AutoFilter));
        }

        ValidateSet(result);
        return result.ToArray();
    }

    public CellRange ExpandSignalRange(CellRange source)
    {
        var related = _tables
            .Where(table => table.Range.Intersects(source))
            .Select(static table => table.Range)
            .ToArray();
        if (related.Length == 0)
        {
            return source;
        }

        return new CellRange(
            new CellAddress(
                Math.Min(source.Top, related.Min(static range => range.Top)),
                Math.Min(source.Left, related.Min(static range => range.Left))),
            new CellAddress(
                Math.Max(source.Bottom, related.Max(static range => range.Bottom)),
                Math.Max(source.Right, related.Max(static range => range.Right))));
    }

    private static SpreadsheetTable? Transform(
        SpreadsheetTable table,
        WorksheetStructuralChange change,
        string worksheetName)
    {
        if (change.Axis == WorksheetAxis.Row)
        {
            ValidateProtectedRowDeletion(table, change);
            if (!change.TryMapRange(table.Range, out var mappedRange))
            {
                return null;
            }

            return table
                .RewriteA1References(worksheetName, change)
                .WithColumnsAndRange(
                    table.Columns,
                    mappedRange,
                    table.AutoFilter);
        }

        var rewritten = table.RewriteA1References(
            worksheetName,
            change);
        if (change.Kind == WorksheetStructuralChangeKind.Insert)
        {
            if (!change.TryMapRange(table.Range, out var mappedRange))
            {
                throw new InvalidOperationException(
                    $"Cannot insert columns because table '{table.Name}' would exceed worksheet bounds.");
            }

            if (change.Index <= table.Range.Left ||
                change.Index > table.Range.Right)
            {
                return rewritten.WithColumnsAndRange(
                    table.Columns,
                    mappedRange,
                    table.AutoFilter);
            }

            var insertionOffset = change.Index - table.Range.Left;
            var columns = table.Columns
                .Select(static column => column.Copy())
                .ToList();
            var existingNames = columns
                .Select(static column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < change.Count; index++)
            {
                var generatedName = TableNameRules.CreateUniqueColumnName(
                    existingNames,
                    "Column");
                existingNames.Add(generatedName);
                columns.Insert(
                    insertionOffset + index,
                    new SpreadsheetTableColumn(
                        Guid.NewGuid(),
                        generatedName));
            }

            return rewritten.WithColumnsAndRange(
                columns,
                mappedRange,
                MapTableSortForInsert(
                    table.AutoFilter,
                    insertionOffset,
                    change.Count));
        }

        var overlapStart = Math.Max(
            table.Range.Left,
            change.Index);
        var overlapEnd = Math.Min(
            table.Range.Right,
            change.EndIndex);
        if (overlapStart > overlapEnd)
        {
            if (!change.TryMapRange(table.Range, out var mappedRange))
            {
                return null;
            }

            return rewritten.WithColumnsAndRange(
                table.Columns,
                mappedRange,
                table.AutoFilter);
        }

        var removeOffset = overlapStart - table.Range.Left;
        var removeCount = overlapEnd - overlapStart + 1;
        if (removeCount == table.Columns.Count)
        {
            return null;
        }

        if (!change.TryMapRange(table.Range, out var reducedRange))
        {
            return null;
        }

        var retained = table.Columns
            .Where((_, index) =>
                index < removeOffset ||
                index >= removeOffset + removeCount)
            .Select(static column => column.Copy())
            .ToArray();
        var removedIds = table.Columns
            .Skip(removeOffset)
            .Take(removeCount)
            .Select(static column => column.Id)
            .ToHashSet();
        var filter = MapTableSortForDelete(
            table.AutoFilter?.WithoutColumns(removedIds),
            removeOffset,
            removeCount);
        return rewritten.WithColumnsAndRange(
            retained,
            reducedRange,
            filter);
    }

    private static TableAutoFilter? MapTableSortForInsert(
        TableAutoFilter? filter,
        int insertionOffset,
        int count)
    {
        if (filter?.SortState is not { } state) return filter;
        var mapped = state.Conditions.Select(condition => new SpreadsheetFilterSortCondition(
            condition.ColumnOffset >= insertionOffset ? condition.ColumnOffset + count : condition.ColumnOffset,
            condition.Descending,
            condition.SortBy,
            condition.CustomList,
            condition.Color,
            condition.Icon));
        return new TableAutoFilter(filter.Columns, new SpreadsheetFilterSortState(mapped, state.CaseSensitive, state.SortLeftToRight));
    }

    private static TableAutoFilter? MapTableSortForDelete(
        TableAutoFilter? filter,
        int removeOffset,
        int removeCount)
    {
        if (filter?.SortState is not { } state) return filter;
        var end = removeOffset + removeCount;
        var mapped = state.Conditions
            .Where(condition => condition.ColumnOffset < removeOffset || condition.ColumnOffset >= end)
            .Select(condition => new SpreadsheetFilterSortCondition(
                condition.ColumnOffset >= end ? condition.ColumnOffset - removeCount : condition.ColumnOffset,
                condition.Descending,
                condition.SortBy,
                condition.CustomList,
                condition.Color,
                condition.Icon))
            .ToArray();
        return new TableAutoFilter(
            filter.Columns,
            mapped.Length == 0 ? null : new SpreadsheetFilterSortState(mapped, state.CaseSensitive, state.SortLeftToRight));
    }

    private static void ValidateProtectedRowDeletion(
        SpreadsheetTable table,
        WorksheetStructuralChange change)
    {
        if (change.Kind != WorksheetStructuralChangeKind.Delete)
        {
            return;
        }

        var deletesEntireTable = change.Index <= table.Range.Top &&
                                 change.EndIndex >= table.Range.Bottom;
        if (deletesEntireTable)
        {
            return;
        }

        if (table.HasHeaders &&
            change.Index <= table.Range.Top &&
            change.EndIndex >= table.Range.Top)
        {
            throw new InvalidOperationException(
                $"Cannot delete the header row of table '{table.Name}' without deleting the entire table.");
        }
        if (table.HasTotalsRow &&
            change.Index <= table.Range.Bottom &&
            change.EndIndex >= table.Range.Bottom)
        {
            throw new InvalidOperationException(
                $"Cannot delete the totals row of table '{table.Name}' without deleting the entire table.");
        }
    }

    private static void ValidateSet(
        IEnumerable<SpreadsheetTable> tables)
    {
        var materialized = tables.ToArray();
        if (materialized.Length > MaxTablesPerWorksheet)
        {
            throw new InvalidDataException(
                $"A worksheet cannot contain more than {MaxTablesPerWorksheet} tables.");
        }
        if (materialized.Select(static table => table.Id)
            .Distinct()
            .Count() != materialized.Length)
        {
            throw new InvalidDataException(
                "Table identifiers must be unique on a worksheet.");
        }
        if (materialized.Select(static table => table.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != materialized.Length)
        {
            throw new InvalidDataException(
                "Table names must be unique on a worksheet.");
        }

        for (var leftIndex = 0; leftIndex < materialized.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < materialized.Length;
                 rightIndex++)
            {
                if (materialized[leftIndex].Range.Intersects(
                        materialized[rightIndex].Range))
                {
                    throw new InvalidDataException(
                        "Worksheet tables cannot overlap.");
                }
            }
        }
    }

    private void Sort() => _tables.Sort(static (left, right) =>
    {
        var row = left.Range.Top.CompareTo(right.Range.Top);
        return row != 0
            ? row
            : left.Range.Left.CompareTo(right.Range.Left);
    });
}

internal static class TableNameRules
{
    public static string ValidateTableName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > SpreadsheetTable.MaxNameLength)
        {
            throw new ArgumentException(
                $"Table names cannot exceed {SpreadsheetTable.MaxNameLength} characters.",
                nameof(name));
        }
        if (!IsIdentifierStart(normalized[0]) ||
            normalized.Any(character => !IsIdentifierPart(character)))
        {
            throw new ArgumentException(
                "Table names must use letters, digits, underscores, periods or backslashes and cannot start with a digit.",
                nameof(name));
        }
        if (CellAddress.TryParseA1(normalized, out _))
        {
            throw new ArgumentException(
                "A table name cannot be a valid A1 cell address.",
                nameof(name));
        }

        return normalized;
    }

    public static string ValidateColumnName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > SpreadsheetTable.MaxColumnNameLength)
        {
            throw new ArgumentException(
                $"Table-column names cannot exceed {SpreadsheetTable.MaxColumnNameLength} characters.",
                nameof(name));
        }
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Table-column names cannot contain control characters.",
                nameof(name));
        }

        return normalized;
    }

    public static string CreateUniqueColumnName(
        IReadOnlySet<string> existingNames,
        string prefix)
    {
        for (var index = 1; ; index++)
        {
            var candidate = string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix}{index}");
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool IsIdentifierStart(char character) =>
        char.IsLetter(character) || character is '_' or '\\';

    private static bool IsIdentifierPart(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '.' or '\\';
}

internal static class TableValueComparer
{
    public static int Compare(CellValue left, CellValue right)
    {
        if (TryNumber(left, out var leftNumber) &&
            TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(
            left.ToString(),
            right.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNumber(CellValue value, out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.DateTime:
                number = ((DateTime)value.RawValue!).ToOADate();
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }
}
