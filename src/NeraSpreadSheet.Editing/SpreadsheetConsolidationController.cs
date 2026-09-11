using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetConsolidationFunction
{
    Sum,
    Average,
    Count,
    Maximum,
    Minimum,
}

public sealed record SpreadsheetConsolidationSource(Worksheet Worksheet, CellRange Range);

public sealed record SpreadsheetConsolidationOptions
{
    public required SpreadsheetConsolidationFunction Function { get; init; }
    public required IReadOnlyList<SpreadsheetConsolidationSource> Sources { get; init; }
    public required CellAddress Destination { get; init; }
    public bool UseTopRowLabels { get; init; }
    public bool UseLeftColumnLabels { get; init; }
    public bool CreateLinksToSource { get; init; }
}

/// <summary>Bounded multi-range consolidation with Excel-style top-row/left-column label matching.
/// Output is one undoable write and optional source links are ordinary workbook formulas.</summary>
public sealed class SpreadsheetConsolidationController
{
    public const long MaximumSourceCells = 500_000;
    private readonly SpreadsheetSession _session;

    public SpreadsheetConsolidationController(SpreadsheetSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public void Consolidate(SpreadsheetConsolidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Function)) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.Sources is null || options.Sources.Count == 0) throw new ArgumentException("Consolidate requires at least one source range.", nameof(options));
        long total = 0;
        foreach (var source in options.Sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!_session.Workbook.Worksheets.Contains(source.Worksheet)) throw new ArgumentException("Every consolidation source must belong to the session workbook.", nameof(options));
            ValidateRange(source.Range);
            if (options.UseTopRowLabels && source.Range.RowCount < 2) throw new ArgumentException("Top-row labels require at least two source rows.", nameof(options));
            if (options.UseLeftColumnLabels && source.Range.ColumnCount < 2) throw new ArgumentException("Left-column labels require at least two source columns.", nameof(options));
            total = checked(total + (long)source.Range.RowCount * source.Range.ColumnCount);
        }
        if (total > MaximumSourceCells) throw new InvalidOperationException($"Consolidation is bounded to {MaximumSourceCells:N0} source cells.");

        var rows = BuildAxis(options.Sources, rowAxis: true, options.UseLeftColumnLabels, options.UseTopRowLabels);
        var columns = BuildAxis(options.Sources, rowAxis: false, options.UseTopRowLabels, options.UseLeftColumnLabels);
        var rowOffset = options.UseTopRowLabels ? 1 : 0;
        var columnOffset = options.UseLeftColumnLabels ? 1 : 0;
        var height = checked(rows.Count + rowOffset);
        var width = checked(columns.Count + columnOffset);
        if (height <= 0 || width <= 0) throw new InvalidOperationException("Consolidation has no output cells.");
        if (options.Destination.RowIndex > SpreadsheetLimits.MaxRows - height || options.Destination.ColumnIndex > SpreadsheetLimits.MaxColumns - width)
            throw new InvalidOperationException("Consolidation output would exceed worksheet limits.");

        var destinationSheet = _session.ActiveWorksheet;
        var updates = new List<KeyValuePair<CellAddress, CellData>>(checked(height * width));
        if (options.UseTopRowLabels)
        {
            for (var column = 0; column < columns.Count; column++)
                AddValue(options.Destination.RowIndex, options.Destination.ColumnIndex + columnOffset + column, columns[column].Display);
        }
        if (options.UseLeftColumnLabels)
        {
            for (var row = 0; row < rows.Count; row++)
                AddValue(options.Destination.RowIndex + rowOffset + row, options.Destination.ColumnIndex, rows[row].Display);
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                var sourceCells = ResolveCells(options, rows[row], columns[column]);
                var target = new CellAddress(options.Destination.RowIndex + rowOffset + row, options.Destination.ColumnIndex + columnOffset + column);
                var current = destinationSheet.GetCell(target);
                CellData next;
                if (options.CreateLinksToSource && sourceCells.Count != 0)
                {
                    next = new CellData(current.Value, BuildFormula(options.Function, sourceCells), current.StyleId);
                }
                else
                {
                    next = new CellData(Aggregate(options.Function, sourceCells.Select(static cell => cell.Value)), styleId: current.StyleId);
                }
                updates.Add(new KeyValuePair<CellAddress, CellData>(target, next));
            }
        }
        if (updates.Count != 0) _session.Execute(new SetCellsOperation(destinationSheet, updates, "Consolidate data"));

        void AddValue(int row, int column, CellValue value)
        {
            var address = new CellAddress(row, column);
            var current = destinationSheet.GetCell(address);
            updates.Add(new KeyValuePair<CellAddress, CellData>(address, new CellData(value, styleId: current.StyleId)));
        }
    }

    private static List<AxisKey> BuildAxis(
        IReadOnlyList<SpreadsheetConsolidationSource> sources,
        bool rowAxis,
        bool useLabels,
        bool skipOtherLabelAxis)
    {
        if (!useLabels)
        {
            var maximum = sources.Max(source => rowAxis
                ? source.Range.RowCount - (skipOtherLabelAxis ? 1 : 0)
                : source.Range.ColumnCount - (skipOtherLabelAxis ? 1 : 0));
            return Enumerable.Range(0, maximum).Select(index => new AxisKey(index.ToString(System.Globalization.CultureInfo.InvariantCulture), CellValue.FromNumber(index), index)).ToList();
        }

        var result = new List<AxisKey>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var start = rowAxis ? source.Range.Top + (skipOtherLabelAxis ? 1 : 0) : source.Range.Left + (skipOtherLabelAxis ? 1 : 0);
            var end = rowAxis ? source.Range.Bottom : source.Range.Right;
            for (var index = start; index <= end; index++)
            {
                var address = rowAxis ? new CellAddress(index, source.Range.Left) : new CellAddress(source.Range.Top, index);
                var display = source.Worksheet.GetCell(address).Value;
                var key = display.ToString();
                if (string.IsNullOrEmpty(key)) continue;
                if (known.Add(key)) result.Add(new AxisKey(key, display, result.Count));
            }
        }
        return result;
    }

    private static List<SourceCell> ResolveCells(SpreadsheetConsolidationOptions options, AxisKey rowKey, AxisKey columnKey)
    {
        var result = new List<SourceCell>(options.Sources.Count);
        foreach (var source in options.Sources)
        {
            var row = FindAxisIndex(source, rowAxis: true, rowKey, options.UseLeftColumnLabels, options.UseTopRowLabels);
            var column = FindAxisIndex(source, rowAxis: false, columnKey, options.UseTopRowLabels, options.UseLeftColumnLabels);
            if (row < 0 || column < 0) continue;
            var address = new CellAddress(row, column);
            result.Add(new SourceCell(source.Worksheet, address, source.Worksheet.GetCell(address).Value));
        }
        return result;
    }

    private static int FindAxisIndex(SpreadsheetConsolidationSource source, bool rowAxis, AxisKey key, bool useLabels, bool skipOtherLabelAxis)
    {
        var start = rowAxis ? source.Range.Top + (skipOtherLabelAxis ? 1 : 0) : source.Range.Left + (skipOtherLabelAxis ? 1 : 0);
        var end = rowAxis ? source.Range.Bottom : source.Range.Right;
        if (!useLabels)
        {
            var target = start + key.Ordinal;
            return target <= end ? target : -1;
        }
        for (var index = start; index <= end; index++)
        {
            var label = rowAxis
                ? source.Worksheet.GetCell(new CellAddress(index, source.Range.Left)).Value.ToString()
                : source.Worksheet.GetCell(new CellAddress(source.Range.Top, index)).Value.ToString();
            if (string.Equals(label, key.Key, StringComparison.OrdinalIgnoreCase)) return index;
        }
        return -1;
    }

    private static CellValue Aggregate(SpreadsheetConsolidationFunction function, IEnumerable<CellValue> source)
    {
        var values = source.Where(static value => value.Kind == CellValueKind.Number)
            .Select(static value => (double)value.RawValue!).ToArray();
        return function switch
        {
            SpreadsheetConsolidationFunction.Count => CellValue.FromNumber(values.Length),
            SpreadsheetConsolidationFunction.Sum => CellValue.FromNumber(values.Sum()),
            SpreadsheetConsolidationFunction.Average when values.Length != 0 => CellValue.FromNumber(values.Average()),
            SpreadsheetConsolidationFunction.Maximum when values.Length != 0 => CellValue.FromNumber(values.Max()),
            SpreadsheetConsolidationFunction.Minimum when values.Length != 0 => CellValue.FromNumber(values.Min()),
            _ => CellValue.Blank,
        };
    }

    private static string BuildFormula(SpreadsheetConsolidationFunction function, IReadOnlyList<SourceCell> cells)
    {
        var name = function switch
        {
            SpreadsheetConsolidationFunction.Sum => "SUM",
            SpreadsheetConsolidationFunction.Average => "AVERAGE",
            SpreadsheetConsolidationFunction.Count => "COUNT",
            SpreadsheetConsolidationFunction.Maximum => "MAX",
            _ => "MIN",
        };
        var references = cells.Select(cell => $"'{cell.Worksheet.Name.Replace("'", "''", StringComparison.Ordinal)}'!{cell.Address.ToA1()}");
        return $"={name}({string.Join(',', references)})";
    }

    private static void ValidateRange(CellRange range)
    {
        if (range.Top < 0 || range.Left < 0 || range.Bottom >= SpreadsheetLimits.MaxRows || range.Right >= SpreadsheetLimits.MaxColumns)
            throw new ArgumentOutOfRangeException(nameof(range));
    }

    private readonly record struct AxisKey(string Key, CellValue Display, int Ordinal);
    private readonly record struct SourceCell(Worksheet Worksheet, CellAddress Address, CellValue Value);
}
