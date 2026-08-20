using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace NeraSpreadSheet.Core;

public enum SpreadsheetTableTotalsFunction
{
    None = 0,
    Sum,
    Average,
    Count,
    CountNumbers,
    Minimum,
    Maximum,
    Custom,
}

public enum SpreadsheetFilterOperator
{
    Equal = 0,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    NotBetween,
    BeginsWith,
    EndsWith,
    Contains,
    DoesNotContain,
    IsBlank,
    IsNotBlank,
}

public enum SpreadsheetStructuredReferenceItem
{
    Data = 0,
    All,
    Headers,
    Totals,
    ThisRow,
}

public sealed class SpreadsheetTableColumn
{
    public SpreadsheetTableColumn(
        Guid id,
        string name,
        SpreadsheetTableTotalsFunction totalsFunction =
            SpreadsheetTableTotalsFunction.None,
        string? totalsFormula = null,
        string? totalsLabel = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Table-column identifiers cannot be empty.",
                nameof(id));
        }

        Id = id;
        Name = SpreadsheetTableNames.ValidateColumnName(name);
        TotalsFunction = totalsFunction;
        TotalsFormula = NormalizeOptionalFormula(totalsFormula);
        TotalsLabel = NormalizeOptionalText(
            totalsLabel,
            nameof(totalsLabel));
        if (TotalsFunction == SpreadsheetTableTotalsFunction.Custom &&
            TotalsFormula is null)
        {
            throw new ArgumentException(
                "Custom total columns require a totals formula.",
                nameof(totalsFormula));
        }
        if (TotalsFunction != SpreadsheetTableTotalsFunction.Custom &&
            TotalsFormula is not null)
        {
            throw new ArgumentException(
                "A totals formula is valid only for a custom totals function.",
                nameof(totalsFormula));
        }
    }

    public Guid Id { get; }

    public string Name { get; }

    public SpreadsheetTableTotalsFunction TotalsFunction { get; }

    public string? TotalsFormula { get; }

    public string? TotalsLabel { get; }

    public SpreadsheetTableColumn Rename(string name) =>
        new(
            Id,
            name,
            TotalsFunction,
            TotalsFormula,
            TotalsLabel);

    public SpreadsheetTableColumn Copy() =>
        new(
            Id,
            Name,
            TotalsFunction,
            TotalsFormula,
            TotalsLabel);

    private static string? NormalizeOptionalFormula(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var normalized = formula.Trim();
        if (normalized.Length > 8_192)
        {
            throw new ArgumentException(
                "Totals formulas cannot exceed 8192 characters.",
                nameof(formula));
        }

        return normalized.StartsWith('=')
            ? normalized
            : $"={normalized}";
    }

    private static string? NormalizeOptionalText(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 255)
        {
            throw new ArgumentException(
                "Table labels cannot exceed 255 characters.",
                parameterName);
        }

        return normalized;
    }
}

public sealed class SpreadsheetFilterCriterion
{
    public SpreadsheetFilterCriterion(
        SpreadsheetFilterOperator @operator,
        CellValue? first = null,
        CellValue? second = null,
        bool caseSensitive = false)
    {
        Operator = @operator;
        First = first;
        Second = second;
        CaseSensitive = caseSensitive;
        ValidateOperands();
    }

    public SpreadsheetFilterOperator Operator { get; }

    public CellValue? First { get; }

    public CellValue? Second { get; }

    public bool CaseSensitive { get; }

    public bool Matches(CellValue candidate)
    {
        if (Operator == SpreadsheetFilterOperator.IsBlank)
        {
            return candidate.IsBlank;
        }
        if (Operator == SpreadsheetFilterOperator.IsNotBlank)
        {
            return !candidate.IsBlank;
        }

        var first = First ?? CellValue.Blank;
        if (Operator is SpreadsheetFilterOperator.BeginsWith or
            SpreadsheetFilterOperator.EndsWith or
            SpreadsheetFilterOperator.Contains or
            SpreadsheetFilterOperator.DoesNotContain)
        {
            var comparison = CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            var candidateText = candidate.ToString();
            var operandText = first.ToString();
            return Operator switch
            {
                SpreadsheetFilterOperator.BeginsWith =>
                    candidateText.StartsWith(operandText, comparison),
                SpreadsheetFilterOperator.EndsWith =>
                    candidateText.EndsWith(operandText, comparison),
                SpreadsheetFilterOperator.Contains =>
                    candidateText.Contains(operandText, comparison),
                SpreadsheetFilterOperator.DoesNotContain =>
                    !candidateText.Contains(operandText, comparison),
                _ => false,
            };
        }

        var firstComparison = Compare(candidate, first, CaseSensitive);
        if (Operator is SpreadsheetFilterOperator.Between or
            SpreadsheetFilterOperator.NotBetween)
        {
            var second = Second ?? CellValue.Blank;
            var lower = Compare(first, second, CaseSensitive) <= 0
                ? first
                : second;
            var upper = ReferenceEquals(lower.RawValue, first.RawValue) &&
                        lower.Kind == first.Kind
                ? second
                : first;
            var isBetween =
                Compare(candidate, lower, CaseSensitive) >= 0 &&
                Compare(candidate, upper, CaseSensitive) <= 0;
            return Operator == SpreadsheetFilterOperator.Between
                ? isBetween
                : !isBetween;
        }

        return Operator switch
        {
            SpreadsheetFilterOperator.Equal => firstComparison == 0,
            SpreadsheetFilterOperator.NotEqual => firstComparison != 0,
            SpreadsheetFilterOperator.GreaterThan => firstComparison > 0,
            SpreadsheetFilterOperator.GreaterThanOrEqual =>
                firstComparison >= 0,
            SpreadsheetFilterOperator.LessThan => firstComparison < 0,
            SpreadsheetFilterOperator.LessThanOrEqual =>
                firstComparison <= 0,
            _ => false,
        };
    }

    public SpreadsheetFilterCriterion Copy() =>
        new(Operator, First, Second, CaseSensitive);

    private void ValidateOperands()
    {
        if (Operator is SpreadsheetFilterOperator.IsBlank or
            SpreadsheetFilterOperator.IsNotBlank)
        {
            if (First is not null || Second is not null)
            {
                throw new ArgumentException(
                    "Blank filter operators do not accept operands.");
            }
            return;
        }

        if (First is null)
        {
            throw new ArgumentException(
                "The selected filter operator requires a first operand.",
                nameof(First));
        }

        var requiresSecond = Operator is
            SpreadsheetFilterOperator.Between or
            SpreadsheetFilterOperator.NotBetween;
        if (requiresSecond != (Second is not null))
        {
            throw new ArgumentException(
                requiresSecond
                    ? "The selected filter operator requires two operands."
                    : "The selected filter operator accepts only one operand.",
                nameof(Second));
        }
    }

    private static int Compare(
        CellValue left,
        CellValue right,
        bool caseSensitive)
    {
        if (TryGetComparableNumber(left, out var leftNumber) &&
            TryGetComparableNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Compare(
            left.ToString(),
            right.ToString(),
            comparison);
    }

    private static bool TryGetComparableNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return double.IsFinite(number);
            case CellValueKind.DateTime:
                number = ((DateTime)value.RawValue!).ToOADate();
                return double.IsFinite(number);
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Text:
                return double.TryParse(
                    value.ToString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out number) &&
                    double.IsFinite(number);
            default:
                number = 0d;
                return false;
        }
    }
}

public sealed class SpreadsheetAutoFilter
{
    private readonly IReadOnlyDictionary<Guid, SpreadsheetFilterCriterion>
        _criteria;

    public SpreadsheetAutoFilter(
        IEnumerable<KeyValuePair<Guid, SpreadsheetFilterCriterion>>?
            criteria = null)
    {
        var materialized = criteria?.ToArray() ?? [];
        var dictionary = new Dictionary<Guid, SpreadsheetFilterCriterion>();
        foreach (var pair in materialized)
        {
            if (pair.Key == Guid.Empty)
            {
                throw new ArgumentException(
                    "Filter column identifiers cannot be empty.",
                    nameof(criteria));
            }
            ArgumentNullException.ThrowIfNull(pair.Value);
            if (!dictionary.TryAdd(pair.Key, pair.Value.Copy()))
            {
                throw new ArgumentException(
                    "A filter cannot contain duplicate column identifiers.",
                    nameof(criteria));
            }
        }

        _criteria = new ReadOnlyDictionary<Guid, SpreadsheetFilterCriterion>(
            dictionary);
    }

    public IReadOnlyDictionary<Guid, SpreadsheetFilterCriterion> Criteria =>
        _criteria;

    public bool IsActive => _criteria.Count > 0;

    public SpreadsheetAutoFilter Set(
        Guid columnId,
        SpreadsheetFilterCriterion criterion)
    {
        if (columnId == Guid.Empty)
        {
            throw new ArgumentException(
                "Filter column identifiers cannot be empty.",
                nameof(columnId));
        }
        ArgumentNullException.ThrowIfNull(criterion);
        var updated = _criteria.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Copy());
        updated[columnId] = criterion.Copy();
        return new SpreadsheetAutoFilter(updated);
    }

    public SpreadsheetAutoFilter Remove(Guid columnId) =>
        new(_criteria.Where(pair => pair.Key != columnId));

    public SpreadsheetAutoFilter Copy() => new(_criteria);
}

public sealed class SpreadsheetTable
{
    private readonly SpreadsheetTableColumn[] _columns;
    private readonly IReadOnlyList<SpreadsheetTableColumn> _readOnlyColumns;

    public SpreadsheetTable(
        Guid id,
        string name,
        CellRange range,
        IEnumerable<SpreadsheetTableColumn> columns,
        bool hasHeaderRow = true,
        bool hasTotalsRow = false,
        string? styleName = null,
        SpreadsheetAutoFilter? autoFilter = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Table identifiers cannot be empty.",
                nameof(id));
        }
        ArgumentNullException.ThrowIfNull(columns);

        Id = id;
        Name = SpreadsheetTableNames.ValidateTableName(name);
        Range = range;
        HasHeaderRow = hasHeaderRow;
        HasTotalsRow = hasTotalsRow;
        StyleName = NormalizeStyleName(styleName);
        _columns = columns.Select(static column => column.Copy()).ToArray();
        ValidateColumns(_columns, range.ColumnCount);
        ValidateRows(range, hasHeaderRow, hasTotalsRow);
        AutoFilter = ValidateFilter(
            autoFilter?.Copy() ?? new SpreadsheetAutoFilter(),
            _columns);
        _readOnlyColumns = Array.AsReadOnly(_columns);
    }

    public Guid Id { get; }

    public string Name { get; }

    public CellRange Range { get; }

    public bool HasHeaderRow { get; }

    public bool HasTotalsRow { get; }

    public string? StyleName { get; }

    public SpreadsheetAutoFilter AutoFilter { get; }

    public IReadOnlyList<SpreadsheetTableColumn> Columns =>
        _readOnlyColumns;

    public CellRange? HeaderRange => HasHeaderRow
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
            var top = Range.Top + (HasHeaderRow ? 1 : 0);
            var bottom = Range.Bottom - (HasTotalsRow ? 1 : 0);
            return top <= bottom
                ? new CellRange(
                    new CellAddress(top, Range.Left),
                    new CellAddress(bottom, Range.Right))
                : null;
        }
    }

    public SpreadsheetTable Rename(string name) =>
        Recreate(name: SpreadsheetTableNames.ValidateTableName(name));

    public SpreadsheetTable RenameColumn(Guid columnId, string name)
    {
        var index = GetColumnIndex(columnId);
        var updated = _columns.Select(static column => column.Copy()).ToArray();
        updated[index] = updated[index].Rename(name);
        return Recreate(columns: updated);
    }

    public SpreadsheetTable WithStyle(string? styleName) =>
        Recreate(styleName: NormalizeStyleName(styleName));

    public SpreadsheetTable WithAutoFilter(SpreadsheetAutoFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Recreate(autoFilter: filter.Copy());
    }

    public SpreadsheetTable Resize(CellRange range)
    {
        if (range.ColumnCount != _columns.Length)
        {
            throw new InvalidOperationException(
                "Resizing a table cannot implicitly add or remove table columns.");
        }

        return Recreate(range: range);
    }

    public SpreadsheetTable InsertColumns(
        int worksheetColumnBoundary,
        IEnumerable<SpreadsheetTableColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var inserted = columns.Select(static column => column.Copy()).ToArray();
        if (inserted.Length == 0)
        {
            throw new ArgumentException(
                "At least one table column is required.",
                nameof(columns));
        }
        if (worksheetColumnBoundary < Range.Left ||
            worksheetColumnBoundary > Range.Right + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worksheetColumnBoundary));
        }

        var offset = worksheetColumnBoundary - Range.Left;
        var updated = new SpreadsheetTableColumn[
            checked(_columns.Length + inserted.Length)];
        Array.Copy(_columns, 0, updated, 0, offset);
        Array.Copy(inserted, 0, updated, offset, inserted.Length);
        Array.Copy(
            _columns,
            offset,
            updated,
            offset + inserted.Length,
            _columns.Length - offset);
        var range = new CellRange(
            Range.TopLeft,
            new CellAddress(
                Range.Bottom,
                checked(Range.Right + inserted.Length)));
        return Recreate(range: range, columns: updated);
    }

    public SpreadsheetTable DeleteColumns(
        int worksheetColumnIndex,
        int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var deleteEnd = checked(worksheetColumnIndex + count - 1);
        if (worksheetColumnIndex < Range.Left || deleteEnd > Range.Right)
        {
            throw new ArgumentOutOfRangeException(nameof(worksheetColumnIndex));
        }
        if (count >= _columns.Length)
        {
            throw new InvalidOperationException(
                "A table must retain at least one column.");
        }

        var offset = worksheetColumnIndex - Range.Left;
        var updated = _columns
            .Where((_, index) => index < offset || index >= offset + count)
            .Select(static column => column.Copy())
            .ToArray();
        var range = new CellRange(
            Range.TopLeft,
            new CellAddress(Range.Bottom, Range.Right - count));
        return Recreate(range: range, columns: updated);
    }

    public SpreadsheetTable MapInsert(
        WorksheetAxis axis,
        int index,
        int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (axis == WorksheetAxis.Row)
        {
            if (index <= Range.Top)
            {
                return Recreate(range: ShiftRange(axis, count));
            }
            if (index <= Range.Bottom)
            {
                return Recreate(range: new CellRange(
                    Range.TopLeft,
                    new CellAddress(
                        checked(Range.Bottom + count),
                        Range.Right)));
            }
            return Copy();
        }

        if (index <= Range.Left)
        {
            return Recreate(range: ShiftRange(axis, count));
        }
        if (index <= Range.Right)
        {
            throw new InvalidOperationException(
                "Columns inserted inside a table require explicit table-column metadata.");
        }
        return Copy();
    }

    public SpreadsheetTable MapDelete(
        WorksheetAxis axis,
        int index,
        int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var end = checked(index + count - 1);
        if (axis == WorksheetAxis.Column)
        {
            if (end < Range.Left)
            {
                return Recreate(range: ShiftRange(axis, -count));
            }
            if (index > Range.Right)
            {
                return Copy();
            }

            var overlapStart = Math.Max(index, Range.Left);
            var overlapEnd = Math.Min(end, Range.Right);
            var overlapCount = checked(overlapEnd - overlapStart + 1);
            if (overlapCount >= _columns.Length)
            {
                throw new InvalidOperationException(
                    "Deleting all columns of a table is not supported by an in-place transform.");
            }

            return DeleteColumns(overlapStart, overlapCount)
                .Recreate(range: ShiftDeletedRange(
                    axis,
                    index,
                    count,
                    Range,
                    overlapCount));
        }

        if (end < Range.Top)
        {
            return Recreate(range: ShiftRange(axis, -count));
        }
        if (index > Range.Bottom)
        {
            return Copy();
        }

        if (HasHeaderRow && index <= Range.Top && end >= Range.Top)
        {
            throw new InvalidOperationException(
                "Deleting a table header row requires removing or recreating the table.");
        }
        if (HasTotalsRow && index <= Range.Bottom && end >= Range.Bottom)
        {
            throw new InvalidOperationException(
                "Deleting a table totals row requires disabling totals first.");
        }

        var overlapStartRow = Math.Max(index, Range.Top);
        var overlapEndRow = Math.Min(end, Range.Bottom);
        var overlapRows = checked(overlapEndRow - overlapStartRow + 1);
        return Recreate(range: ShiftDeletedRange(
            axis,
            index,
            count,
            Range,
            overlapRows));
    }

    public SpreadsheetTable MapMove(WorksheetAxisMove move)
    {
        if (!move.TryMapUniformRange(Range, out var mapped))
        {
            throw new InvalidOperationException(
                "A table can be reordered only when its complete range moves by one uniform translation.");
        }

        return Recreate(range: mapped);
    }

    public bool TryGetColumn(string name, out SpreadsheetTableColumn? column)
    {
        column = _columns.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            name,
            StringComparison.OrdinalIgnoreCase));
        return column is not null;
    }

    public int GetColumnIndex(Guid columnId)
    {
        for (var index = 0; index < _columns.Length; index++)
        {
            if (_columns[index].Id == columnId)
            {
                return index;
            }
        }

        throw new KeyNotFoundException(
            $"Table column '{columnId}' was not found.");
    }

    public int GetColumnIndex(string name)
    {
        for (var index = 0; index < _columns.Length; index++)
        {
            if (string.Equals(
                    _columns[index].Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new KeyNotFoundException(
            $"Table column '{name}' was not found.");
    }

    public SpreadsheetTable Copy() => Recreate();

    private SpreadsheetTable Recreate(
        string? name = null,
        CellRange? range = null,
        IEnumerable<SpreadsheetTableColumn>? columns = null,
        string? styleName = null,
        SpreadsheetAutoFilter? autoFilter = null) =>
        new(
            Id,
            name ?? Name,
            range ?? Range,
            columns ?? _columns,
            HasHeaderRow,
            HasTotalsRow,
            styleName ?? StyleName,
            autoFilter ?? AutoFilter);

    private CellRange ShiftRange(WorksheetAxis axis, int delta) =>
        axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(
                    checked(Range.Top + delta),
                    Range.Left),
                new CellAddress(
                    checked(Range.Bottom + delta),
                    Range.Right))
            : new CellRange(
                new CellAddress(
                    Range.Top,
                    checked(Range.Left + delta)),
                new CellAddress(
                    Range.Bottom,
                    checked(Range.Right + delta)));

    private static CellRange ShiftDeletedRange(
        WorksheetAxis axis,
        int deleteIndex,
        int deleteCount,
        CellRange source,
        int overlapCount)
    {
        if (axis == WorksheetAxis.Row)
        {
            var top = deleteIndex < source.Top
                ? source.Top - Math.Min(deleteCount, source.Top - deleteIndex)
                : source.Top;
            var bottom = checked(source.Bottom - overlapCount -
                (deleteIndex < source.Top
                    ? Math.Min(deleteCount, source.Top - deleteIndex)
                    : 0));
            return new CellRange(
                new CellAddress(top, source.Left),
                new CellAddress(bottom, source.Right));
        }

        var left = deleteIndex < source.Left
            ? source.Left - Math.Min(deleteCount, source.Left - deleteIndex)
            : source.Left;
        var right = checked(source.Right - overlapCount -
            (deleteIndex < source.Left
                ? Math.Min(deleteCount, source.Left - deleteIndex)
                : 0));
        return new CellRange(
            new CellAddress(source.Top, left),
            new CellAddress(source.Bottom, right));
    }

    private static SpreadsheetAutoFilter ValidateFilter(
        SpreadsheetAutoFilter filter,
        IReadOnlyCollection<SpreadsheetTableColumn> columns)
    {
        var columnIds = columns.Select(static column => column.Id).ToHashSet();
        if (filter.Criteria.Keys.Any(columnId => !columnIds.Contains(columnId)))
        {
            throw new ArgumentException(
                "An AutoFilter criterion references a column outside the table.",
                nameof(filter));
        }

        return filter;
    }

    private static void ValidateColumns(
        IReadOnlyCollection<SpreadsheetTableColumn> columns,
        int expectedCount)
    {
        if (columns.Count != expectedCount)
        {
            throw new ArgumentException(
                "The number of table columns must match the table range width.",
                nameof(columns));
        }
        if (columns.Count == 0)
        {
            throw new ArgumentException(
                "A table must contain at least one column.",
                nameof(columns));
        }
        if (columns.Select(static column => column.Id).Distinct().Count() !=
            columns.Count)
        {
            throw new ArgumentException(
                "Table-column identifiers must be unique.",
                nameof(columns));
        }
        if (columns.Select(static column => column.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != columns.Count)
        {
            throw new ArgumentException(
                "Table-column names must be unique.",
                nameof(columns));
        }
    }

    private static void ValidateRows(
        CellRange range,
        bool hasHeaderRow,
        bool hasTotalsRow)
    {
        var requiredRows = (hasHeaderRow ? 1 : 0) +
                           (hasTotalsRow ? 1 : 0);
        if (range.RowCount < Math.Max(1, requiredRows))
        {
            throw new ArgumentException(
                "The table range is too small for its header/totals configuration.",
                nameof(range));
        }
    }

    private static string? NormalizeStyleName(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        var normalized = styleName.Trim();
        if (normalized.Length > 255)
        {
            throw new ArgumentException(
                "Table style names cannot exceed 255 characters.",
                nameof(styleName));
        }

        return normalized;
    }
}

public sealed record SpreadsheetTableBinding(
    Worksheet Worksheet,
    SpreadsheetTable Table);

public sealed class WorkbookTableCatalog
{
    public const int MaxTablesPerWorkbook = 65_536;

    private readonly Workbook _workbook;
    private readonly List<SpreadsheetTableBinding> _bindings = [];

    public WorkbookTableCatalog(Workbook workbook)
    {
        _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
    }

    public Workbook Workbook => _workbook;

    public int Count => _bindings.Count;

    public IReadOnlyList<SpreadsheetTableBinding> Bindings =>
        _bindings.Select(static binding => new SpreadsheetTableBinding(
            binding.Worksheet,
            binding.Table.Copy())).ToArray();

    public void Add(Worksheet worksheet, SpreadsheetTable table)
    {
        EnsureWorksheet(worksheet);
        ArgumentNullException.ThrowIfNull(table);
        if (_bindings.Count >= MaxTablesPerWorkbook)
        {
            throw new InvalidOperationException(
                $"A workbook cannot contain more than {MaxTablesPerWorkbook} tables.");
        }
        if (_bindings.Any(binding => binding.Table.Id == table.Id))
        {
            throw new InvalidOperationException(
                $"A table with identifier '{table.Id}' already exists.");
        }
        if (_bindings.Any(binding => string.Equals(
                binding.Table.Name,
                table.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A table named '{table.Name}' already exists in the workbook.");
        }
        if (_bindings.Any(binding =>
                ReferenceEquals(binding.Worksheet, worksheet) &&
                binding.Table.Range.Intersects(table.Range)))
        {
            throw new InvalidOperationException(
                "Tables on the same worksheet cannot overlap.");
        }

        _bindings.Add(new SpreadsheetTableBinding(worksheet, table.Copy()));
    }

    public bool Remove(Guid tableId)
    {
        var index = _bindings.FindIndex(binding => binding.Table.Id == tableId);
        if (index < 0)
        {
            return false;
        }

        _bindings.RemoveAt(index);
        return true;
    }

    public void Replace(Guid tableId, SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var index = _bindings.FindIndex(binding => binding.Table.Id == tableId);
        if (index < 0)
        {
            throw new KeyNotFoundException(
                $"Table '{tableId}' was not found.");
        }
        if (table.Id != tableId)
        {
            throw new ArgumentException(
                "Replacing a table cannot change its stable identifier.",
                nameof(table));
        }

        var worksheet = _bindings[index].Worksheet;
        var before = _bindings[index];
        _bindings.RemoveAt(index);
        try
        {
            Add(worksheet, table);
            var appended = _bindings[^1];
            _bindings.RemoveAt(_bindings.Count - 1);
            _bindings.Insert(index, appended);
        }
        catch
        {
            _bindings.Insert(index, before);
            throw;
        }
    }

    public SpreadsheetTable Get(Guid tableId) =>
        _bindings.FirstOrDefault(binding => binding.Table.Id == tableId)?.Table
            .Copy()
        ?? throw new KeyNotFoundException(
            $"Table '{tableId}' was not found.");

    public SpreadsheetTable Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _bindings.FirstOrDefault(binding => string.Equals(
                binding.Table.Name,
                name.Trim(),
                StringComparison.OrdinalIgnoreCase))?.Table.Copy()
            ?? throw new KeyNotFoundException(
                $"Table '{name}' was not found.");
    }

    public bool TryGet(
        string name,
        out SpreadsheetTableBinding? binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var found = _bindings.FirstOrDefault(candidate => string.Equals(
            candidate.Table.Name,
            name.Trim(),
            StringComparison.OrdinalIgnoreCase));
        binding = found is null
            ? null
            : new SpreadsheetTableBinding(
                found.Worksheet,
                found.Table.Copy());
        return binding is not null;
    }

    public bool TryGetContaining(
        Worksheet worksheet,
        CellAddress address,
        out SpreadsheetTableBinding? binding)
    {
        EnsureWorksheet(worksheet);
        var found = _bindings.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Worksheet, worksheet) &&
            candidate.Table.Range.Contains(address));
        binding = found is null
            ? null
            : new SpreadsheetTableBinding(
                found.Worksheet,
                found.Table.Copy());
        return binding is not null;
    }

    public IReadOnlyList<SpreadsheetTable> GetTables(Worksheet worksheet)
    {
        EnsureWorksheet(worksheet);
        return _bindings
            .Where(binding => ReferenceEquals(binding.Worksheet, worksheet))
            .Select(static binding => binding.Table.Copy())
            .ToArray();
    }

    public IReadOnlyList<int> GetFilteredOutRows(
        WorksheetSnapshot worksheet,
        SpreadsheetTable table,
        int maximumRowsToScan = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRowsToScan);
        var dataRange = table.DataRange;
        if (dataRange is null || !table.AutoFilter.IsActive)
        {
            return [];
        }
        if (dataRange.Value.RowCount > maximumRowsToScan)
        {
            throw new InvalidOperationException(
                $"The table data range exceeds the filter scan limit of {maximumRowsToScan} rows.");
        }

        var hidden = new List<int>();
        for (var row = dataRange.Value.Top; row <= dataRange.Value.Bottom; row++)
        {
            if (!IsRowVisible(worksheet, table, row))
            {
                hidden.Add(row);
            }
        }

        return hidden;
    }

    public static bool IsRowVisible(
        WorksheetSnapshot worksheet,
        SpreadsheetTable table,
        int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(table);
        var dataRange = table.DataRange;
        if (dataRange is null ||
            rowIndex < dataRange.Value.Top ||
            rowIndex > dataRange.Value.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        foreach (var pair in table.AutoFilter.Criteria)
        {
            var columnIndex = table.GetColumnIndex(pair.Key);
            var address = new CellAddress(
                rowIndex,
                table.Range.Left + columnIndex);
            if (!pair.Value.Matches(worksheet.GetCell(address).Value))
            {
                return false;
            }
        }

        return true;
    }

    public WorkbookTableCatalog Copy()
    {
        var copy = new WorkbookTableCatalog(_workbook);
        foreach (var binding in _bindings)
        {
            copy._bindings.Add(new SpreadsheetTableBinding(
                binding.Worksheet,
                binding.Table.Copy()));
        }
        return copy;
    }

    private void EnsureWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!_workbook.Worksheets.Any(candidate =>
                ReferenceEquals(candidate, worksheet)))
        {
            throw new InvalidOperationException(
                "Worksheet does not belong to the table catalog workbook.");
        }
    }
}

public static class SpreadsheetStructuredReferenceResolver
{
    public static string ResolveFormula(
        string formula,
        WorkbookTableCatalog tables,
        Worksheet currentWorksheet,
        CellAddress formulaAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(currentWorksheet);

        var result = new StringBuilder(formula.Length + 32);
        var inString = false;
        for (var index = 0; index < formula.Length;)
        {
            var current = formula[index];
            if (current == '"')
            {
                result.Append(current);
                if (inString &&
                    index + 1 < formula.Length &&
                    formula[index + 1] == '"')
                {
                    result.Append('"');
                    index += 2;
                    continue;
                }

                inString = !inString;
                index++;
                continue;
            }

            if (inString)
            {
                result.Append(current);
                index++;
                continue;
            }

            if (IsIdentifierStart(current))
            {
                var identifierStart = index;
                index++;
                while (index < formula.Length &&
                       IsIdentifierPart(formula[index]))
                {
                    index++;
                }

                var identifier = formula[identifierStart..index];
                if (index < formula.Length && formula[index] == '[' &&
                    tables.TryGet(identifier, out var explicitBinding) &&
                    explicitBinding is not null)
                {
                    var referenceEnd = FindReferenceEnd(formula, index);
                    var reference = formula[index..referenceEnd];
                    result.Append(ResolveReference(
                        explicitBinding,
                        reference,
                        currentWorksheet,
                        formulaAddress));
                    index = referenceEnd;
                    continue;
                }

                result.Append(identifier);
                continue;
            }

            if (current == '[' &&
                tables.TryGetContaining(
                    currentWorksheet,
                    formulaAddress,
                    out var currentBinding) &&
                currentBinding is not null)
            {
                var referenceEnd = FindReferenceEnd(formula, index);
                var reference = formula[index..referenceEnd];
                result.Append(ResolveReference(
                    currentBinding,
                    reference,
                    currentWorksheet,
                    formulaAddress));
                index = referenceEnd;
                continue;
            }

            result.Append(current);
            index++;
        }

        if (inString)
        {
            throw new FormatException(
                "Formula contains an unterminated string literal.");
        }

        return result.ToString();
    }

    private static string ResolveReference(
        SpreadsheetTableBinding binding,
        string reference,
        Worksheet currentWorksheet,
        CellAddress formulaAddress)
    {
        var parsed = ParseReference(reference);
        var table = binding.Table;
        CellRange? target = parsed.Item switch
        {
            SpreadsheetStructuredReferenceItem.All => table.Range,
            SpreadsheetStructuredReferenceItem.Headers => table.HeaderRange,
            SpreadsheetStructuredReferenceItem.Totals => table.TotalsRange,
            SpreadsheetStructuredReferenceItem.ThisRow =>
                CreateThisRowRange(table, formulaAddress.RowIndex),
            _ => table.DataRange,
        };
        if (target is null)
        {
            throw new InvalidOperationException(
                $"Table '{table.Name}' does not expose the requested structured-reference item.");
        }

        var range = target.Value;
        if (parsed.ColumnName is not null)
        {
            var columnIndex = table.GetColumnIndex(parsed.ColumnName);
            var worksheetColumn = table.Range.Left + columnIndex;
            range = new CellRange(
                new CellAddress(range.Top, worksheetColumn),
                new CellAddress(range.Bottom, worksheetColumn));
        }

        var qualifier = ReferenceEquals(binding.Worksheet, currentWorksheet)
            ? string.Empty
            : $"'{binding.Worksheet.Name.Replace("'", "''", StringComparison.Ordinal)}'!";
        return qualifier + range;
    }

    private static CellRange CreateThisRowRange(
        SpreadsheetTable table,
        int rowIndex)
    {
        var dataRange = table.DataRange;
        if (dataRange is null ||
            rowIndex < dataRange.Value.Top ||
            rowIndex > dataRange.Value.Bottom)
        {
            throw new InvalidOperationException(
                "A #This Row structured reference must be evaluated from a table data row.");
        }

        return new CellRange(
            new CellAddress(rowIndex, table.Range.Left),
            new CellAddress(rowIndex, table.Range.Right));
    }

    private static ParsedStructuredReference ParseReference(string reference)
    {
        if (reference.Length < 2 ||
            reference[0] != '[' ||
            reference[^1] != ']')
        {
            throw new FormatException(
                "Structured reference is not bracket-balanced.");
        }

        var items = SplitReferenceItems(reference);
        var item = SpreadsheetStructuredReferenceItem.Data;
        string? columnName = null;
        foreach (var rawItem in items)
        {
            var value = rawItem.Trim();
            if (value.StartsWith('@'))
            {
                item = SpreadsheetStructuredReferenceItem.ThisRow;
                var inlineColumn = value[1..].Trim();
                if (inlineColumn.Length > 0)
                {
                    columnName = inlineColumn;
                }
                continue;
            }

            if (value.StartsWith('#'))
            {
                item = value.ToUpperInvariant() switch
                {
                    "#ALL" => SpreadsheetStructuredReferenceItem.All,
                    "#DATA" => SpreadsheetStructuredReferenceItem.Data,
                    "#HEADERS" =>
                        SpreadsheetStructuredReferenceItem.Headers,
                    "#TOTALS" =>
                        SpreadsheetStructuredReferenceItem.Totals,
                    "#THIS ROW" =>
                        SpreadsheetStructuredReferenceItem.ThisRow,
                    _ => throw new NotSupportedException(
                        $"Structured-reference item '{value}' is not supported."),
                };
                continue;
            }

            if (columnName is not null)
            {
                throw new NotSupportedException(
                    "Multi-column structured-reference spans are not supported yet.");
            }
            columnName = value;
        }

        return new ParsedStructuredReference(item, columnName);
    }

    private static string[] SplitReferenceItems(string reference)
    {
        var inner = reference[1..^1];
        if (inner.Length >= 2 && inner[0] == '[' && inner[^1] == ']')
        {
            inner = inner[1..^1];
        }

        var items = new List<string>();
        var builder = new StringBuilder();
        var depth = 0;
        for (var index = 0; index < inner.Length; index++)
        {
            var current = inner[index];
            if (current == '[')
            {
                depth++;
                if (depth > 1)
                {
                    builder.Append(current);
                }
                continue;
            }
            if (current == ']')
            {
                if (depth > 1)
                {
                    builder.Append(current);
                }
                depth--;
                if (depth < 0)
                {
                    throw new FormatException(
                        "Structured reference is not bracket-balanced.");
                }
                continue;
            }
            if (current == ',' && depth == 0)
            {
                AddReferenceItem(items, builder);
                continue;
            }

            builder.Append(current);
        }
        if (depth != 0)
        {
            throw new FormatException(
                "Structured reference is not bracket-balanced.");
        }
        AddReferenceItem(items, builder);
        if (items.Count == 0)
        {
            throw new FormatException(
                "Structured reference does not contain an item or column.");
        }

        return items.ToArray();
    }

    private static void AddReferenceItem(
        List<string> items,
        StringBuilder builder)
    {
        var item = builder.ToString().Trim();
        builder.Clear();
        if (item.Length > 0)
        {
            items.Add(item);
        }
    }

    private static int FindReferenceEnd(string formula, int start)
    {
        var depth = 0;
        for (var index = start; index < formula.Length; index++)
        {
            if (formula[index] == '[')
            {
                depth++;
            }
            else if (formula[index] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return index + 1;
                }
                if (depth < 0)
                {
                    break;
                }
            }
        }

        throw new FormatException(
            "Structured reference is not bracket-balanced.");
    }

    private static bool IsIdentifierStart(char value) =>
        value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value) =>
        value == '_' || char.IsLetterOrDigit(value);

    private readonly record struct ParsedStructuredReference(
        SpreadsheetStructuredReferenceItem Item,
        string? ColumnName);
}

internal static class SpreadsheetTableNames
{
    public static string ValidateTableName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 255)
        {
            throw new ArgumentException(
                "Table names cannot exceed 255 characters.",
                nameof(name));
        }
        if (!IsNameStart(normalized[0]) ||
            normalized.Skip(1).Any(character => !IsNamePart(character)))
        {
            throw new ArgumentException(
                "Table names must begin with a letter or underscore and contain only letters, digits or underscores.",
                nameof(name));
        }
        if (CellAddress.TryParseA1(normalized, out _))
        {
            throw new ArgumentException(
                "Table names cannot be valid A1 cell addresses.",
                nameof(name));
        }

        return normalized;
    }

    public static string ValidateColumnName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 255)
        {
            throw new ArgumentException(
                "Table-column names cannot exceed 255 characters.",
                nameof(name));
        }
        if (normalized.Contains('[', StringComparison.Ordinal) ||
            normalized.Contains(']', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Table-column names cannot contain square brackets.",
                nameof(name));
        }

        return normalized;
    }

    private static bool IsNameStart(char value) =>
        value == '_' || char.IsLetter(value);

    private static bool IsNamePart(char value) =>
        value == '_' || char.IsLetterOrDigit(value);
}
