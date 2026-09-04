using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetSession
{
    public SpreadsheetSession(
        Workbook workbook,
        Worksheet? activeWorksheet = null)
    {
        Workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        ActiveWorksheet = activeWorksheet ??
            (workbook.Worksheets.Count > 0
                ? workbook.Worksheets[0]
                : throw new ArgumentException(
                    "Workbook must contain at least one worksheet.",
                    nameof(workbook)));
        if (!workbook.Worksheets.Contains(ActiveWorksheet))
        {
            throw new ArgumentException(
                "Active worksheet must belong to the workbook.",
                nameof(activeWorksheet));
        }

        Clipboard = new SpreadsheetClipboardController(this);
        Styles = new SpreadsheetStyleController(this);
        Merge = new SpreadsheetMergeController(this);
        Sort = new SpreadsheetSortController(this);
        Validation = new SpreadsheetDataValidationController(this);
        Tables = new SpreadsheetTableController(this);
        Analytics = new SpreadsheetAnalyticsController(this);
        AnalyticsPlacements = new SpreadsheetAnalyticsPlacementController(
            this,
            Analytics);
        AnalyticsInteraction = new SpreadsheetAnalyticsInteractionController();
        Analytics.Changed += OnAnalyticsChanged;
        WorksheetFilter =
            new SpreadsheetWorksheetAutoFilterController(this);
        Editor = new SpreadsheetCellEditorController(this);
        FormulaEditing = new SpreadsheetFormulaEditingAssistant();
        View = new SpreadsheetViewController(this);
        Structure = new SpreadsheetStructureController(this);
        Reorder = new SpreadsheetAxisReorderController(this);
        AxisVisibility = new SpreadsheetAxisVisibilityController(this);
        Commands = new CommandRegistry();
        SpreadsheetCommandCatalog.Register(Commands, this);
        SpreadsheetClipboardCommandCatalog.Register(Commands, Clipboard);
        SpreadsheetFormattingCommandCatalog.Register(Commands, Styles);
        SpreadsheetMergeCommandCatalog.Register(Commands, Merge);
        SpreadsheetSortCommandCatalog.Register(Commands, Sort);
        SpreadsheetViewCommandCatalog.Register(Commands, View);
        SpreadsheetStructureCommandCatalog.Register(
            Commands,
            this,
            Structure,
            AxisVisibility);
        SpreadsheetAnalyticsCommandCatalog.Register(
            Commands,
            Analytics);
        CommandDispatcher = new CommandDispatcher(Commands);
        Calculation.PrepareDependencyGraph(Workbook);
    }

    public Workbook Workbook { get; }

    public Worksheet ActiveWorksheet { get; private set; }

    public SelectionModel Selection { get; } = new();

    public UndoRedoManager History { get; } = new();

    public DynamicArrayWorkbookCalculationEngine Calculation { get; } = new();

    public SpreadsheetClipboardController Clipboard { get; }

    public SpreadsheetStyleController Styles { get; }

    public SpreadsheetMergeController Merge { get; }

    public SpreadsheetSortController Sort { get; }

    public SpreadsheetDataValidationController Validation { get; }

    public SpreadsheetTableController Tables { get; }

    public SpreadsheetAnalyticsController Analytics { get; }

    public SpreadsheetAnalyticsPlacementController AnalyticsPlacements { get; }

    public SpreadsheetAnalyticsInteractionController AnalyticsInteraction { get; }

    public SpreadsheetWorksheetAutoFilterController
        WorksheetFilter
    { get; }

    public SpreadsheetCellEditorController Editor { get; }

    public SpreadsheetFormulaEditingAssistant FormulaEditing { get; }

    public SpreadsheetViewController View { get; }

    public SpreadsheetStructureController Structure { get; }

    public SpreadsheetAxisReorderController Reorder { get; }

    public SpreadsheetAxisVisibilityController AxisVisibility { get; }

    public CommandRegistry Commands { get; }

    public CommandDispatcher CommandDispatcher { get; }

    public event EventHandler? ActiveWorksheetChanged;

    public void ActivateWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!Workbook.Worksheets.Contains(worksheet))
        {
            throw new ArgumentException(
                "Worksheet must belong to the session workbook.",
                nameof(worksheet));
        }
        if (ReferenceEquals(ActiveWorksheet, worksheet))
        {
            return;
        }

        Editor.Cancel();
        AnalyticsInteraction.ClearSelection();
        ActiveWorksheet = worksheet;
        Selection.SetActiveCell(default);
        View.NotifyActiveWorksheetChanged();
        ActiveWorksheetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetValue(CellAddress address, object? value)
    {
        address = ActiveWorksheet.ResolveMergedAnchor(address);
        EnsureSpillCellEditable(address);
        var current = ActiveWorksheet.GetCell(address);
        var next = new CellData(
            CellValue.FromObject(value),
            styleId: current.StyleId);
        Execute(new SetCellsOperation(
            ActiveWorksheet,
            [new KeyValuePair<CellAddress, CellData>(address, next)],
            "Set cell value"));
    }

    public void SetFormula(CellAddress address, string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        address = ActiveWorksheet.ResolveMergedAnchor(address);
        EnsureSpillCellEditable(address);
        var normalized = formula.StartsWith('=')
            ? formula
            : $"={formula}";
        var current = ActiveWorksheet.GetCell(address);
        var next = new CellData(
            current.Value,
            normalized,
            current.StyleId);
        Execute(new SetCellsOperation(
            ActiveWorksheet,
            [new KeyValuePair<CellAddress, CellData>(address, next)],
            "Set formula"));
    }

    public bool ClearSelection()
    {
        var spills = ActiveWorksheet.GetFormulaSpills();
        foreach (var spill in spills)
        {
            var intersects = Selection.Ranges.Any(range =>
                range.Intersects(spill.Range));
            if (!intersects)
            {
                continue;
            }
            var ownerSelected = Selection.Ranges.Any(range =>
                range.Contains(spill.Owner));
            if (!ownerSelected)
            {
                throw new InvalidOperationException(
                    "Cannot clear part of a dynamic-array spill. " +
                    "Select its owner cell to clear the complete formula.");
            }
        }

        var updates = ActiveWorksheet.EnumerateUsedCells()
            .Where(pair =>
                Selection.Ranges.Any(range => range.Contains(pair.Key)))
            .Where(pair =>
                !ActiveWorksheet.TryGetFormulaSpillOwner(
                    pair.Key,
                    out var owner) ||
                pair.Key == owner)
            .Select(pair => new KeyValuePair<CellAddress, CellData>(
                pair.Key,
                CellData.Empty))
            .ToArray();
        if (updates.Length == 0)
        {
            return false;
        }

        Execute(new SetCellsOperation(
            ActiveWorksheet,
            updates,
            "Clear contents"));
        return true;
    }

    public void Execute(ISpreadsheetEditOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Workbook.Worksheets.Contains(operation.Worksheet))
        {
            throw new ArgumentException(
                "Operation worksheet must belong to the session workbook.",
                nameof(operation));
        }

        History.Execute(operation);
        if (operation.AffectsCalculation)
        {
            Calculation.RecalculateAffected(
                Workbook,
                operation.Worksheet,
                operation.AffectedRange);
        }
    }

    public bool Undo()
    {
        if (!History.TryUndo(out var operation))
        {
            return false;
        }
        RecalculateAfterHistoryOperation(operation);
        return true;
    }

    public bool Redo()
    {
        if (!History.TryRedo(out var operation))
        {
            return false;
        }
        RecalculateAfterHistoryOperation(operation);
        return true;
    }

    public WorkbookCalculationResult Recalculate() =>
        Calculation.Recalculate(Workbook);

    private void OnAnalyticsChanged(
        object? sender,
        SpreadsheetAnalyticsChangedEventArgs e)
    {
        if (e.ChangeKind is not
                (SpreadsheetAnalyticsChangeKind.ChartRemoved or
                 SpreadsheetAnalyticsChangeKind.PivotRemoved) ||
            AnalyticsInteraction.SelectedItem is not { } selected ||
            selected.Id != e.ItemId)
        {
            return;
        }

        AnalyticsInteraction.ClearSelection();
    }

    private void RecalculateAfterHistoryOperation(
        IUndoableOperation? operation)
    {
        if (operation is ISpreadsheetEditOperation
            {
                AffectsCalculation: false,
            })
        {
            return;
        }
        if (operation is ISpreadsheetEditOperation editOperation &&
            operation is IIncrementalCalculationOperation)
        {
            Calculation.RecalculateAffected(
                Workbook,
                editOperation.Worksheet,
                editOperation.AffectedRange);
            return;
        }
        Calculation.Recalculate(Workbook);
    }

    private void EnsureSpillCellEditable(CellAddress address)
    {
        if (ActiveWorksheet.TryGetFormulaSpillOwner(
                address,
                out var owner) &&
            owner != address)
        {
            throw new InvalidOperationException(
                $"Cell {address.ToA1()} belongs to the dynamic-array spill " +
                $"owned by {owner.ToA1()} and cannot be edited directly.");
        }
    }
}
