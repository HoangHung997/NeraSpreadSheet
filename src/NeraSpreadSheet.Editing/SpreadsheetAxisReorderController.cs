using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetAxisReorderController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetAxisReorderController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public bool MoveRows(
        int sourceIndex,
        int count,
        int destinationBoundary) =>
        Move(new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex,
            count,
            destinationBoundary));

    public bool MoveColumns(
        int sourceIndex,
        int count,
        int destinationBoundary) =>
        Move(new WorksheetAxisMove(
            WorksheetAxis.Column,
            sourceIndex,
            count,
            destinationBoundary));

    public bool Move(WorksheetAxisMove move)
    {
        if (move.IsNoOp)
        {
            return false;
        }

        var operation = new AxisMoveOperation(
            _session,
            _session.ActiveWorksheet,
            move);
        _session.History.Execute(operation);
        _session.Calculation.Recalculate(_session.Workbook);
        return true;
    }

    private sealed class AxisMoveOperation : ISpreadsheetEditOperation
    {
        private readonly SpreadsheetSession _session;
        private readonly WorksheetAxisMove _move;
        private WorksheetStructuralState? _worksheetBefore;
        private Dictionary<Worksheet, KeyValuePair<CellAddress, CellData>[]>?
            _externalFormulaCellsBefore;
        private Dictionary<Worksheet, KeyValuePair<CellAddress, CellData>[]>?
            _formulaUpdates;
        private SelectionSnapshot? _selectionBefore;
        private SelectionSnapshot? _selectionAfter;
        private int _frozenRowsBefore;
        private int _frozenColumnsBefore;
        private SpreadsheetSplitViewState _splitStateBefore;
        private SpreadsheetSplitViewState _splitStateAfter;

        public AxisMoveOperation(
            SpreadsheetSession session,
            Worksheet worksheet,
            WorksheetAxisMove move)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            _move = move;
            AffectedRange = CreateAffectedRange(move);
        }

        public string Description => _move.Axis == WorksheetAxis.Row
            ? "Reorder rows"
            : "Reorder columns";

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public void Execute()
        {
            PrepareIfNeeded();
            var worksheetChanged = false;
            try
            {
                Worksheet.ApplyAxisMove(_move);
                worksheetChanged = true;
                ApplyFormulaUpdates();
                _session.View.SetFrozenPanes(
                    _frozenRowsBefore,
                    _frozenColumnsBefore);
                _session.View.SetSplitState(
                    Worksheet,
                    _splitStateAfter,
                    SpreadsheetSplitViewChangeKind.PaneScroll,
                    this);
                _session.Selection.Restore(
                    _selectionAfter ??
                    throw new InvalidOperationException(
                        "Mapped selection state was not prepared."));
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

        private void PrepareIfNeeded()
        {
            if (_worksheetBefore is not null)
            {
                return;
            }

            _worksheetBefore = Worksheet.CaptureStructuralState();
            _selectionBefore = _session.Selection.Capture();
            _frozenRowsBefore = _session.View.FrozenRows;
            _frozenColumnsBefore = _session.View.FrozenColumns;
            _splitStateBefore = _session.View.SplitState;
            _externalFormulaCellsBefore = CaptureExternalFormulaCells();

            ValidateMappedMergedRanges();
            _formulaUpdates = CreateFormulaUpdates();
            _selectionAfter = MapSelection(_selectionBefore);
            _splitStateAfter = MapSplitState(
                _splitStateBefore,
                _worksheetBefore);
        }

        private Dictionary<
            Worksheet,
            KeyValuePair<CellAddress, CellData>[]>
            CaptureExternalFormulaCells()
        {
            var result = new Dictionary<
                Worksheet,
                KeyValuePair<CellAddress, CellData>[]>();
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
                    result.Add(worksheet, formulas);
                }
            }
            return result;
        }

        private Dictionary<
            Worksheet,
            KeyValuePair<CellAddress, CellData>[]>
            CreateFormulaUpdates()
        {
            var result = new Dictionary<
                Worksheet,
                KeyValuePair<CellAddress, CellData>[]>();
            foreach (var worksheet in _session.Workbook.Worksheets)
            {
                var updates = new List<KeyValuePair<CellAddress, CellData>>();
                foreach (var (address, cell) in worksheet.EnumerateUsedCells())
                {
                    if (cell.Formula is not { } formula)
                    {
                        continue;
                    }

                    var targetAddress = ReferenceEquals(worksheet, Worksheet)
                        ? _move.MapAddress(address)
                        : address;
                    var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
                        formula,
                        worksheet.Name,
                        Worksheet.Name,
                        _move);
                    updates.Add(new KeyValuePair<CellAddress, CellData>(
                        targetAddress,
                        new CellData(cell.Value, rewritten, cell.StyleId)));
                }

                if (updates.Count > 0)
                {
                    result.Add(worksheet, [.. updates]);
                }
            }
            return result;
        }

        private void ApplyFormulaUpdates()
        {
            var updates = _formulaUpdates ??
                throw new InvalidOperationException(
                    "Formula updates were not prepared.");
            foreach (var (worksheet, cells) in updates)
            {
                worksheet.SetCells(cells);
            }
        }

        private void ValidateMappedMergedRanges()
        {
            var mapped = Worksheet.MergedCells.CreateAxisMoveRanges(_move);
            foreach (var range in mapped)
            {
                if (_frozenRowsBefore > 0 &&
                    range.Top < _frozenRowsBefore &&
                    range.Bottom >= _frozenRowsBefore)
                {
                    throw new InvalidOperationException(
                        "Cannot reorder because a merged range would cross the frozen-row boundary.");
                }
                if (_frozenColumnsBefore > 0 &&
                    range.Left < _frozenColumnsBefore &&
                    range.Right >= _frozenColumnsBefore)
                {
                    throw new InvalidOperationException(
                        "Cannot reorder because a merged range would cross the frozen-column boundary.");
                }
            }
        }

        private SelectionSnapshot MapSelection(SelectionSnapshot source)
        {
            var ranges = new List<CellRange>(source.Ranges.Count + 2);
            foreach (var range in source.Ranges)
            {
                var start = _move.Axis == WorksheetAxis.Row
                    ? range.Top
                    : range.Left;
                var end = _move.Axis == WorksheetAxis.Row
                    ? range.Bottom
                    : range.Right;
                foreach (var interval in _move.MapInterval(start, end))
                {
                    var mapped = _move.Axis == WorksheetAxis.Row
                        ? new CellRange(
                            new CellAddress(interval.Start, range.Left),
                            new CellAddress(interval.End, range.Right))
                        : new CellRange(
                            new CellAddress(range.Top, interval.Start),
                            new CellAddress(range.Bottom, interval.End));
                    if (!ranges.Contains(mapped))
                    {
                        ranges.Add(mapped);
                    }
                }
            }

            if (ranges.Count == 0)
            {
                var active = _move.MapAddress(source.ActiveCell);
                ranges.Add(new CellRange(active, active));
            }

            return new SelectionSnapshot(
                _move.MapAddress(source.ActiveCell),
                _move.MapAddress(source.AnchorCell),
                ranges,
                source.Version);
        }

        private SpreadsheetSplitViewState MapSplitState(
            SpreadsheetSplitViewState source,
            WorksheetStructuralState worksheetBefore)
        {
            var beforeOverrides = (_move.Axis == WorksheetAxis.Row
                    ? worksheetBefore.RowHeights
                    : worksheetBefore.ColumnWidths)
                .OrderBy(static pair => pair.Key)
                .ToArray();
            var afterOverrides = beforeOverrides
                .Select(pair => new KeyValuePair<int, double>(
                    _move.MapIndex(pair.Key),
                    pair.Value))
                .OrderBy(static pair => pair.Key)
                .ToArray();
            var beforeHidden = _move.Axis == WorksheetAxis.Row
                ? worksheetBefore.HiddenRows
                : worksheetBefore.HiddenColumns;
            var afterHidden = beforeHidden
                .SelectMany(range => _move.MapInterval(range.Start, range.End))
                .OrderBy(static range => range.Start)
                .ToArray();
            var defaultSize = _move.Axis == WorksheetAxis.Row
                ? Worksheet.Dimensions.DefaultRowHeight
                : Worksheet.Dimensions.DefaultColumnWidth;

            var mapped = source;
            foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
            {
                var scroll = source.GetPaneScroll(pane);
                var axisOffset = _move.Axis == WorksheetAxis.Row
                    ? scroll.OffsetY
                    : scroll.OffsetX;
                var mappedOffset = MapAxisOffset(
                    axisOffset,
                    defaultSize,
                    beforeOverrides,
                    afterOverrides,
                    beforeHidden,
                    afterHidden);
                mapped = _move.Axis == WorksheetAxis.Row
                    ? mapped.WithPaneScroll(
                        pane,
                        scroll.OffsetX,
                        mappedOffset)
                    : mapped.WithPaneScroll(
                        pane,
                        mappedOffset,
                        scroll.OffsetY);
            }
            return mapped;
        }

        private double MapAxisOffset(
            double offset,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> beforeOverrides,
            IReadOnlyList<KeyValuePair<int, double>> afterOverrides,
            IReadOnlyList<WorksheetAxisInterval> beforeHidden,
            IReadOnlyList<WorksheetAxisInterval> afterHidden)
        {
            if (offset <= 0d)
            {
                return 0d;
            }

            var sourceIndex = FindAxisIndex(
                offset,
                defaultSize,
                beforeOverrides,
                beforeHidden);
            var sourceStart = GetAxisOffset(
                sourceIndex,
                defaultSize,
                beforeOverrides,
                beforeHidden);
            var sourceSize = GetAxisSize(
                sourceIndex,
                defaultSize,
                beforeOverrides,
                beforeHidden);
            var localOffset = Math.Clamp(
                offset - sourceStart,
                0d,
                sourceSize);
            var targetIndex = _move.MapIndex(sourceIndex);
            return GetAxisOffset(
                targetIndex,
                defaultSize,
                afterOverrides,
                afterHidden) + localOffset;
        }

        private int FindAxisIndex(
            double offset,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> overrides,
            IReadOnlyList<WorksheetAxisInterval> hiddenRanges)
        {
            var low = 0;
            var high = _move.AxisLength - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var start = GetAxisOffset(
                    middle,
                    defaultSize,
                    overrides,
                    hiddenRanges);
                if (start <= offset)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            var candidate = Math.Clamp(high, 0, _move.AxisLength - 1);
            foreach (var range in hiddenRanges)
            {
                if (candidate < range.Start)
                {
                    break;
                }
                if (candidate <= range.End)
                {
                    return range.End < _move.AxisLength - 1
                        ? range.End + 1
                        : Math.Max(0, range.Start - 1);
                }
            }
            return candidate;
        }

        private static double GetAxisOffset(
            int index,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> overrides,
            IReadOnlyList<WorksheetAxisInterval> hiddenRanges)
        {
            var offset = GetRawAxisOffset(index, defaultSize, overrides);
            foreach (var range in hiddenRanges)
            {
                if (range.Start >= index)
                {
                    break;
                }
                var endExclusive = Math.Min(index, checked(range.End + 1));
                offset -= GetRawAxisOffset(endExclusive, defaultSize, overrides) -
                          GetRawAxisOffset(range.Start, defaultSize, overrides);
            }
            return offset;
        }

        private static double GetRawAxisOffset(
            int index,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> overrides)
        {
            var offset = index * defaultSize;
            foreach (var (overrideIndex, size) in overrides)
            {
                if (overrideIndex >= index)
                {
                    break;
                }
                offset += size - defaultSize;
            }
            return offset;
        }

        private static double GetAxisSize(
            int index,
            double defaultSize,
            IReadOnlyList<KeyValuePair<int, double>> overrides,
            IReadOnlyList<WorksheetAxisInterval> hiddenRanges)
        {
            if (hiddenRanges.Any(range =>
                    index >= range.Start && index <= range.End))
            {
                return 0d;
            }
            foreach (var (overrideIndex, size) in overrides)
            {
                if (overrideIndex == index)
                {
                    return size;
                }
                if (overrideIndex > index)
                {
                    break;
                }
            }
            return defaultSize;
        }

        private void RestoreBeforeState()
        {
            var worksheetBefore = _worksheetBefore ??
                throw new InvalidOperationException(
                    "The reorder operation has not been prepared.");
            var externalBefore = _externalFormulaCellsBefore ??
                throw new InvalidOperationException(
                    "External formulas were not captured.");
            var selectionBefore = _selectionBefore ??
                throw new InvalidOperationException(
                    "Selection state was not captured.");

            Worksheet.RestoreAxisMoveState(worksheetBefore, _move);
            foreach (var (worksheet, formulas) in externalBefore)
            {
                worksheet.SetCells(formulas);
            }
            _session.View.SetFrozenPanes(
                _frozenRowsBefore,
                _frozenColumnsBefore);
            _session.View.SetSplitState(
                Worksheet,
                _splitStateBefore,
                SpreadsheetSplitViewChangeKind.State,
                this);
            _session.Selection.Restore(selectionBefore);
        }

        private static CellRange CreateAffectedRange(WorksheetAxisMove move) =>
            move.Axis == WorksheetAxis.Row
                ? new CellRange(
                    new CellAddress(move.AffectedStartIndex, 0),
                    new CellAddress(
                        move.AffectedEndIndex,
                        SpreadsheetLimits.MaxColumns - 1))
                : new CellRange(
                    new CellAddress(0, move.AffectedStartIndex),
                    new CellAddress(
                        SpreadsheetLimits.MaxRows - 1,
                        move.AffectedEndIndex));
    }
}
