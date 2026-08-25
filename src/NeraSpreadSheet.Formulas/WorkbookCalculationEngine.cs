using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed record WorkbookCalculationResult(
    int FormulaCellCount,
    int UpdatedCellCount,
    int ErrorCellCount);

public sealed class WorkbookCalculationEngine
{
    private readonly IFormulaEngine _formulaEngine;

    public WorkbookCalculationEngine(
        IFormulaEngine? formulaEngine = null)
    {
        _formulaEngine = formulaEngine ?? new NeraFormulaEngine();
    }

    public FormulaDependencyGraph DependencyGraph { get; } = new();

    public WorkbookCalculationResult Recalculate(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        foreach (var worksheet in workbook.Worksheets)
        {
            SpreadsheetTableFormulaProjection
                .RefreshMetadataFromCells(worksheet);
            SpreadsheetTableFormulaProjection.ProjectAll(worksheet);
        }

        DependencyGraph.Clear();
        var formulaCells = new List<FormulaCellKey>();
        foreach (var worksheet in workbook.Worksheets)
        {
            formulaCells.AddRange(worksheet.EnumerateUsedCells()
                .Where(pair => pair.Value.Formula is not null)
                .Select(pair => new FormulaCellKey(
                    worksheet.Name,
                    pair.Key)));
        }

        return RecalculateFormulaCells(workbook, formulaCells);
    }

    public WorkbookCalculationResult RecalculateAffected(
        Workbook workbook,
        Worksheet changedWorksheet,
        CellRange changedRange)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(changedWorksheet);

        if (DependencyGraph.FormulaCount == 0)
        {
            return Recalculate(workbook);
        }

        var candidates = new HashSet<FormulaCellKey>(
            DependencyGraph.GetTransitiveDependents(
                changedWorksheet.Name,
                changedRange));

        foreach (var pair in changedWorksheet.EnumerateUsedCells())
        {
            if (pair.Value.Formula is not null &&
                changedRange.Contains(pair.Key))
            {
                candidates.Add(new FormulaCellKey(
                    changedWorksheet.Name,
                    pair.Key));
            }
        }

        foreach (var formulaCell in DependencyGraph.FormulaCells)
        {
            if (!string.Equals(
                    formulaCell.WorksheetName,
                    changedWorksheet.Name,
                    StringComparison.OrdinalIgnoreCase) ||
                !changedRange.Contains(formulaCell.Address))
            {
                continue;
            }

            if (changedWorksheet.GetCell(
                    formulaCell.Address).Formula is null)
            {
                DependencyGraph.Remove(formulaCell);
            }
        }

        return RecalculateFormulaCells(workbook, candidates);
    }

    /// <summary>
    /// Recalculates formulas that depend on a committed changed range without
    /// evaluating formula cells inside that range. Spill transactions use this
    /// path so dependents observe the materialized values (including #SPILL!)
    /// rather than recursively re-evaluating the spill owner as a scalar.
    /// </summary>
    public WorkbookCalculationResult RecalculateDependents(
        Workbook workbook,
        Worksheet changedWorksheet,
        CellRange changedRange)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(changedWorksheet);
        if (DependencyGraph.FormulaCount == 0)
        {
            return new WorkbookCalculationResult(0, 0, 0);
        }

        return RecalculateFormulaCells(
            workbook,
            DependencyGraph.GetTransitiveDependents(
                changedWorksheet.Name,
                changedRange),
            new FrozenCalculationRange(
                changedWorksheet,
                changedRange));
    }

    private WorkbookCalculationResult RecalculateFormulaCells(
        Workbook workbook,
        IEnumerable<FormulaCellKey> formulaCells,
        FrozenCalculationRange? frozenRange = null)
    {
        var requested = formulaCells
            .Distinct()
            .Where(formulaCell =>
                !IsFrozenFormulaCell(
                    workbook,
                    formulaCell,
                    frozenRange))
            .ToArray();
        var states = new Dictionary<CellKey, VisitState>();
        var cache = new Dictionary<CellKey, CellValue>();
        var updates = new Dictionary<
            Worksheet,
            List<KeyValuePair<CellAddress, CellData>>>();
        var errors = 0;
        var evaluated = 0;

        foreach (var formulaCell in requested)
        {
            Worksheet worksheet;
            try
            {
                worksheet = workbook.GetWorksheet(
                    formulaCell.WorksheetName);
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
            var value = EvaluateCell(
                workbook,
                worksheet,
                formulaCell.Address,
                states,
                cache,
                frozenRange);
            if (value.Kind == CellValueKind.Error)
            {
                errors++;
            }

            if (current.Value == value)
            {
                continue;
            }

            if (!updates.TryGetValue(
                    worksheet,
                    out var worksheetUpdates))
            {
                worksheetUpdates = [];
                updates.Add(worksheet, worksheetUpdates);
            }

            worksheetUpdates.Add(
                new KeyValuePair<CellAddress, CellData>(
                    formulaCell.Address,
                    new CellData(
                        value,
                        current.Formula,
                        current.StyleId)));
        }

        foreach (var (worksheet, worksheetUpdates) in updates)
        {
            worksheet.SetCells(worksheetUpdates);
        }

        return new WorkbookCalculationResult(
            evaluated,
            updates.Values.Sum(list => list.Count),
            errors);
    }

    private CellValue EvaluateCell(
        Workbook workbook,
        Worksheet worksheet,
        CellAddress address,
        IDictionary<CellKey, VisitState> states,
        IDictionary<CellKey, CellValue> cache,
        FrozenCalculationRange? frozenRange)
    {
        var key = new CellKey(worksheet, address);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (frozenRange?.Contains(worksheet, address) == true)
        {
            var committed = worksheet.GetCell(address).Value;
            cache[key] = committed;
            return committed;
        }

        if (states.TryGetValue(key, out var state) &&
            state == VisitState.Visiting)
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
            var context = new CalculationContext(
                this,
                workbook,
                worksheet,
                address,
                states,
                cache,
                frozenRange);
            var result = _formulaEngine.Evaluate(
                cell.Formula,
                context);
            value = result.Value;
            DependencyGraph.Replace(
                new FormulaCellKey(worksheet.Name, address),
                result.Dependencies);
        }

        states[key] = VisitState.Visited;
        cache[key] = value;
        return value;
    }

    private static bool IsFrozenFormulaCell(
        Workbook workbook,
        FormulaCellKey formulaCell,
        FrozenCalculationRange? frozenRange)
    {
        if (frozenRange is null ||
            !string.Equals(
                formulaCell.WorksheetName,
                frozenRange.Value.Worksheet.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return ReferenceEquals(
                       workbook.GetWorksheet(formulaCell.WorksheetName),
                       frozenRange.Value.Worksheet) &&
                   frozenRange.Value.Range.Contains(formulaCell.Address);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private sealed class CalculationContext
        : IStructuredReferenceEvaluationContext,
          IFilterAwareFormulaEvaluationContext,
          IFormulaReferenceIntrospectionContext
    {
        private readonly WorkbookCalculationEngine _owner;
        private readonly Workbook _workbook;
        private readonly Worksheet _currentWorksheet;
        private readonly CellAddress _currentAddress;
        private readonly IDictionary<CellKey, VisitState> _states;
        private readonly IDictionary<CellKey, CellValue> _cache;
        private readonly FrozenCalculationRange? _frozenRange;

        public CalculationContext(
            WorkbookCalculationEngine owner,
            Workbook workbook,
            Worksheet currentWorksheet,
            CellAddress currentAddress,
            IDictionary<CellKey, VisitState> states,
            IDictionary<CellKey, CellValue> cache,
            FrozenCalculationRange? frozenRange)
        {
            _owner = owner;
            _workbook = workbook;
            _currentWorksheet = currentWorksheet;
            _currentAddress = currentAddress;
            _states = states;
            _cache = cache;
            _frozenRange = frozenRange;
        }

        public string CurrentWorksheetName => _currentWorksheet.Name;

        public CellAddress CurrentCellAddress => _currentAddress;

        public string ExpandStructuredReferences(string formula) =>
            StructuredReferenceFormulaTranslator.Translate(
                formula,
                _workbook,
                _currentWorksheet,
                _currentAddress);

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address)
        {
            if (!TryResolveWorksheet(
                    worksheetName,
                    out var worksheet))
            {
                return CellValue.FromError("#REF!");
            }

            return _owner.EvaluateCell(
                _workbook,
                worksheet,
                address,
                _states,
                _cache,
                _frozenRange);
        }

        public bool TryGetCellFormula(
            string? worksheetName,
            CellAddress address,
            out string? formula)
        {
            if (!TryResolveWorksheet(
                    worksheetName,
                    out var worksheet))
            {
                formula = null;
                return false;
            }

            formula = worksheet.GetCell(address).Formula;
            return true;
        }

        public bool IsRowVisible(
            string? worksheetName,
            int rowIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >= SpreadsheetLimits.MaxRows ||
                !TryResolveWorksheet(
                    worksheetName,
                    out var worksheet))
            {
                return false;
            }

            foreach (var table in worksheet.Tables)
            {
                if (table.DataRange is not { } dataRange ||
                    rowIndex < dataRange.Top ||
                    rowIndex > dataRange.Bottom ||
                    table.AutoFilter is not { Columns.Count: > 0 })
                {
                    continue;
                }

                foreach (var filter in table.AutoFilter.Columns)
                {
                    var columnIndex = table.GetColumnIndex(
                        filter.ColumnId);
                    var value = _owner.EvaluateCell(
                        _workbook,
                        worksheet,
                        new CellAddress(
                            rowIndex,
                            table.Range.Left + columnIndex),
                        _states,
                        _cache,
                        _frozenRange);
                    if (!filter.Matches(value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public IReadOnlyList<FormulaDependency>
            GetRowVisibilityDependencies(
                string? worksheetName,
                CellRange referencedRange)
        {
            if (!TryResolveWorksheet(
                    worksheetName,
                    out var worksheet))
            {
                return Array.Empty<FormulaDependency>();
            }

            var dependencies = new List<FormulaDependency>();
            foreach (var table in worksheet.Tables)
            {
                if (table.DataRange is not { } dataRange ||
                    table.AutoFilter is not { Columns.Count: > 0 })
                {
                    continue;
                }

                var top = Math.Max(
                    referencedRange.Top,
                    dataRange.Top);
                var bottom = Math.Min(
                    referencedRange.Bottom,
                    dataRange.Bottom);
                if (top > bottom)
                {
                    continue;
                }

                foreach (var filter in table.AutoFilter.Columns)
                {
                    var columnIndex = table.GetColumnIndex(
                        filter.ColumnId);
                    dependencies.Add(new FormulaDependency(
                        worksheetName,
                        new CellRange(
                            new CellAddress(
                                top,
                                table.Range.Left + columnIndex),
                            new CellAddress(
                                bottom,
                                table.Range.Left + columnIndex))));
                }
            }

            return dependencies.Distinct().ToArray();
        }

        private bool TryResolveWorksheet(
            string? worksheetName,
            out Worksheet worksheet)
        {
            if (worksheetName is null)
            {
                worksheet = _currentWorksheet;
                return true;
            }

            try
            {
                worksheet = _workbook.GetWorksheet(worksheetName);
                return true;
            }
            catch (KeyNotFoundException)
            {
                worksheet = null!;
                return false;
            }
        }
    }

    private readonly record struct CellKey(
        Worksheet Worksheet,
        CellAddress Address);

    private readonly record struct FrozenCalculationRange(
        Worksheet Worksheet,
        CellRange Range)
    {
        public bool Contains(
            Worksheet worksheet,
            CellAddress address) =>
            ReferenceEquals(Worksheet, worksheet) &&
            Range.Contains(address);
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }
}
