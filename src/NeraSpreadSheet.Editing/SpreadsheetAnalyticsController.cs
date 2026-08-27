using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetAnalyticsChangeKind
{
    ChartAdded,
    ChartRemoved,
    PivotAdded,
    PivotRemoved,
}

public sealed class SpreadsheetAnalyticsChangedEventArgs : EventArgs
{
    public SpreadsheetAnalyticsChangedEventArgs(
        Worksheet worksheet,
        SpreadsheetAnalyticsChangeKind changeKind,
        Guid itemId)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        ChangeKind = changeKind;
        ItemId = itemId;
    }

    public Worksheet Worksheet { get; }

    public SpreadsheetAnalyticsChangeKind ChangeKind { get; }

    public Guid ItemId { get; }
}

public sealed class SpreadsheetAnalyticsController
{
    private readonly SpreadsheetSession _session;
    private readonly Dictionary<Worksheet, List<SpreadsheetChartDefinition>>
        _charts = [];
    private readonly Dictionary<Worksheet, List<SpreadsheetPivotDefinition>>
        _pivots = [];

    public SpreadsheetAnalyticsController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public event EventHandler<SpreadsheetAnalyticsChangedEventArgs>? Changed;

    public IReadOnlyList<SpreadsheetChartDefinition> Charts =>
        GetChartList(_session.ActiveWorksheet);

    public IReadOnlyList<SpreadsheetPivotDefinition> Pivots =>
        GetPivotList(_session.ActiveWorksheet);

    public bool CanInsertChartFromSelection =>
        TryGetPrimarySelection(out var range) &&
        range.RowCount >= 2 &&
        range.ColumnCount >= 2;

    public bool CanInsertPivotFromSelection =>
        TryGetPrimarySelection(out var range) &&
        range.RowCount >= 2 &&
        range.ColumnCount >= 2;

    public IReadOnlyList<SpreadsheetChartDefinition> GetCharts(
        Worksheet worksheet)
    {
        EnsureWorksheet(worksheet);
        return GetChartList(worksheet);
    }

    public IReadOnlyList<SpreadsheetPivotDefinition> GetPivots(
        Worksheet worksheet)
    {
        EnsureWorksheet(worksheet);
        return GetPivotList(worksheet);
    }

    public SpreadsheetChartDefinition InsertChartFromSelection(
        SpreadsheetChartType chartType,
        string? title = null)
    {
        if (!TryGetPrimarySelection(out var range) ||
            range.RowCount < 2 ||
            range.ColumnCount < 2)
        {
            throw new InvalidOperationException(
                "A chart requires a selection with at least two rows and two columns.");
        }

        return InsertChart(range, chartType, title);
    }

    public SpreadsheetChartDefinition InsertChart(
        CellRange sourceRange,
        SpreadsheetChartType chartType,
        string? title = null,
        string? requestedName = null)
    {
        if (sourceRange.RowCount < 2 || sourceRange.ColumnCount < 2)
        {
            throw new ArgumentException(
                "A chart source range must contain at least two rows and two columns.",
                nameof(sourceRange));
        }
        if (!Enum.IsDefined(chartType))
        {
            throw new ArgumentOutOfRangeException(nameof(chartType));
        }

        var worksheet = _session.ActiveWorksheet;
        var list = GetChartList(worksheet);
        var name = requestedName is null
            ? GenerateUniqueName(list.Select(static chart => chart.Name), "Chart")
            : ValidateUniqueName(
                requestedName,
                list.Select(static chart => chart.Name),
                nameof(requestedName));
        var definition = new SpreadsheetChartDefinition(
            Guid.NewGuid(),
            name,
            chartType,
            sourceRange,
            title);
        var index = list.Count;
        _session.Execute(new AnalyticsOperation(
            worksheet,
            sourceRange,
            $"Insert {chartType} chart",
            () =>
            {
                list.Insert(index, definition);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.ChartAdded,
                    definition.Id);
            },
            () =>
            {
                RemoveAt(list, index, definition.Id);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.ChartRemoved,
                    definition.Id);
            }));
        return definition;
    }

    public bool RemoveChart(Guid chartId)
    {
        var worksheet = _session.ActiveWorksheet;
        var list = GetChartList(worksheet);
        var index = list.FindIndex(chart => chart.Id == chartId);
        if (index < 0)
        {
            return false;
        }

        var definition = list[index];
        _session.Execute(new AnalyticsOperation(
            worksheet,
            definition.SourceRange,
            "Remove chart",
            () =>
            {
                RemoveAt(list, index, definition.Id);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.ChartRemoved,
                    definition.Id);
            },
            () =>
            {
                list.Insert(index, definition);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.ChartAdded,
                    definition.Id);
            }));
        return true;
    }

    public SpreadsheetChartProjection ProjectChart(Guid chartId)
    {
        var worksheet = _session.ActiveWorksheet;
        var definition = GetChartList(worksheet)
            .FirstOrDefault(chart => chart.Id == chartId)
            ?? throw new KeyNotFoundException($"Chart '{chartId}' was not found.");
        return SpreadsheetChartProjector.Project(worksheet, definition);
    }

    public SpreadsheetPivotDefinition InsertPivotFromSelection(
        SpreadsheetPivotAggregation aggregation = SpreadsheetPivotAggregation.Sum)
    {
        if (!TryGetPrimarySelection(out var range) ||
            range.RowCount < 2 ||
            range.ColumnCount < 2)
        {
            throw new InvalidOperationException(
                "A pivot requires a selection with at least two rows and two columns.");
        }

        return InsertPivot(
            range,
            range.Left,
            range.Left + 1,
            aggregation);
    }

    public SpreadsheetPivotDefinition InsertPivot(
        CellRange sourceRange,
        int rowFieldColumnIndex,
        int valueFieldColumnIndex,
        SpreadsheetPivotAggregation aggregation = SpreadsheetPivotAggregation.Sum,
        string? requestedName = null)
    {
        if (sourceRange.RowCount < 2 || sourceRange.ColumnCount < 2)
        {
            throw new ArgumentException(
                "A pivot source range must contain at least two rows and two columns.",
                nameof(sourceRange));
        }

        var worksheet = _session.ActiveWorksheet;
        var list = GetPivotList(worksheet);
        var name = requestedName is null
            ? GenerateUniqueName(list.Select(static pivot => pivot.Name), "Pivot")
            : ValidateUniqueName(
                requestedName,
                list.Select(static pivot => pivot.Name),
                nameof(requestedName));
        var definition = new SpreadsheetPivotDefinition(
            Guid.NewGuid(),
            name,
            sourceRange,
            rowFieldColumnIndex,
            valueFieldColumnIndex,
            aggregation);
        var index = list.Count;
        _session.Execute(new AnalyticsOperation(
            worksheet,
            sourceRange,
            $"Insert {aggregation} pivot",
            () =>
            {
                list.Insert(index, definition);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.PivotAdded,
                    definition.Id);
            },
            () =>
            {
                RemoveAt(list, index, definition.Id);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.PivotRemoved,
                    definition.Id);
            }));
        return definition;
    }

    public bool RemovePivot(Guid pivotId)
    {
        var worksheet = _session.ActiveWorksheet;
        var list = GetPivotList(worksheet);
        var index = list.FindIndex(pivot => pivot.Id == pivotId);
        if (index < 0)
        {
            return false;
        }

        var definition = list[index];
        _session.Execute(new AnalyticsOperation(
            worksheet,
            definition.SourceRange,
            "Remove pivot",
            () =>
            {
                RemoveAt(list, index, definition.Id);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.PivotRemoved,
                    definition.Id);
            },
            () =>
            {
                list.Insert(index, definition);
                Publish(
                    worksheet,
                    SpreadsheetAnalyticsChangeKind.PivotAdded,
                    definition.Id);
            }));
        return true;
    }

    public SpreadsheetPivotProjection ProjectPivot(Guid pivotId)
    {
        var worksheet = _session.ActiveWorksheet;
        var definition = GetPivotList(worksheet)
            .FirstOrDefault(pivot => pivot.Id == pivotId)
            ?? throw new KeyNotFoundException($"Pivot '{pivotId}' was not found.");
        return SpreadsheetPivotProjector.Project(worksheet, definition);
    }

    private List<SpreadsheetChartDefinition> GetChartList(
        Worksheet worksheet)
    {
        if (!_charts.TryGetValue(worksheet, out var list))
        {
            list = [];
            _charts.Add(worksheet, list);
        }
        return list;
    }

    private List<SpreadsheetPivotDefinition> GetPivotList(
        Worksheet worksheet)
    {
        if (!_pivots.TryGetValue(worksheet, out var list))
        {
            list = [];
            _pivots.Add(worksheet, list);
        }
        return list;
    }

    private bool TryGetPrimarySelection(out CellRange range)
    {
        if (_session.Selection.Ranges.Count == 0)
        {
            range = default;
            return false;
        }

        range = _session.Selection.Ranges[0];
        return true;
    }

    private void EnsureWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!_session.Workbook.Worksheets.Contains(worksheet))
        {
            throw new ArgumentException(
                "Worksheet must belong to the session workbook.",
                nameof(worksheet));
        }
    }

    private void Publish(
        Worksheet worksheet,
        SpreadsheetAnalyticsChangeKind changeKind,
        Guid itemId) =>
        Changed?.Invoke(
            this,
            new SpreadsheetAnalyticsChangedEventArgs(
                worksheet,
                changeKind,
                itemId));

    private static string GenerateUniqueName(
        IEnumerable<string> names,
        string prefix)
    {
        var existing = new HashSet<string>(
            names,
            StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var candidate = $"{prefix}{index}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string ValidateUniqueName(
        string requestedName,
        IEnumerable<string> names,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedName);
        var normalized = requestedName.Trim();
        if (names.Any(name => string.Equals(
                name,
                normalized,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"An analytics item named '{normalized}' already exists on this worksheet.");
        }
        return normalized;
    }

    private static void RemoveAt<T>(
        List<T> items,
        int index,
        Guid expectedId)
        where T : class
    {
        if (index < 0 || index >= items.Count)
        {
            throw new InvalidOperationException(
                "Analytics history no longer matches the collection state.");
        }

        var actualId = items[index] switch
        {
            SpreadsheetChartDefinition chart => chart.Id,
            SpreadsheetPivotDefinition pivot => pivot.Id,
            _ => Guid.Empty,
        };
        if (actualId != expectedId)
        {
            throw new InvalidOperationException(
                "Analytics history no longer matches the collection identity.");
        }
        items.RemoveAt(index);
    }

    private sealed class AnalyticsOperation : ISpreadsheetEditOperation
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public AnalyticsOperation(
            Worksheet worksheet,
            CellRange affectedRange,
            string description,
            Action execute,
            Action undo)
        {
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            AffectedRange = affectedRange;
            Description = description.Trim();
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public string Description { get; }

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public bool AffectsCalculation => false;

        public void Execute() => _execute();

        public void Undo() => _undo();
    }
}

public static class SpreadsheetAnalyticsCommandIds
{
    public static CommandId InsertColumnChart { get; } =
        new("Insert.Chart.Column");
    public static CommandId InsertBarChart { get; } =
        new("Insert.Chart.Bar");
    public static CommandId InsertLineChart { get; } =
        new("Insert.Chart.Line");
    public static CommandId InsertPieChart { get; } =
        new("Insert.Chart.Pie");
    public static CommandId InsertSumPivot { get; } =
        new("Insert.Pivot.Sum");
}

public static class SpreadsheetAnalyticsCommandCatalog
{
    public static void Register(
        CommandRegistry registry,
        SpreadsheetAnalyticsController controller)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(controller);

        RegisterChart(
            registry,
            controller,
            SpreadsheetAnalyticsCommandIds.InsertColumnChart,
            "Insert column chart",
            SpreadsheetChartType.Column);
        RegisterChart(
            registry,
            controller,
            SpreadsheetAnalyticsCommandIds.InsertBarChart,
            "Insert bar chart",
            SpreadsheetChartType.Bar);
        RegisterChart(
            registry,
            controller,
            SpreadsheetAnalyticsCommandIds.InsertLineChart,
            "Insert line chart",
            SpreadsheetChartType.Line);
        RegisterChart(
            registry,
            controller,
            SpreadsheetAnalyticsCommandIds.InsertPieChart,
            "Insert pie chart",
            SpreadsheetChartType.Pie);

        registry.Register(
            new CommandDescriptor(
                SpreadsheetAnalyticsCommandIds.InsertSumPivot,
                "Insert pivot summary"),
            new AnalyticsCommandHandler(
                () => controller.CanInsertPivotFromSelection,
                () => controller.InsertPivotFromSelection()));
    }

    private static void RegisterChart(
        CommandRegistry registry,
        SpreadsheetAnalyticsController controller,
        CommandId commandId,
        string label,
        SpreadsheetChartType chartType)
    {
        registry.Register(
            new CommandDescriptor(commandId, label),
            new AnalyticsCommandHandler(
                () => controller.CanInsertChartFromSelection,
                () => controller.InsertChartFromSelection(chartType)));
    }

    private sealed class AnalyticsCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        public AnalyticsCommandHandler(
            Func<bool> canExecute,
            Action execute)
        {
            _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(CommandContext context) => _canExecute();

        public CommandState GetState(CommandContext context) =>
            new(_canExecute());

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
