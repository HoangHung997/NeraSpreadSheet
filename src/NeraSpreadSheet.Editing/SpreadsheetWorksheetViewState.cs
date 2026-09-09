using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

/// <summary>One session's worksheet view; scroll offsets remain in the existing split-state model, even when unsplit.</summary>
public sealed class SpreadsheetWorksheetViewState
{
    public SpreadsheetWorksheetViewState(SelectionSnapshot selection, double zoom = 1d,
        SpreadsheetSplitViewState splitState = default, int frozenRows = 0, int frozenColumns = 0)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.Ranges);
        if (selection.Ranges.Count == 0) throw new ArgumentException("A view requires at least one selected range.", nameof(selection));
        if (!selection.Ranges.Any(range => range.Contains(selection.ActiveCell)))
            throw new ArgumentException("The active cell must belong to a selected range.", nameof(selection));
        if (!double.IsFinite(zoom) || zoom < 0.1d || zoom > 4d) throw new ArgumentOutOfRangeException(nameof(zoom));
        if (frozenRows < 0 || frozenRows >= SpreadsheetLimits.MaxRows) throw new ArgumentOutOfRangeException(nameof(frozenRows));
        if (frozenColumns < 0 || frozenColumns >= SpreadsheetLimits.MaxColumns) throw new ArgumentOutOfRangeException(nameof(frozenColumns));
        Selection = new SelectionSnapshot(selection.ActiveCell, selection.AnchorCell,
            Array.AsReadOnly(selection.Ranges.ToArray()), selection.Version);
        Zoom = zoom;
        SplitState = splitState;
        FrozenRows = frozenRows;
        FrozenColumns = frozenColumns;
    }

    public SelectionSnapshot Selection { get; }
    public double Zoom { get; }
    public SpreadsheetSplitViewState SplitState { get; }
    public int FrozenRows { get; }
    public int FrozenColumns { get; }
    public double OffsetX => SplitState.TopLeftScroll.OffsetX;
    public double OffsetY => SplitState.TopLeftScroll.OffsetY;

    public static SpreadsheetWorksheetViewState Default { get; } = new(
        new SelectionSnapshot(default, default, Array.AsReadOnly(new[] { new CellRange(default, default) }), 0));
}

public sealed class SpreadsheetWorksheetViewChangedEventArgs : EventArgs
{
    public SpreadsheetWorksheetViewChangedEventArgs(Worksheet worksheet, SpreadsheetWorksheetViewState state, object? source)
    {
        Worksheet = worksheet;
        State = state;
        Source = source;
    }
    public Worksheet Worksheet { get; }
    public SpreadsheetWorksheetViewState State { get; }
    public object? Source { get; }
}

public sealed partial class SpreadsheetViewController
{
    private readonly Dictionary<Worksheet, SpreadsheetWorksheetViewState> _worksheetViews = [];

    /// <summary>True throughout worksheet activation notifications; feedback writes to view setters are ignored.</summary>
    public bool IsRestoringWorksheetView { get; private set; }
    public event EventHandler<SpreadsheetWorksheetViewChangedEventArgs>? WorksheetViewChanged;

    public SpreadsheetWorksheetViewState WorksheetState => GetWorksheetState(_session.ActiveWorksheet);

    public SpreadsheetWorksheetViewState GetWorksheetState(Worksheet worksheet)
    {
        ValidateWorksheet(worksheet);
        var saved = _worksheetViews.GetValueOrDefault(worksheet, SpreadsheetWorksheetViewState.Default);
        var selection = ReferenceEquals(worksheet, _session.ActiveWorksheet)
            ? _session.Selection.Capture() : saved.Selection;
        var freeze = GetFreezeState(worksheet);
        return new SpreadsheetWorksheetViewState(selection, saved.Zoom, GetSplitState(worksheet), freeze.Rows, freeze.Columns);
    }

    /// <summary>Restores all supported state for one worksheet without writing workbook cells or history.</summary>
    public bool SetWorksheetState(Worksheet worksheet, SpreadsheetWorksheetViewState state, object? source = null)
    {
        ValidateWorksheet(worksheet);
        ArgumentNullException.ThrowIfNull(state);
        if (IsRestoringWorksheetView) return false;
        ValidateMergedBoundaries(worksheet, state.FrozenRows, state.FrozenColumns);
        var current = GetWorksheetState(worksheet);
        var normalized = NormalizeState(worksheet, state);
        if (SameState(current, normalized)) return false;
        _worksheetViews[worksheet] = normalized;
        if (normalized.SplitState == default) _splitStates.Remove(worksheet);
        else _splitStates[worksheet] = normalized.SplitState;
        var freeze = new FreezeState(normalized.FrozenRows, normalized.FrozenColumns);
        if (freeze == default) _freezeStates.Remove(worksheet); else _freezeStates[worksheet] = freeze;
        IsRestoringWorksheetView = true;
        try
        {
            if (ReferenceEquals(worksheet, _session.ActiveWorksheet))
                _session.Selection.Restore(normalized.Selection);
            PublishFreeze(worksheet, freeze);
            PublishSplit(worksheet, normalized.SplitState, SpreadsheetSplitViewChangeKind.State, source);
            WorksheetViewChanged?.Invoke(this, new(worksheet, GetWorksheetState(worksheet), source));
        }
        finally { IsRestoringWorksheetView = false; }
        return true;
    }

    /// <summary>Updates the standalone/top-left-pane offset and zoom. The host supplies document pixels, not row indices.</summary>
    public bool SetWorksheetViewport(Worksheet worksheet, double offsetX, double offsetY, double zoom, object? source = null)
    {
        ValidateWorksheet(worksheet);
        // Constructing these values validates non-finite and out-of-range input even during restore.
        var state = GetWorksheetState(worksheet);
        var next = new SpreadsheetWorksheetViewState(state.Selection, zoom,
            state.SplitState.WithPaneScroll(SpreadsheetSplitViewPane.TopLeft, offsetX, offsetY),
            state.FrozenRows, state.FrozenColumns);
        if (IsRestoringWorksheetView ||
            state.Zoom == next.Zoom && state.SplitState == next.SplitState)
            return false;

        // Viewport input must not normalize or restore the selection, republish
        // unchanged freeze boundaries, or touch workbook/history. In particular,
        // scrolling while an active cell is hidden must not move that active cell.
        _worksheetViews[worksheet] = next;
        var scrollChanged = state.SplitState != next.SplitState;
        if (next.SplitState == default) _splitStates.Remove(worksheet);
        else _splitStates[worksheet] = next.SplitState;
        IsRestoringWorksheetView = true;
        try
        {
            if (scrollChanged)
                PublishSplit(worksheet, next.SplitState, SpreadsheetSplitViewChangeKind.PaneScroll, source);
            else
                Version++;
            WorksheetViewChanged?.Invoke(this, new(worksheet, GetWorksheetState(worksheet), source));
        }
        finally { IsRestoringWorksheetView = false; }
        return true;
    }

    /// <summary>Clamps only this worksheet using actual viewport maximum offsets supplied by its host.</summary>
    public bool ClampWorksheetViewport(Worksheet worksheet, double maximumX, double maximumY, object? source = null)
    {
        if (!double.IsFinite(maximumX) || maximumX < 0) throw new ArgumentOutOfRangeException(nameof(maximumX));
        if (!double.IsFinite(maximumY) || maximumY < 0) throw new ArgumentOutOfRangeException(nameof(maximumY));
        var state = GetWorksheetState(worksheet);
        return SetWorksheetViewport(worksheet, Math.Min(state.OffsetX, maximumX), Math.Min(state.OffsetY, maximumY), state.Zoom, source);
    }

    internal SpreadsheetWorksheetViewState BeginWorksheetActivation(Worksheet worksheet)
    {
        if (IsRestoringWorksheetView) throw new InvalidOperationException("Worksheet activation cannot be re-entered from a restore callback.");
        PruneRemovedWorksheetStates();
        if (_session.Workbook.Worksheets.Contains(_session.ActiveWorksheet))
            _worksheetViews[_session.ActiveWorksheet] = GetWorksheetState(_session.ActiveWorksheet);
        var state = NormalizeState(worksheet, GetWorksheetState(worksheet));
        _worksheetViews[worksheet] = state;
        IsRestoringWorksheetView = true;
        return state;
    }

    internal void AbortWorksheetActivation() => IsRestoringWorksheetView = false;

    internal void CompleteWorksheetActivation()
    {
        try
        {
            var worksheet = _session.ActiveWorksheet;
            WorksheetViewChanged?.Invoke(this, new(worksheet, GetWorksheetState(worksheet), null));
        }
        finally { IsRestoringWorksheetView = false; }
    }

    /// <summary>Releases removed worksheet references; worksheet names never serve as state keys.</summary>
    public void PruneRemovedWorksheetStates()
    {
        foreach (var worksheet in _worksheetViews.Keys.Where(sheet => !_session.Workbook.Worksheets.Contains(sheet)).ToArray())
            _worksheetViews.Remove(worksheet);
        foreach (var worksheet in _freezeStates.Keys.Where(sheet => !_session.Workbook.Worksheets.Contains(sheet)).ToArray())
            _freezeStates.Remove(worksheet);
        foreach (var worksheet in _splitStates.Keys.Where(sheet => !_session.Workbook.Worksheets.Contains(sheet)).ToArray())
            _splitStates.Remove(worksheet);
    }

    private static SpreadsheetWorksheetViewState NormalizeState(Worksheet worksheet, SpreadsheetWorksheetViewState state)
    {
        CellAddress Normalize(CellAddress address)
        {
            var row = address.RowIndex;
            var column = address.ColumnIndex;
            if (worksheet.Dimensions.TryGetHiddenRowRange(row, out var rows))
                row = rows.End + 1 < SpreadsheetLimits.MaxRows ? rows.End + 1 : Math.Max(0, rows.Start - 1);
            if (worksheet.Dimensions.TryGetHiddenColumnRange(column, out var columns))
                column = columns.End + 1 < SpreadsheetLimits.MaxColumns ? columns.End + 1 : Math.Max(0, columns.Start - 1);
            return worksheet.ResolveMergedAnchor(new CellAddress(row, column));
        }
        var active = Normalize(state.Selection.ActiveCell);
        var anchor = Normalize(state.Selection.AnchorCell);
        var ranges = state.Selection.Ranges.ToArray();
        if (active != state.Selection.ActiveCell && !ranges.Any(range => range.Contains(active)))
        {
            ranges = ranges.Length == 1 && ranges[0].TopLeft == ranges[0].BottomRight
                ? [new CellRange(active, active)] : ranges.Append(new CellRange(active, active)).ToArray();
        }
        return new SpreadsheetWorksheetViewState(new SelectionSnapshot(active, anchor, ranges, state.Selection.Version),
            state.Zoom, state.SplitState, state.FrozenRows, state.FrozenColumns);
    }

    private static bool SameState(SpreadsheetWorksheetViewState left, SpreadsheetWorksheetViewState right) =>
        left.Zoom == right.Zoom && left.SplitState == right.SplitState &&
        left.FrozenRows == right.FrozenRows && left.FrozenColumns == right.FrozenColumns &&
        left.Selection.ActiveCell == right.Selection.ActiveCell && left.Selection.AnchorCell == right.Selection.AnchorCell &&
        left.Selection.Ranges.SequenceEqual(right.Selection.Ranges);
}
