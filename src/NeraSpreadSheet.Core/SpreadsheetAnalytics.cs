namespace NeraSpreadSheet.Core;

public enum SpreadsheetChartType
{
    Column,
    Bar,
    Line,
    Pie,
}

public sealed record SpreadsheetChartDefinition
{
    public SpreadsheetChartDefinition(
        Guid id,
        string name,
        SpreadsheetChartType chartType,
        CellRange sourceRange,
        string? title = null,
        bool firstRowContainsSeriesNames = true,
        bool firstColumnContainsCategories = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(chartType))
        {
            throw new ArgumentOutOfRangeException(nameof(chartType));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        ChartType = chartType;
        SourceRange = sourceRange;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        FirstRowContainsSeriesNames = firstRowContainsSeriesNames;
        FirstColumnContainsCategories = firstColumnContainsCategories;
    }

    public Guid Id { get; }

    public string Name { get; }

    public SpreadsheetChartType ChartType { get; }

    public CellRange SourceRange { get; }

    public string? Title { get; }

    public bool FirstRowContainsSeriesNames { get; }

    public bool FirstColumnContainsCategories { get; }
}

public sealed record SpreadsheetChartPoint(
    string Category,
    double? Value);

public sealed record SpreadsheetChartProjectedSeries(
    string Name,
    IReadOnlyList<SpreadsheetChartPoint> Points);

public sealed record SpreadsheetChartProjection(
    Guid ChartId,
    SpreadsheetChartType ChartType,
    string? Title,
    IReadOnlyList<SpreadsheetChartProjectedSeries> Series);

public static class SpreadsheetChartProjector
{
    public static SpreadsheetChartProjection Project(
        Worksheet worksheet,
        SpreadsheetChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(definition);

        var source = definition.SourceRange;
        var dataTop = source.Top +
                      (definition.FirstRowContainsSeriesNames ? 1 : 0);
        var dataLeft = source.Left +
                       (definition.FirstColumnContainsCategories ? 1 : 0);
        if (dataTop > source.Bottom || dataLeft > source.Right)
        {
            return new SpreadsheetChartProjection(
                definition.Id,
                definition.ChartType,
                definition.Title,
                []);
        }

        var series = new List<SpreadsheetChartProjectedSeries>();
        for (var column = dataLeft; column <= source.Right; column++)
        {
            var name = definition.FirstRowContainsSeriesNames
                ? FormatLabel(worksheet.GetCell(
                    new CellAddress(source.Top, column)).Value)
                : $"Series {column - dataLeft + 1}";
            if (string.IsNullOrEmpty(name))
            {
                name = $"Series {column - dataLeft + 1}";
            }

            var points = new List<SpreadsheetChartPoint>();
            for (var row = dataTop; row <= source.Bottom; row++)
            {
                var category = definition.FirstColumnContainsCategories
                    ? FormatLabel(worksheet.GetCell(
                        new CellAddress(row, source.Left)).Value)
                    : (row - dataTop + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(category))
                {
                    category = "(blank)";
                }

                var value = worksheet.GetCell(
                    new CellAddress(row, column)).Value;
                points.Add(new SpreadsheetChartPoint(
                    category,
                    value.Kind == CellValueKind.Number
                        ? (double)value.RawValue!
                        : null));
            }

            series.Add(new SpreadsheetChartProjectedSeries(name, points));
        }

        return new SpreadsheetChartProjection(
            definition.Id,
            definition.ChartType,
            definition.Title,
            series);
    }

    private static string FormatLabel(CellValue value) =>
        value.IsBlank ? string.Empty : value.ToString();
}

public enum SpreadsheetPivotAggregation
{
    Sum,
    Count,
    Average,
    Minimum,
    Maximum,
}

public sealed record SpreadsheetPivotDefinition
{
    public SpreadsheetPivotDefinition(
        Guid id,
        string name,
        CellRange sourceRange,
        int rowFieldColumnIndex,
        int valueFieldColumnIndex,
        SpreadsheetPivotAggregation aggregation = SpreadsheetPivotAggregation.Sum,
        bool firstRowContainsHeaders = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(aggregation))
        {
            throw new ArgumentOutOfRangeException(nameof(aggregation));
        }
        if (rowFieldColumnIndex < sourceRange.Left ||
            rowFieldColumnIndex > sourceRange.Right)
        {
            throw new ArgumentOutOfRangeException(nameof(rowFieldColumnIndex));
        }
        if (valueFieldColumnIndex < sourceRange.Left ||
            valueFieldColumnIndex > sourceRange.Right)
        {
            throw new ArgumentOutOfRangeException(nameof(valueFieldColumnIndex));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        SourceRange = sourceRange;
        RowFieldColumnIndex = rowFieldColumnIndex;
        ValueFieldColumnIndex = valueFieldColumnIndex;
        Aggregation = aggregation;
        FirstRowContainsHeaders = firstRowContainsHeaders;
    }

    public Guid Id { get; }

    public string Name { get; }

    public CellRange SourceRange { get; }

    public int RowFieldColumnIndex { get; }

    public int ValueFieldColumnIndex { get; }

    public SpreadsheetPivotAggregation Aggregation { get; }

    public bool FirstRowContainsHeaders { get; }
}

public sealed record SpreadsheetPivotRow(
    CellValue Key,
    string Label,
    double Value,
    int SourceRowCount,
    int NumericValueCount);

public sealed record SpreadsheetPivotProjection(
    Guid PivotId,
    string RowFieldName,
    string ValueFieldName,
    SpreadsheetPivotAggregation Aggregation,
    IReadOnlyList<SpreadsheetPivotRow> Rows);

public static class SpreadsheetPivotProjector
{
    public static SpreadsheetPivotProjection Project(
        Worksheet worksheet,
        SpreadsheetPivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(definition);

        var source = definition.SourceRange;
        var dataTop = source.Top +
                      (definition.FirstRowContainsHeaders ? 1 : 0);
        var rowFieldName = GetFieldName(
            worksheet,
            definition,
            definition.RowFieldColumnIndex,
            "Row");
        var valueFieldName = GetFieldName(
            worksheet,
            definition,
            definition.ValueFieldColumnIndex,
            "Value");

        var accumulators = new List<PivotAccumulator>();
        var groupIndexes = new Dictionary<CellValue, int>();
        for (var row = dataTop; row <= source.Bottom; row++)
        {
            var key = worksheet.GetCell(new CellAddress(
                row,
                definition.RowFieldColumnIndex)).Value;
            if (!groupIndexes.TryGetValue(key, out var groupIndex))
            {
                groupIndex = accumulators.Count;
                groupIndexes.Add(key, groupIndex);
                accumulators.Add(new PivotAccumulator(key));
            }

            var value = worksheet.GetCell(new CellAddress(
                row,
                definition.ValueFieldColumnIndex)).Value;
            accumulators[groupIndex].Add(value);
        }

        var rows = accumulators
            .Select(accumulator => accumulator.ToRow(definition.Aggregation))
            .ToArray();
        return new SpreadsheetPivotProjection(
            definition.Id,
            rowFieldName,
            valueFieldName,
            definition.Aggregation,
            rows);
    }

    private static string GetFieldName(
        Worksheet worksheet,
        SpreadsheetPivotDefinition definition,
        int columnIndex,
        string fallback)
    {
        if (!definition.FirstRowContainsHeaders)
        {
            return fallback;
        }

        var value = worksheet.GetCell(new CellAddress(
            definition.SourceRange.Top,
            columnIndex)).Value;
        return value.IsBlank ? fallback : value.ToString();
    }

    private sealed class PivotAccumulator
    {
        private double _sum;
        private double _minimum = double.PositiveInfinity;
        private double _maximum = double.NegativeInfinity;

        public PivotAccumulator(CellValue key)
        {
            Key = key;
        }

        public CellValue Key { get; }

        public int SourceRowCount { get; private set; }

        public int NumericValueCount { get; private set; }

        public int NonBlankValueCount { get; private set; }

        public void Add(CellValue value)
        {
            SourceRowCount++;
            if (!value.IsBlank)
            {
                NonBlankValueCount++;
            }
            if (value.Kind != CellValueKind.Number)
            {
                return;
            }

            var number = (double)value.RawValue!;
            NumericValueCount++;
            _sum += number;
            _minimum = Math.Min(_minimum, number);
            _maximum = Math.Max(_maximum, number);
        }

        public SpreadsheetPivotRow ToRow(
            SpreadsheetPivotAggregation aggregation)
        {
            var value = aggregation switch
            {
                SpreadsheetPivotAggregation.Count => NonBlankValueCount,
                SpreadsheetPivotAggregation.Average => NumericValueCount == 0
                    ? 0d
                    : _sum / NumericValueCount,
                SpreadsheetPivotAggregation.Minimum => NumericValueCount == 0
                    ? 0d
                    : _minimum,
                SpreadsheetPivotAggregation.Maximum => NumericValueCount == 0
                    ? 0d
                    : _maximum,
                _ => _sum,
            };
            var label = Key.IsBlank ? "(blank)" : Key.ToString();
            return new SpreadsheetPivotRow(
                Key,
                label,
                value,
                SourceRowCount,
                NumericValueCount);
        }
    }
}
