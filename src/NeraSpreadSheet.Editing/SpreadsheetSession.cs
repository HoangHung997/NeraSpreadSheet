using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetSession
{
    public SpreadsheetSession(Workbook workbook, Worksheet? activeWorksheet = null)
    {
        Workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        ActiveWorksheet = activeWorksheet ?? (workbook.Worksheets.Count > 0
            ? workbook.Worksheets[0]
            : throw new ArgumentException("Workbook must contain at least one worksheet.", nameof(workbook)));
        if (!workbook.Worksheets.Contains(ActiveWorksheet))
        {
            throw new ArgumentException("Active worksheet must belong to the workbook.", nameof(activeWorksheet));
        }
    }

    public Workbook Workbook { get; }
    public Worksheet ActiveWorksheet { get; private set; }
    public SelectionModel Selection { get; } = new();
    public UndoRedoManager History { get; } = new();
    public WorkbookCalculationEngine Calculation { get; } = new();

    public void ActivateWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!Workbook.Worksheets.Contains(worksheet))
        {
            throw new ArgumentException("Worksheet must belong to the session workbook.", nameof(worksheet));
        }
        ActiveWorksheet = worksheet;
        Selection.SetActiveCell(default);
    }

    public void SetValue(CellAddress address, object? value)
    {
        var current = ActiveWorksheet.GetCell(address);
        var next = new CellData(CellValue.FromObject(value), styleId: current.StyleId);
        Execute(new SetCellsOperation(
            ActiveWorksheet,
            [new KeyValuePair<CellAddress, CellData>(address, next)],
            "Set cell value"));
    }

    public void SetFormula(CellAddress address, string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        var normalized = formula.StartsWith('=') ? formula : $"={formula}";
        var current = ActiveWorksheet.GetCell(address);
        var next = new CellData(current.Value, normalized, current.StyleId);
        Execute(new SetCellsOperation(
            ActiveWorksheet,
            [new KeyValuePair<CellAddress, CellData>(address, next)],
            "Set formula"));
    }

    public bool ClearSelection()
    {
        var updates = ActiveWorksheet.EnumerateUsedCells()
            .Where(pair => Selection.Ranges.Any(range => range.Contains(pair.Key)))
            .Select(pair => new KeyValuePair<CellAddress, CellData>(pair.Key, CellData.Empty))
            .ToArray();
        if (updates.Length == 0)
        {
            return false;
        }

        Execute(new SetCellsOperation(ActiveWorksheet, updates, "Clear contents"));
        return true;
    }

    public void Execute(ISpreadsheetEditOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Workbook.Worksheets.Contains(operation.Worksheet))
        {
            throw new ArgumentException("Operation worksheet must belong to the session workbook.", nameof(operation));
        }

        History.Execute(operation);
        Calculation.RecalculateAffected(Workbook, operation.Worksheet, operation.AffectedRange);
    }

    public bool Undo()
    {
        if (!History.Undo())
        {
            return false;
        }
        Calculation.Recalculate(Workbook);
        return true;
    }

    public bool Redo()
    {
        if (!History.Redo())
        {
            return false;
        }
        Calculation.Recalculate(Workbook);
        return true;
    }

    public WorkbookCalculationResult Recalculate() => Calculation.Recalculate(Workbook);
}
