using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetWorksheetAutoFilterController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetWorksheetAutoFilterController(
        SpreadsheetSession session)
    {
        _session = session ??
            throw new ArgumentNullException(nameof(session));
    }

    public WorksheetAutoFilter? Current =>
        _session.ActiveWorksheet.AutoFilter;

    public void SetRange(
        CellRange range,
        bool hasHeaderRow = true) =>
        SetAutoFilter(new WorksheetAutoFilter(
            range,
            columns: null,
            hasHeaderRow));

    public void SetAutoFilter(
        WorksheetAutoFilter? autoFilter,
        string description = "Set worksheet AutoFilter")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var before = _session.ActiveWorksheet.AutoFilter;
        var after = autoFilter?.Copy();
        if (Equals(before, after))
        {
            return;
        }

        _session.Execute(new SetWorksheetAutoFilterOperation(
            _session.ActiveWorksheet,
            before,
            after,
            description.Trim()));
    }

    public void ApplyValueFilter(
        int worksheetColumnIndex,
        IEnumerable<CellValue> values,
        bool includeBlank = false)
    {
        ArgumentNullException.ThrowIfNull(values);
        var current = RequireCurrent();
        var offset = GetColumnOffset(
            current,
            worksheetColumnIndex);
        var replacement = new WorksheetAutoFilterColumn(
            offset,
            values,
            includeBlank);
        ReplaceColumn(
            current,
            replacement,
            "Apply worksheet value filter");
    }

    public void ApplyCustomFilter(
        int worksheetColumnIndex,
        TableFilterCondition firstCondition,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true)
    {
        ArgumentNullException.ThrowIfNull(firstCondition);
        var current = RequireCurrent();
        var offset = GetColumnOffset(
            current,
            worksheetColumnIndex);
        var replacement = new WorksheetAutoFilterColumn(
            offset,
            firstCondition: firstCondition,
            secondCondition: secondCondition,
            combineWithAnd: combineWithAnd);
        ReplaceColumn(
            current,
            replacement,
            "Apply worksheet custom filter");
    }

    public void ClearColumnFilter(int worksheetColumnIndex)
    {
        var current = RequireCurrent();
        var offset = GetColumnOffset(
            current,
            worksheetColumnIndex);
        if (!current.Columns.Any(column =>
                column.ColumnOffset == offset))
        {
            return;
        }

        SetAutoFilter(
            current.WithColumns(current.Columns.Where(column =>
                column.ColumnOffset != offset)),
            "Clear worksheet column filter");
    }

    public void ClearCriteria()
    {
        var current = RequireCurrent();
        if (current.Columns.Count == 0)
        {
            return;
        }

        SetAutoFilter(
            current.WithColumns([]),
            "Clear worksheet filter criteria");
    }

    public void Clear() =>
        SetAutoFilter(
            null,
            "Remove worksheet AutoFilter");

    private void ReplaceColumn(
        WorksheetAutoFilter current,
        WorksheetAutoFilterColumn replacement,
        string description)
    {
        var columns = current.Columns
            .Where(column =>
                column.ColumnOffset != replacement.ColumnOffset)
            .Select(static column => column.Copy())
            .Append(replacement)
            .OrderBy(static column => column.ColumnOffset)
            .ToArray();
        SetAutoFilter(
            current.WithColumns(columns),
            description);
    }

    private WorksheetAutoFilter RequireCurrent() =>
        _session.ActiveWorksheet.AutoFilter ??
        throw new InvalidOperationException(
            "The active worksheet does not have a direct AutoFilter range.");

    private static int GetColumnOffset(
        WorksheetAutoFilter filter,
        int worksheetColumnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            worksheetColumnIndex);
        var offset = worksheetColumnIndex - filter.Range.Left;
        if (offset < 0 || offset >= filter.Range.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worksheetColumnIndex),
                worksheetColumnIndex,
                "The column must belong to the worksheet AutoFilter range.");
        }
        return offset;
    }
}

internal sealed class SetWorksheetAutoFilterOperation :
    ISpreadsheetEditOperation
{
    private readonly WorksheetAutoFilter? _before;
    private readonly WorksheetAutoFilter? _after;

    public SetWorksheetAutoFilterOperation(
        Worksheet worksheet,
        WorksheetAutoFilter? before,
        WorksheetAutoFilter? after,
        string description)
    {
        Worksheet = worksheet ??
            throw new ArgumentNullException(nameof(worksheet));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _before = before?.Copy();
        _after = after?.Copy();
        Description = description.Trim();
        AffectedRange = CalculateAffectedRange(
            _before,
            _after);
    }

    public string Description { get; }

    public Worksheet Worksheet { get; }

    public CellRange AffectedRange { get; }

    public bool AffectsCalculation => false;

    public void Execute() =>
        Worksheet.SetAutoFilter(_after);

    public void Undo() =>
        Worksheet.SetAutoFilter(_before);

    private static CellRange CalculateAffectedRange(
        WorksheetAutoFilter? before,
        WorksheetAutoFilter? after)
    {
        if (before is null && after is null)
        {
            throw new ArgumentException(
                "A worksheet AutoFilter operation requires a change.");
        }
        if (before is null)
        {
            return after!.Range;
        }
        if (after is null)
        {
            return before.Range;
        }

        return new CellRange(
            new CellAddress(
                Math.Min(before.Range.Top, after.Range.Top),
                Math.Min(before.Range.Left, after.Range.Left)),
            new CellAddress(
                Math.Max(before.Range.Bottom, after.Range.Bottom),
                Math.Max(before.Range.Right, after.Range.Right)));
    }
}
