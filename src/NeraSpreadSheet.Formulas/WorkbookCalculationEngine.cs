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
        DependencyGraph.Clear();
        var formulaCells = new List<FormulaCellKey>();
        foreach (var worksheet in workbook.Worksheets)
        {
            formulaCells.AddRange(worksheet.EnumerateUsedCells()
                .Where(pair => pair.Value.Formula is not null)
                .Select(pair => new FormulaCellKey(worksheet.Name, pair.Key)));
        }
        return RecalculateFormulaCells(workbook, formulaCells);
    }

    public WorkbookCalculationResult RecalculateAffected(Workbook workbook, Worksheet changedWorksheet, CellRange changedRange)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(changedWorksheet);

        if (DependencyGraph.FormulaCount == 0)
        {
            return Recalculate(workbook);
        }

        var candidates = new HashSet<FormulaCellKey>(
            DependencyGraph.GetTransitiveDependents(changedWorksheet.Name, changedRange));

        foreach (var pair in changedWorksheet.EnumerateUsedCells())
        {
            if (pair.Value.Formula is not null && changedRange.Contains(pair.Key))
            {
                candidates.Add(new FormulaCellKey(changedWorksheet.Name, pair.Key));
            }
        }

        foreach (var formulaCell in DependencyGraph.FormulaCells)
        {
            if (!string.Equals(formulaCell.WorksheetName, changedWorksheet.Name, StringComparison.OrdinalIgnoreCase) ||
                !changedRange.Contains(formulaCell.Address))
            {
                continue;
            }

            if (changedWorksheet.GetCell(formulaCell.Address).Formula is null)
            {
                DependencyGraph.Remove(formulaCell);
            }
        }

        return RecalculateFormulaCells(workbook, candidates);
    }

    private WorkbookCalculationResult RecalculateFormulaCells(Workbook workbook, IEnumerable<FormulaCellKey> formulaCells)
    {
        var requested = formulaCells.Distinct().ToArray();
        var states = new Dictionary<CellKey, VisitState>();
        var cache = new Dictionary<CellKey, CellValue>();
        var updates = new Dictionary<Worksheet, List<KeyValuePair<CellAddress, CellData>>>();
        var errors = 0;
        var evaluated = 0;

        foreach (var formulaCell in requested)
        {
            Worksheet worksheet;
            try
            {
                worksheet = workbook.GetWorksheet(formulaCell.WorksheetName);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            var current = worksheet.GetCell(formulaCell.Address);
            if (current.Formula is null)
            {
                DependencyGraph.Remove(formulaCell);
                continue;
            }

            evaluated++;
            var value = EvaluateCell(workbook, worksheet, formulaCell.Address, states, cache);
            if (value.Kind == CellValueKind.Error)
            {
                errors++;
            }

            if (current.Value == value)
            {
                continue;
            }

            if (!updates.TryGetValue(worksheet, out var worksheetUpdates))
            {
                worksheetUpdates = [];
                updates.Add(worksheet, worksheetUpdates);
            }

            worksheetUpdates.Add(new KeyValuePair<CellAddress, CellData>(
                formulaCell.Address,
                new CellData(value, current.Formula, current.StyleId)));
        }

        foreach (var (worksheet, worksheetUpdates) in updates)
        {
            worksheet.SetCells(worksheetUpdates);
        }

        return new WorkbookCalculationResult(evaluated, updates.Values.Sum(list => list.Count), errors);
    }

    private CellValue EvaluateCell(
        Workbook workbook,
        Worksheet worksheet,
        CellAddress address,
        IDictionary<CellKey, VisitState> states,
        IDictionary<CellKey, CellValue> cache)
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

        public CalculationContext(
            WorkbookCalculationEngine owner,
            Workbook workbook,
            Worksheet currentWorksheet,
            IDictionary<CellKey, VisitState> states,
            IDictionary<CellKey, CellValue> cache)
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

    private enum VisitState
    {
        Visiting,
        Visited,
    }
}
