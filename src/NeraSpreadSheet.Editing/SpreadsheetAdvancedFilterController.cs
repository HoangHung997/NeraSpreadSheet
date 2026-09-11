using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetAdvancedFilterAction
{
    FilterInPlace,
    CopyToAnotherLocation,
}

public sealed record SpreadsheetAdvancedFilterOptions
{
    public required SpreadsheetAdvancedFilterAction Action { get; init; }
    public required CellRange ListRange { get; init; }
    public CellRange? CriteriaRange { get; init; }
    public CellAddress? CopyTo { get; init; }
    public bool UniqueRecordsOnly { get; init; }
}

/// <summary>Excel-style Advanced Filter over a bounded worksheet region. Criteria rows are OR clauses,
/// columns within a row are AND clauses, and common =, &lt;&gt;, &gt;, &gt;=, &lt;, &lt;= operators are supported.</summary>
public sealed class SpreadsheetAdvancedFilterController
{
    public const long MaximumMaterializedCells = 250_000;
    private readonly SpreadsheetSession _session;

    public SpreadsheetAdvancedFilterController(SpreadsheetSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public void Apply(SpreadsheetAdvancedFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sheet = _session.ActiveWorksheet;
        ValidateRange(options.ListRange, nameof(options.ListRange));
        if (options.ListRange.RowCount < 2) throw new ArgumentException("Advanced Filter requires a header row and at least one data row.", nameof(options));
        Bound(options.ListRange, "Advanced Filter list");
        if (options.CriteriaRange is { } criteria)
        {
            ValidateRange(criteria, nameof(options.CriteriaRange));
            Bound(criteria, "Advanced Filter criteria");
            if (criteria.RowCount < 2) throw new ArgumentException("Criteria range requires a header row and at least one criteria row.", nameof(options));
        }

        var matchedRows = GetMatchingRows(sheet, options.ListRange, options.CriteriaRange);
        if (options.UniqueRecordsOnly)
            matchedRows = KeepUniqueRows(sheet, options.ListRange, matchedRows);

        if (options.Action == SpreadsheetAdvancedFilterAction.FilterInPlace)
        {
            var protection = sheet.GetProtectionSettings();
            if (protection.Enabled && !protection.AllowAutoFilter)
                throw new InvalidOperationException("The protected worksheet does not allow filtering.");
            _session.Execute(new AdvancedFilterVisibilityOperation(sheet, options.ListRange, matchedRows));
            return;
        }

        if (options.CopyTo is not { } destination)
            throw new ArgumentException("Copy-to Advanced Filter requires a destination cell.", nameof(options));
        CopyRows(sheet, options.ListRange, matchedRows, destination);
    }

    private void CopyRows(Worksheet sheet, CellRange listRange, IReadOnlyList<int> rows, CellAddress destination)
    {
        var outputRows = checked(rows.Count + 1);
        if (destination.RowIndex > SpreadsheetLimits.MaxRows - outputRows ||
            destination.ColumnIndex > SpreadsheetLimits.MaxColumns - listRange.ColumnCount)
            throw new InvalidOperationException("Advanced Filter output would exceed worksheet limits.");
        var target = new CellRange(
            destination,
            new CellAddress(destination.RowIndex + outputRows - 1, destination.ColumnIndex + listRange.ColumnCount - 1));
        if (target.Intersects(listRange))
            throw new InvalidOperationException("Advanced Filter copy destination cannot overlap the source list.");
        Bound(target, "Advanced Filter output");

        var updates = new List<KeyValuePair<CellAddress, CellData>>(checked(outputRows * listRange.ColumnCount));
        CopyRow(listRange.Top, destination.RowIndex);
        for (var index = 0; index < rows.Count; index++) CopyRow(rows[index], destination.RowIndex + index + 1);
        _session.Execute(new SetCellsOperation(sheet, updates, "Advanced Filter copy"));

        void CopyRow(int sourceRow, int targetRow)
        {
            for (var offset = 0; offset < listRange.ColumnCount; offset++)
            {
                updates.Add(new KeyValuePair<CellAddress, CellData>(
                    new CellAddress(targetRow, destination.ColumnIndex + offset),
                    sheet.GetCell(new CellAddress(sourceRow, listRange.Left + offset))));
            }
        }
    }

    private static int[] GetMatchingRows(Worksheet sheet, CellRange list, CellRange? criteriaRange)
    {
        var criteriaRows = criteriaRange is { } criteria ? BuildCriteria(sheet, list, criteria) : [];
        var result = new List<int>(list.RowCount - 1);
        for (var row = list.Top + 1; row <= list.Bottom; row++)
        {
            if (criteriaRows.Count == 0 || criteriaRows.Any(clause => clause.All(condition => condition.Matches(sheet.GetCell(new CellAddress(row, list.Left + condition.ColumnOffset)).Value))))
                result.Add(row);
        }
        return [.. result];
    }

    private static List<List<Criterion>> BuildCriteria(Worksheet sheet, CellRange list, CellRange criteria)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = list.Left; column <= list.Right; column++)
        {
            var header = sheet.GetCell(new CellAddress(list.Top, column)).Value.ToString().Trim();
            if (!string.IsNullOrEmpty(header) && !headers.ContainsKey(header)) headers.Add(header, column - list.Left);
        }
        var criteriaHeaders = new (int CriteriaColumn, int ListOffset)[criteria.ColumnCount];
        for (var offset = 0; offset < criteria.ColumnCount; offset++)
        {
            var header = sheet.GetCell(new CellAddress(criteria.Top, criteria.Left + offset)).Value.ToString().Trim();
            if (string.IsNullOrEmpty(header) || !headers.TryGetValue(header, out var listOffset))
                throw new InvalidOperationException($"Advanced Filter criteria header '{header}' was not found in the list header row.");
            criteriaHeaders[offset] = (criteria.Left + offset, listOffset);
        }

        var rows = new List<List<Criterion>>();
        for (var row = criteria.Top + 1; row <= criteria.Bottom; row++)
        {
            var clause = new List<Criterion>();
            foreach (var mapping in criteriaHeaders)
            {
                var value = sheet.GetCell(new CellAddress(row, mapping.CriteriaColumn)).Value;
                if (!value.IsBlank) clause.Add(new Criterion(mapping.ListOffset, value));
            }
            if (clause.Count != 0) rows.Add(clause);
        }
        return rows;
    }

    private static int[] KeepUniqueRows(Worksheet sheet, CellRange list, IReadOnlyList<int> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<int>(rows.Count);
        foreach (var row in rows)
        {
            var parts = new string[list.ColumnCount];
            for (var offset = 0; offset < list.ColumnCount; offset++)
            {
                var value = sheet.GetCell(new CellAddress(row, list.Left + offset)).Value;
                parts[offset] = $"{(int)value.Kind}:{value}";
            }
            if (seen.Add(string.Join('\u001F', parts))) unique.Add(row);
        }
        return [.. unique];
    }

    private static void ValidateRange(CellRange range, string parameterName)
    {
        if (range.Top < 0 || range.Left < 0 || range.Bottom >= SpreadsheetLimits.MaxRows || range.Right >= SpreadsheetLimits.MaxColumns)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void Bound(CellRange range, string label)
    {
        var cells = checked((long)range.RowCount * range.ColumnCount);
        if (cells > MaximumMaterializedCells)
            throw new InvalidOperationException($"{label} is bounded to {MaximumMaterializedCells:N0} cells.");
    }

    private readonly record struct Criterion(int ColumnOffset, CellValue Expected)
    {
        public bool Matches(CellValue candidate)
        {
            if (Expected.Kind != CellValueKind.Text) return candidate == Expected;
            var text = Expected.ToString();
            var op = "=";
            var operand = text;
            foreach (var token in new[] { ">=", "<=", "<>", ">", "<", "=" })
            {
                if (!text.StartsWith(token, StringComparison.Ordinal)) continue;
                op = token; operand = text[token.Length..]; break;
            }
            var comparison = Compare(candidate, operand);
            return op switch
            {
                "=" => comparison == 0,
                "<>" => comparison != 0,
                ">" => comparison > 0,
                ">=" => comparison >= 0,
                "<" => comparison < 0,
                "<=" => comparison <= 0,
                _ => false,
            };
        }

        private static int Compare(CellValue candidate, string operand)
        {
            if (candidate.Kind == CellValueKind.Number && double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return ((double)candidate.RawValue!).CompareTo(number);
            if (candidate.Kind == CellValueKind.DateTime && DateTime.TryParse(operand, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
                return ((DateTime)candidate.RawValue!).CompareTo(date);
            if (candidate.Kind == CellValueKind.Boolean && bool.TryParse(operand, out var boolean))
                return ((bool)candidate.RawValue!).CompareTo(boolean);
            return string.Compare(candidate.ToString(), operand, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class AdvancedFilterVisibilityOperation : ISpreadsheetEditOperation
    {
        private readonly CellRange _list;
        private readonly HashSet<int> _matched;
        private WorksheetAxisInterval[]? _before;

        public AdvancedFilterVisibilityOperation(Worksheet worksheet, CellRange list, IEnumerable<int> matched)
        {
            Worksheet = worksheet;
            _list = list;
            _matched = [.. matched];
            AffectedRange = list;
        }

        public string Description => "Advanced Filter";
        public Worksheet Worksheet { get; }
        public CellRange AffectedRange { get; }
        public bool AffectsCalculation => false;

        public void Execute()
        {
            _before ??= [.. Worksheet.Dimensions.GetHiddenRowRanges()];
            for (var row = _list.Top + 1; row <= _list.Bottom; row++)
            {
                if (_matched.Contains(row)) Worksheet.Dimensions.UnhideRows(row, 1);
                else Worksheet.Dimensions.HideRows(row, 1);
            }
        }

        public void Undo()
        {
            if (_before is null) throw new InvalidOperationException("Advanced Filter has not been executed.");
            Worksheet.Dimensions.RestoreHiddenRanges(WorksheetAxis.Row, _before, _list.Top + 1, _list.RowCount - 1);
        }
    }
}
