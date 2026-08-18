using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetStructureController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetStructureController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void InsertRows(int rowIndex, int count = 1) =>
        Execute(new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            rowIndex,
            count));

    public void DeleteRows(int rowIndex, int count = 1) =>
        Execute(new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            rowIndex,
            count));

    public void InsertColumns(int columnIndex, int count = 1) =>
        Execute(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Insert,
            columnIndex,
            count));

    public void DeleteColumns(int columnIndex, int count = 1) =>
        Execute(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            columnIndex,
            count));

    public void InsertAtActiveCell(int count = 1)
    {
        var active = _session.Selection.ActiveCell;
        if (_session.Selection.Ranges.Count == 1 && IsWholeColumnRange(_session.Selection.Ranges[0]))
        {
            InsertColumns(active.ColumnIndex, count);
            return;
        }
        InsertRows(active.RowIndex, count);
    }

    public void DeleteAtActiveCell(int count = 1)
    {
        var active = _session.Selection.ActiveCell;
        if (_session.Selection.Ranges.Count == 1 && IsWholeColumnRange(_session.Selection.Ranges[0]))
        {
            DeleteColumns(active.ColumnIndex, count);
            return;
        }
        DeleteRows(active.RowIndex, count);
    }

    private void Execute(WorksheetStructuralChange change)
    {
        var operation = new StructuralWorksheetOperation(_session, _session.ActiveWorksheet, change);
        _session.History.Execute(operation);
        _session.Calculation.Recalculate(_session.Workbook);
    }

    private static bool IsWholeColumnRange(CellRange range) =>
        range.Top == 0 && range.Bottom == SpreadsheetLimits.MaxRows - 1;

    private sealed class StructuralWorksheetOperation : ISpreadsheetEditOperation
    {
        private readonly SpreadsheetSession _session;
        private readonly WorksheetStructuralChange _change;
        private WorksheetStructuralState? _worksheetBefore;
        private Dictionary<Worksheet, KeyValuePair<CellAddress, CellData>[]>? _externalFormulaCellsBefore;
        private SelectionSnapshot? _selectionBefore;
        private int _frozenRowsBefore;
        private int _frozenColumnsBefore;
        private SpreadsheetSplitViewState _splitStateBefore;

        public StructuralWorksheetOperation(
            SpreadsheetSession session,
            Worksheet worksheet,
            WorksheetStructuralChange change)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            _change = change;
            AffectedRange = CreateAffectedRange(change);
        }

        public string Description => _change switch
        {
            { Axis: WorksheetAxis.Row, Kind: WorksheetStructuralChangeKind.Insert } => "Insert rows",
            { Axis: WorksheetAxis.Row, Kind: WorksheetStructuralChangeKind.Delete } => "Delete rows",
            { Axis: WorksheetAxis.Column, Kind: WorksheetStructuralChangeKind.Insert } => "Insert columns",
            _ => "Delete columns",
        };

        public Worksheet Worksheet { get; }
        public CellRange AffectedRange { get; }

        public void Execute()
        {
            CaptureBeforeStateIfNeeded();
            var worksheetChanged = false;
            try
            {
                Worksheet.ApplyStructuralChange(_change);
                worksheetChanged = true;
                RewriteWorkbookFormulas();
                RestoreMappedSelection();
                ApplyMappedFreezeState();
                ApplyMappedSplitState();
            }
            catch
            {
                if (worksheetChanged)
                {
                    RestoreBeforeState();
                }
                throw;
            }
        }

        public void Undo() => RestoreBeforeState();

        private void CaptureBeforeStateIfNeeded()
        {
            if (_worksheetBefore is not null)
            {
                return;
            }

            _worksheetBefore = Worksheet.CaptureStructuralState();
            _externalFormulaCellsBefore = new Dictionary<Worksheet, KeyValuePair<CellAddress, CellData>[]>();
            foreach (var worksheet in _session.Workbook.Worksheets)
            {
                if (ReferenceEquals(worksheet, Worksheet))
                {
                    continue;
                }
                var formulas = worksheet.EnumerateUsedCells()
                    .Where(static pair => pair.Value.Formula is not null)
                    .ToArray();
                if (formulas.Length > 0)
                {
                    _externalFormulaCellsBefore.Add(worksheet, formulas);
                }
            }
            _selectionBefore = _session.Selection.Capture();
            _frozenRowsBefore = _session.View.FrozenRows;
            _frozenColumnsBefore = _session.View.FrozenColumns;
            _splitStateBefore = _session.View.SplitState;
        }

        private void RestoreBeforeState()
        {
            if (_worksheetBefore is null ||
                _externalFormulaCellsBefore is null ||
                _selectionBefore is null)
            {
                throw new InvalidOperationException("The structural operation has not been executed yet.");
            }

            Worksheet.RestoreStructuralState(_worksheetBefore, _change);
            foreach (var (worksheet, formulas) in _externalFormulaCellsBefore)
            {
                worksheet.SetCells(formulas);
            }
            _session.View.SetFrozenPanes(_frozenRowsBefore, _frozenColumnsBefore);
            _session.View.SetSplitState(
                Worksheet,
                _splitStateBefore,
                SpreadsheetSplitViewChangeKind.State,
                this);
            _session.Selection.Restore(_selectionBefore);
        }

        private void RewriteWorkbookFormulas()
        {
            foreach (var worksheet in _session.Workbook.Worksheets)
            {
                var updates = new List<KeyValuePair<CellAddress, CellData>>();
                foreach (var (address, cell) in worksheet.EnumerateUsedCells())
                {
                    if (cell.Formula is not { } formula)
                    {
                        continue;
                    }
                    var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
                        formula,
                        worksheet.Name,
                        Worksheet.Name,
                        _change);
                    if (string.Equals(rewritten, formula, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    updates.Add(new KeyValuePair<CellAddress, CellData>(
                        address,
                        new CellData(cell.Value, rewritten, cell.StyleId)));
                }
                if (updates.Count > 0)
                {
                    worksheet.SetCells(updates);
                }
            }
        }

        private void RestoreMappedSelection()
        {
            var before = _selectionBefore
                ?? throw new InvalidOperationException("Selection state was not captured.");
            var active = MapSelectionAddress(before.ActiveCell);
            var anchor = MapSelectionAddress(before.AnchorCell);
            var ranges = new List<CellRange>(before.Ranges.Count);
            foreach (var range in before.Ranges)
            {
                if (TryMapSelectionRange(range, out var mappedRange))
                {
                    ranges.Add(mappedRange);
                }
            }
            if (ranges.Count == 0)
            {
                ranges.Add(new CellRange(active, active));
            }
            _session.Selection.Restore(new SelectionSnapshot(active, anchor, ranges, before.Version));
        }

        private void ApplyMappedFreezeState()
        {
            var rows = _change.Axis == WorksheetAxis.Row
                ? _change.MapBoundary(_frozenRowsBefore)
                : _frozenRowsBefore;
            var columns = _change.Axis == WorksheetAxis.Column
                ? _change.MapBoundary(_frozenColumnsBefore)
                : _frozenColumnsBefore;
            _session.View.SetFrozenPanes(rows, columns);
        }

        private void ApplyMappedSplitState()
        {
            var mapped = _splitStateBefore;
            foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
            {
                var scroll = _splitStateBefore.GetPaneScroll(pane);
                var offsetX = _change.Axis == WorksheetAxis.Column
                    ? MapScrollOffset(scroll.OffsetX)
                    : scroll.OffsetX;
                var offsetY = _change.Axis == WorksheetAxis.Row
                    ? MapScrollOffset(scroll.OffsetY)
                    : scroll.OffsetY;
                mapped = mapped.WithPaneScroll(pane, offsetX, offsetY);
            }

            _session.View.SetSplitState(
                Worksheet,
                mapped,
                SpreadsheetSplitViewChangeKind.PaneScroll,
                this);
        }

        private double MapScrollOffset(double offset)
        {
            var before = _worksheetBefore
                ?? throw new InvalidOperationException("Worksheet state was not captured.");
            var overrides = _change.Axis == WorksheetAxis.Row
                ? before.RowHeights
                : before.ColumnWidths;
            var defaultSize = _change.Axis == WorksheetAxis.Row
                ? Worksheet.Dimensions.DefaultRowHeight
                : Worksheet.Dimensions.DefaultColumnWidth;
            var changeStart = GetAxisOffset(
                _change.Index,
                defaultSize,
                overrides);
            if (_change.Kind == WorksheetStructuralChangeKind.Insert)
            {
                return offset < changeStart
                    ? offset
                    : offset + (_change.Count * defaultSize);
            }

            var changeEnd = GetAxisOffset(
                _change.Index + _change.Count,
                defaultSize,
                overrides);
            if (offset < changeStart)
            {
                return offset;
            }
            if (offset >= changeEnd)
            {
                return offset - (changeEnd - changeStart);
            }
            return changeStart;
        }

        private static double GetAxisOffset(
            int index,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> overrides)
        {
            var offset = index * defaultSize;
            foreach (var (overrideIndex, size) in overrides)
            {
                if (overrideIndex >= index)
                {
                    continue;
                }
                offset += size - defaultSize;
            }
            return offset;
        }

        private CellAddress MapSelectionAddress(CellAddress source)
        {
            if (_change.TryMapAddress(source, out var mapped))
            {
                return mapped;
            }

            var replacementIndex = Math.Min(_change.Index, _change.AxisLength - 1);
            return _change.Axis == WorksheetAxis.Row
                ? new CellAddress(replacementIndex, source.ColumnIndex)
                : new CellAddress(source.RowIndex, replacementIndex);
        }

        private bool TryMapSelectionRange(CellRange source, out CellRange mapped)
        {
            var startsAtOrigin = _change.Axis == WorksheetAxis.Row
                ? source.Top == 0
                : source.Left == 0;
            var reachesAxisEnd = _change.Axis == WorksheetAxis.Row
                ? source.Bottom == _change.AxisLength - 1
                : source.Right == _change.AxisLength - 1;
            if (startsAtOrigin && reachesAxisEnd)
            {
                mapped = source;
                return true;
            }
            return _change.TryMapRange(source, out mapped);
        }

        private static CellRange CreateAffectedRange(WorksheetStructuralChange change) =>
            change.Axis == WorksheetAxis.Row
                ? new CellRange(
                    new CellAddress(change.Index, 0),
                    new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1))
                : new CellRange(
                    new CellAddress(0, change.Index),
                    new CellAddress(SpreadsheetLimits.MaxRows - 1, SpreadsheetLimits.MaxColumns - 1));
    }
}
