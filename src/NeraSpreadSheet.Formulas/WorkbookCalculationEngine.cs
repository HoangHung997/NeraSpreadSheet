using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed record WorkbookCalculationResult(int FormulaCellCount, int UpdatedCellCount, int ErrorCellCount);

public sealed class WorkbookCalculationEngine
{
    private readonly IFormulaEngine _formulaEngine;

    public WorkbookCalculationEngine(IFormulaEngine? formulaEngine = null)
    {
        _formulaEngine = formulaEngine ?? new NeraFormulaEngine();
    }

    public FormulaDependencyGraph DependencyGraph { get; } = new();

    public WorkbookCalculationResult Recalculate(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var states = new Dictionary<CellKey, VisitState>();
        var cache = new Dictionary<CellKey, CellValue>();
        var updates = new Dictionary<Worksheet, List<KeyValuePair<CellAddress, CellData>>>();
        var formulaCount = 0;
        var errors = 0;

        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var pair in worksheet.EnumerateUsedCells().ToArray())
            {
                if (pair.Value.Formula is null)
                {
                    continue;
                }

                formulaCount++;
                var value = EvaluateCell(workbook, worksheet, pair.Key, states, cache);
                if (value.Kind == CellValueKind.Error)
                {
                    errors++;
                }

                if (pair.Value.Value == value)
                {
                    continue;
                }

                if (!updates.TryGetValue(worksheet, out var worksheetUpdates))
                {
                    worksheetUpdates = [];
                    updates.Add(worksheet, worksheetUpdates);
                }

                worksheetUpdates.Add(new KeyValuePair<CellAddress, CellData>(pair.Key, new CellData(value, pair.Value.Formula, pair.Value.StyleId)));
            }
        }

        foreach (var (worksheet, worksheetUpdates) in updates)
        {
            foreach (var update in worksheetUpdates)
            {
                worksheet.SetCell(update.Key, update.Value);
            }
        }

        return new WorkbookCalculationResult(formulaCount, updates.Values.Sum(list => list.Count), errors);
    }

    private CellValue EvaluateCell(Workbook workbook, Worksheet worksheet, CellAddress address, IDictionary<CellKey, VisitState> states, IDictionary<CellKey, CellValue> cache)
    {
        var key = new CellKey(worksheet, address);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (states.TryGetValue(key, out var state) && state == VisitState.Visiting)
        {
            return CellValue.FromError("#CIRC!");
        }

        states[key] = VisitState.Visiting;
        var cell = worksheet.GetCell(address);
        CellValue value;
        if (cell.Formula is null)
        {
            value = cell.Value;
        }
        else
        {
            var context = new CalculationContext(this, workbook, worksheet, states, cache);
            var result = _formulaEngine.Evaluate(cell.Formula, context);
            value = result.Value;
            DependencyGraph.Replace(new FormulaCellKey(worksheet.Name, address), result.Dependencies);
        }

        states[key] = VisitState.Visited;
        cache[key] = value;
        return value;
    }

    private sealed class CalculationContext : IFormulaEvaluationContext
    {
        private readonly WorkbookCalculationEngine _owner;
        private readonly Workbook _workbook;
        private readonly Worksheet _currentWorksheet;
        private readonly IDictionary<CellKey, VisitState> _states;
        private readonly IDictionary<CellKey, CellValue> _cache;

        public CalculationContext(WorkbookCalculationEngine owner, Workbook workbook, Worksheet currentWorksheet, IDictionary<CellKey, VisitState> states, IDictionary<CellKey, CellValue> cache)
        {
            _owner = owner;
            _workbook = workbook;
            _currentWorksheet = currentWorksheet;
            _states = states;
            _cache = cache;
        }

        public CellValue GetCellValue(string? worksheetName, CellAddress address)
        {
            Worksheet worksheet;
            try
            {
                worksheet = worksheetName is null ? _currentWorksheet : _workbook.GetWorksheet(worksheetName);
            }
            catch (KeyNotFoundException)
            {
                return CellValue.FromError("#REF!");
            }

            return _owner.EvaluateCell(_workbook, worksheet, address, _states, _cache);
        }
    }

    private readonly record struct CellKey(Worksheet Worksheet, CellAddress Address);

    private enum VisitState { Visiting, Visited }
}
