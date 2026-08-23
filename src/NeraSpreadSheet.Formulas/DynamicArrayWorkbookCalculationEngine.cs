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

    public WorkbookCalculationResult Recalculate(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var scalar = _scalarCalculation.Recalculate(workbook);
        return ReconcileDynamicArrays(workbook, scalar);
    }

    public WorkbookCalculationResult RecalculateAffected(
        Workbook workbook,
        Worksheet changedWorksheet,
        CellRange changedRange)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(changedWorksheet);
        var scalar = _scalarCalculation.RecalculateAffected(
            workbook,
            changedWorksheet,
            changedRange);
        return ReconcileDynamicArrays(workbook, scalar);
    }

    private WorkbookCalculationResult ReconcileDynamicArrays(
        Workbook workbook,
        WorkbookCalculationResult scalarResult)
    {
        var formulaCellCount = scalarResult.FormulaCellCount;
        var updatedCellCount = scalarResult.UpdatedCellCount;
        var errorCellCount = scalarResult.ErrorCellCount;

        for (var pass = 0;
             pass < MaximumStabilizationPasses;
             pass++)
        {
            var changes = new Dictionary<Worksheet, CellRange>();
            var dynamicOwners = new HashSet<FormulaCellKey>();
            foreach (var worksheet in workbook.Worksheets)
            {
                var formulaCells = worksheet.EnumerateUsedCells()
                    .Where(static pair => pair.Value.Formula is not null)
                    .ToArray();
                foreach (var (address, cell) in formulaCells)
                {
                    var context = new DynamicCalculationContext(
                        workbook,
                        worksheet,
                        address);
                    if (!_arrayEngine.TryEvaluate(
                            cell.Formula!,
                            context,
                            out var arrayResult))
                    {
                        continue;
                    }

                    var ownerKey = new FormulaCellKey(
                        worksheet.Name,
                        address);
                    dynamicOwners.Add(ownerKey);
                    DependencyGraph.Replace(
                        ownerKey,
                        arrayResult.Dependencies);
                    worksheet.TryGetFormulaSpill(
                        address,
                        out var previous);
                    if (arrayResult.IsSuccess)
                    {
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
                            AddChange(
                                changes,
                                worksheet,
                                Union(previous?.Range, applied.Spill!.Range));
                            updatedCellCount = checked(
                                updatedCellCount +
                                EstimateChangedCells(previous, applied.Spill));
                        }
                        else
                        {
                            var current = worksheet.GetCell(address);
                            var alreadyBlocked =
                                previous is null &&
                                current.Value ==
                                CellValue.FromError("#SPILL!");
                            if (alreadyBlocked)
                            {
                                continue;
                            }

                            worksheet.SetFormulaSpillError(address);
                            AddChange(
                                changes,
                                worksheet,
                                Union(previous?.Range, new CellRange(address, address)));
                            updatedCellCount = checked(
                                updatedCellCount +
                                (previous?.Values.Count ?? 0) + 1);
                            errorCellCount++;
                        }
                    }
                    else
                    {
                        var current = worksheet.GetCell(address);
                        var alreadyCurrent =
                            previous is null &&
                            current.Value == arrayResult.ErrorValue;
                        if (alreadyCurrent)
                        {
                            continue;
                        }

                        worksheet.SetFormulaSpillError(
                            address,
                            arrayResult.ErrorValue.ToString());
                        AddChange(
                            changes,
                            worksheet,
                            Union(previous?.Range, new CellRange(address, address)));
                        updatedCellCount = checked(
                            updatedCellCount +
                            (previous?.Values.Count ?? 0) + 1);
                        errorCellCount++;
                    }
                }
            }

            foreach (var worksheet in workbook.Worksheets)
            {
                foreach (var spill in worksheet.GetFormulaSpills())
                {
                    var owner = new FormulaCellKey(
                        worksheet.Name,
                        spill.Owner);
                    if (dynamicOwners.Contains(owner))
                    {
                        continue;
                    }
                    if (worksheet.ClearFormulaSpill(spill.Owner))
                    {
                        AddChange(changes, worksheet, spill.Range);
                        updatedCellCount = checked(
                            updatedCellCount +
                            Math.Max(0, spill.Values.Count - 1));
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
        }

        throw new InvalidOperationException(
            $"Dynamic-array calculation did not stabilize within " +
            $"{MaximumStabilizationPasses} passes.");
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
        IFilterAwareFormulaEvaluationContext
    {
        private readonly Workbook _workbook;
        private readonly Worksheet _currentWorksheet;
        private readonly CellAddress _formulaAddress;
        private readonly Dictionary<Worksheet, WorksheetSnapshot> _snapshots = [];

        public DynamicCalculationContext(
            Workbook workbook,
            Worksheet currentWorksheet,
            CellAddress formulaAddress)
        {
            _workbook = workbook;
            _currentWorksheet = currentWorksheet;
            _formulaAddress = formulaAddress;
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
                var bottom = Math.Min(referencedRange.Bottom, dataRange.Bottom);
                if (top > bottom)
                {
                    continue;
                }
                foreach (var filter in table.AutoFilter.Columns)
                {
                    var columnIndex = table.GetColumnIndex(filter.ColumnId);
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
