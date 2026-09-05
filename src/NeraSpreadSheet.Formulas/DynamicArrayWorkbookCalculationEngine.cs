using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Extends the scalar workbook calculation engine with bounded dynamic-array
/// stabilization and sparse worksheet spill materialization.
/// </summary>
public sealed class DynamicArrayWorkbookCalculationEngine
{
    public const int MaximumStabilizationPasses = 8;

    private readonly IDynamicArrayFormulaEngine _arrayEngine;
    private readonly WorkbookCalculationEngine _scalarCalculation;
    private readonly HashSet<FormulaCellKey> _dynamicOwners = [];
    private readonly Dictionary<FormulaCellKey, CellRange> _dynamicFootprints = [];

    public DynamicArrayWorkbookCalculationEngine(
        IFormulaEngine? scalarFormulaEngine = null,
        IDynamicArrayFormulaEngine? arrayEngine = null)
    {
        var scalar = scalarFormulaEngine ?? new NeraFormulaEngine();
        _arrayEngine = arrayEngine ??
            new NeraDynamicArrayFormulaEngine(scalar);
        _scalarCalculation = new WorkbookCalculationEngine(
            new DynamicArrayAwareFormulaEngine(
                scalar,
                _arrayEngine));
    }

    public FormulaDependencyGraph DependencyGraph =>
        _scalarCalculation.DependencyGraph;

    /// <summary>
    /// Prepares static scalar dependencies and discovers dynamic-array owners
    /// without changing cached workbook values.
    /// </summary>
    public int PrepareDependencyGraph(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var count = _scalarCalculation.PrepareDependencyGraph(workbook);
        _dynamicOwners.Clear();
        _dynamicFootprints.Clear();

        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var (address, cell) in worksheet.EnumerateUsedCells())
            {
                if (cell.Formula is null)
                {
                    continue;
                }
                var owner = new FormulaCellKey(worksheet.Name, address);
                var context = new DynamicCalculationContext(
                    workbook,
                    worksheet,
                    address);
                if (!_arrayEngine.TryEvaluate(
                        cell.Formula,
                        context,
                        out var result))
                {
                    continue;
                }

                _dynamicOwners.Add(owner);
                DependencyGraph.Replace(owner, result.Dependencies);
                if (result.IsSuccess &&
                    TryCreateFootprint(address, result.Value!, out var footprint))
                {
                    _dynamicFootprints[owner] = footprint;
                }
            }

            foreach (var spill in worksheet.GetFormulaSpills())
            {
                var owner = new FormulaCellKey(worksheet.Name, spill.Owner);
                _dynamicOwners.Add(owner);
                _dynamicFootprints[owner] = spill.Range;
            }
        }
        DependencyGraph.MarkPrepared();
        return count;
    }

    public WorkbookCalculationResult Recalculate(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var scalar = _scalarCalculation.Recalculate(workbook);
        return ReconcileDynamicArrays(
            workbook,
            scalar,
            DependencyGraph.FormulaCells);
    }

    public WorkbookCalculationResult RecalculateAffected(
        Workbook workbook,
        Worksheet changedWorksheet,
        CellRange changedRange)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(changedWorksheet);
        var candidates = new HashSet<FormulaCellKey>(
            DependencyGraph.GetTransitiveDependents(
                changedWorksheet.Name,
                changedRange));
        candidates.UnionWith(DependencyGraph.GetFormulaCells(
            changedWorksheet.Name,
            changedRange));
        foreach (var (owner, footprint) in _dynamicFootprints)
        {
            if (string.Equals(
                    owner.WorksheetName,
                    changedWorksheet.Name,
                    StringComparison.OrdinalIgnoreCase) &&
                footprint.Intersects(changedRange))
            {
                candidates.Add(owner);
            }
        }
        var scalar = _scalarCalculation.RecalculateAffected(
            workbook,
            changedWorksheet,
            changedRange);
        candidates.UnionWith(DependencyGraph.GetFormulaCells(
            changedWorksheet.Name,
            changedRange));
        return ReconcileDynamicArrays(workbook, scalar, candidates);
    }

    private WorkbookCalculationResult ReconcileDynamicArrays(
        Workbook workbook,
        WorkbookCalculationResult scalarResult,
        IEnumerable<FormulaCellKey> initialCandidates)
    {
        var formulaCellCount = scalarResult.FormulaCellCount;
        var updatedCellCount = scalarResult.UpdatedCellCount;
        var errorCellCount = scalarResult.ErrorCellCount;

        var candidates = new HashSet<FormulaCellKey>(initialCandidates);
        for (var pass = 0;
             pass < MaximumStabilizationPasses;
             pass++)
        {
            var changes = new Dictionary<Worksheet, CellRange>();
            var nextCandidates = new HashSet<FormulaCellKey>();
            foreach (var ownerKey in candidates.ToArray())
            {
                if (!TryResolveOwner(
                        workbook,
                        ownerKey,
                        out var worksheet,
                        out var cell) ||
                    cell.Formula is null)
                {
                    if (worksheet is not null &&
                        worksheet.TryGetFormulaSpill(
                            ownerKey.Address,
                            out var obsolete) &&
                        worksheet.ClearFormulaSpill(ownerKey.Address))
                    {
                        AddChange(changes, worksheet, obsolete!.Range);
                        updatedCellCount = checked(
                            updatedCellCount +
                            Math.Max(0, obsolete.Values.Count - 1));
                    }
                    _dynamicOwners.Remove(ownerKey);
                    _dynamicFootprints.Remove(ownerKey);
                    continue;
                }

                var address = ownerKey.Address;
                var context = new DynamicCalculationContext(
                    workbook,
                    worksheet,
                    address);
                if (!_arrayEngine.TryEvaluate(
                        cell.Formula,
                        context,
                        out var arrayResult))
                {
                    if (_dynamicOwners.Remove(ownerKey) &&
                        worksheet.TryGetFormulaSpill(address, out var obsolete) &&
                        worksheet.ClearFormulaSpill(address))
                    {
                        AddChange(changes, worksheet, obsolete!.Range);
                        updatedCellCount = checked(
                            updatedCellCount +
                            Math.Max(0, obsolete.Values.Count - 1));
                    }
                    _dynamicFootprints.Remove(ownerKey);
                    continue;
                }

                _dynamicOwners.Add(ownerKey);
                DependencyGraph.Replace(ownerKey, arrayResult.Dependencies);
                worksheet.TryGetFormulaSpill(address, out var previous);
                if (arrayResult.IsSuccess)
                {
                    if (TryCreateFootprint(
                            address,
                            arrayResult.Value!,
                            out var footprint))
                    {
                        _dynamicFootprints[ownerKey] = footprint;
                    }
                    if (previous is not null &&
                        previous.Values.Equals(arrayResult.Value))
                    {
                        continue;
                    }

                    var applied = worksheet.TryApplyFormulaSpill(
                        address,
                        arrayResult.Value!);
                    if (applied.IsApplied)
                    {
                        _dynamicFootprints[ownerKey] = applied.Spill!.Range;
                        AddChange(
                            changes,
                            worksheet,
                            Union(previous?.Range, applied.Spill.Range));
                        updatedCellCount = checked(
                            updatedCellCount +
                            EstimateChangedCells(previous, applied.Spill));
                    }
                    else
                    {
                        var current = worksheet.GetCell(address);
                        var alreadyCurrent =
                            previous is null &&
                            current.Value == CellValue.FromError("#SPILL!");
                        if (!alreadyCurrent)
                        {
                            worksheet.SetFormulaSpillError(address);
                            AddChange(
                                changes,
                                worksheet,
                                Union(
                                    previous?.Range,
                                    new CellRange(address, address)));
                            updatedCellCount = checked(
                                updatedCellCount +
                                (previous?.Values.Count ?? 0) + 1);
                            errorCellCount++;
                        }
                    }
                }
                else
                {
                    var current = worksheet.GetCell(address);
                    var alreadyCurrent =
                        previous is null &&
                        current.Value == arrayResult.ErrorValue;
                    if (!alreadyCurrent)
                    {
                        worksheet.SetFormulaSpillError(
                            address,
                            arrayResult.ErrorValue.ToString());
                        AddChange(
                            changes,
                            worksheet,
                            Union(
                                previous?.Range,
                                new CellRange(address, address)));
                        updatedCellCount = checked(
                            updatedCellCount +
                            (previous?.Values.Count ?? 0) + 1);
                        errorCellCount++;
                    }
                }
            }

            if (changes.Count == 0)
            {
                return new WorkbookCalculationResult(
                    formulaCellCount,
                    updatedCellCount,
                    errorCellCount);
            }

            foreach (var (worksheet, range) in changes)
            {
                foreach (var dependent in DependencyGraph.GetTransitiveDependents(
                             worksheet.Name,
                             range))
                {
                    if (_dynamicOwners.Contains(dependent))
                    {
                        nextCandidates.Add(dependent);
                    }
                }
                var affected = _scalarCalculation.RecalculateDependents(
                    workbook,
                    worksheet,
                    range);
                formulaCellCount = checked(
                    formulaCellCount + affected.FormulaCellCount);
                updatedCellCount = checked(
                    updatedCellCount + affected.UpdatedCellCount);
                errorCellCount = checked(
                    errorCellCount + affected.ErrorCellCount);
            }
            candidates = nextCandidates;
            if (candidates.Count == 0)
            {
                return new WorkbookCalculationResult(
                    formulaCellCount,
                    updatedCellCount,
                    errorCellCount);
            }
        }

        throw new InvalidOperationException(
            $"Dynamic-array calculation did not stabilize within " +
            $"{MaximumStabilizationPasses} passes.");
    }

    private static bool TryResolveOwner(
        Workbook workbook,
        FormulaCellKey owner,
        out Worksheet worksheet,
        out CellData cell)
    {
        try
        {
            worksheet = workbook.GetWorksheet(owner.WorksheetName);
            cell = worksheet.GetCell(owner.Address);
            return true;
        }
        catch (KeyNotFoundException)
        {
            worksheet = null!;
            cell = CellData.Empty;
            return false;
        }
    }

    private static bool TryCreateFootprint(
        CellAddress owner,
        FormulaArrayValue value,
        out CellRange footprint)
    {
        var bottom = (long)owner.RowIndex + value.RowCount - 1L;
        var right = (long)owner.ColumnIndex + value.ColumnCount - 1L;
        if (bottom >= SpreadsheetLimits.MaxRows ||
            right >= SpreadsheetLimits.MaxColumns)
        {
            footprint = default;
            return false;
        }
        footprint = new CellRange(
            owner,
            new CellAddress((int)bottom, (int)right));
        return true;
    }

    private static int EstimateChangedCells(
        FormulaSpillRange? previous,
        FormulaSpillRange current) =>
        checked(Math.Max(
            previous?.Values.Count ?? 0,
            current.Values.Count));

    private static void AddChange(
        Dictionary<Worksheet, CellRange> changes,
        Worksheet worksheet,
        CellRange range)
    {
        changes[worksheet] = changes.TryGetValue(
                worksheet,
                out var existing)
            ? Union(existing, range)
            : range;
    }

    private static CellRange Union(
        CellRange? left,
        CellRange right) =>
        left is null
            ? right
            : Union(left.Value, right);

    private static CellRange Union(
        CellRange left,
        CellRange right) =>
        new(
            new CellAddress(
                Math.Min(left.Top, right.Top),
                Math.Min(left.Left, right.Left)),
            new CellAddress(
                Math.Max(left.Bottom, right.Bottom),
                Math.Max(left.Right, right.Right)));

    private sealed class DynamicCalculationContext :
        IStructuredReferenceEvaluationContext,
        IFilterAwareFormulaEvaluationContext,
        IFormulaReferenceIntrospectionContext,
        IFormulaWorkbookMetadataEvaluationContext
    {
        private readonly Workbook _workbook;
        private readonly Worksheet _currentWorksheet;
        private readonly CellAddress _formulaAddress;
        private readonly Dictionary<Worksheet, WorksheetSnapshot>
            _snapshots = [];

        public DynamicCalculationContext(
            Workbook workbook,
            Worksheet currentWorksheet,
            CellAddress formulaAddress)
        {
            _workbook = workbook;
            _currentWorksheet = currentWorksheet;
            _formulaAddress = formulaAddress;
        }

        public string CurrentWorksheetName => _currentWorksheet.Name;

        public CellAddress CurrentCellAddress => _formulaAddress;

        public int WorksheetCount => _workbook.Worksheets.Count;

        public bool TryGetWorksheetIndex(
            string? worksheetName,
            out int oneBasedIndex)
        {
            var effectiveName = worksheetName ?? _currentWorksheet.Name;
            for (var index = 0; index < _workbook.Worksheets.Count; index++)
            {
                if (string.Equals(
                        _workbook.Worksheets[index].Name,
                        effectiveName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    oneBasedIndex = index + 1;
                    return true;
                }
            }

            oneBasedIndex = default;
            return false;
        }

        public string ExpandStructuredReferences(string formula) =>
            StructuredReferenceFormulaEngine.Expand(
                formula,
                _workbook,
                _currentWorksheet,
                _formulaAddress);

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            TryResolveWorksheet(worksheetName, out var worksheet)
                ? worksheet.GetCell(address).Value
                : CellValue.FromError("#REF!");

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
                !TryResolveWorksheet(worksheetName, out var worksheet))
            {
                return false;
            }
            return GetSnapshot(worksheet).IsRowVisible(rowIndex);
        }

        public IReadOnlyList<FormulaDependency>
            GetRowVisibilityDependencies(
                string? worksheetName,
                CellRange referencedRange)
        {
            if (!TryResolveWorksheet(worksheetName, out var worksheet))
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
                var top = Math.Max(referencedRange.Top, dataRange.Top);
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

            if (worksheet.AutoFilter is
                {
                    DataRange: { } worksheetDataRange,
                    Columns.Count: > 0,
                } worksheetFilter)
            {
                var top = Math.Max(
                    referencedRange.Top,
                    worksheetDataRange.Top);
                var bottom = Math.Min(
                    referencedRange.Bottom,
                    worksheetDataRange.Bottom);
                if (top <= bottom)
                {
                    foreach (var filter in worksheetFilter.Columns)
                    {
                        var column = checked(
                            worksheetFilter.Range.Left +
                            filter.ColumnOffset);
                        dependencies.Add(new FormulaDependency(
                            worksheetName,
                            new CellRange(
                                new CellAddress(top, column),
                                new CellAddress(bottom, column))));
                    }
                }
            }
            return dependencies.Distinct().ToArray();
        }

        private WorksheetSnapshot GetSnapshot(Worksheet worksheet)
        {
            if (!_snapshots.TryGetValue(worksheet, out var snapshot))
            {
                snapshot = WorksheetSnapshot.Capture(worksheet);
                _snapshots.Add(worksheet, snapshot);
            }
            return snapshot;
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
}
