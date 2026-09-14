using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Coordinates cached worksheet view state across multiple sessions that share
/// the same workbook. Only inactive peer worksheets are remapped here; active
/// host/editor arbitration remains owned by the peer session/host.
/// </summary>
internal static class SpreadsheetCrossSessionStructuralViewCoordinator
{
    private static readonly ConditionalWeakTable<Workbook, SessionBucket> Buckets = new();

    internal static void Register(SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var bucket = Buckets.GetValue(session.Workbook, static _ => new SessionBucket());
        lock (bucket.Gate)
        {
            Prune(bucket);
            if (!bucket.Sessions.Any(reference =>
                    reference.TryGetTarget(out var existing) &&
                    ReferenceEquals(existing, session)))
            {
                bucket.Sessions.Add(new WeakReference<SpreadsheetSession>(session));
            }
        }
    }

    internal static PeerViewSnapshot[] CaptureInactivePeers(
        SpreadsheetSession origin,
        Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!Buckets.TryGetValue(origin.Workbook, out var bucket))
        {
            return [];
        }

        lock (bucket.Gate)
        {
            Prune(bucket);
            var snapshots = new List<PeerViewSnapshot>();
            foreach (var reference in bucket.Sessions)
            {
                if (!reference.TryGetTarget(out var peer) ||
                    ReferenceEquals(peer, origin) ||
                    ReferenceEquals(peer.ActiveWorksheet, worksheet) ||
                    !peer.Workbook.Worksheets.Contains(worksheet))
                {
                    continue;
                }

                snapshots.Add(new PeerViewSnapshot(
                    peer,
                    worksheet,
                    peer.View.GetWorksheetState(worksheet)));
            }
            return snapshots.ToArray();
        }
    }

    /// <summary>
    /// Maps captured inactive peer states after a committed structural change.
    /// A peer that changed its cached view between capture and apply is skipped.
    /// If any applied peer throws, already-applied peers are restored in reverse
    /// order and the original exception remains the exception that escapes.
    /// </summary>
    internal static void ApplyMapped(
        IReadOnlyList<PeerViewSnapshot> snapshots,
        WorksheetStructuralChange change,
        WorksheetStructuralState worksheetBefore)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(worksheetBefore);
        var applied = new List<PeerViewSnapshot>(snapshots.Count);
        try
        {
            foreach (var snapshot in snapshots)
            {
                if (!TryGetApplicablePeer(snapshot, out var peer) ||
                    !SameState(
                        peer.View.GetWorksheetState(snapshot.Worksheet),
                        snapshot.BeforeState))
                {
                    continue;
                }

                var mapped = MapState(
                    snapshot.Worksheet,
                    snapshot.BeforeState,
                    change,
                    worksheetBefore);
                peer.View.SetWorksheetState(
                    snapshot.Worksheet,
                    mapped,
                    source: snapshot);
                snapshot.AppliedState = mapped;
                applied.Add(snapshot);
            }
        }
        catch (Exception original)
        {
            var recoveryErrors = new List<Exception>();
            for (var index = applied.Count - 1; index >= 0; index--)
            {
                try
                {
                    RestoreOneIfUnchanged(applied[index]);
                }
                catch (Exception recovery)
                {
                    recoveryErrors.Add(recovery);
                }
            }
            if (recoveryErrors.Count > 0)
            {
                original.Data["NeraSpreadSheet.CrossSessionStructuralRecoveryErrors"] =
                    recoveryErrors.ToArray();
            }
            ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
    }

    /// <summary>
    /// Restores the pre-operation state only when the peer is still inactive and
    /// still has the exact state applied by this operation. User/host view changes
    /// made after Execute therefore survive a later Undo.
    /// </summary>
    internal static void RestoreIfUnchanged(
        IReadOnlyList<PeerViewSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var restored = new List<(PeerViewSnapshot Snapshot, SpreadsheetWorksheetViewState Applied)>();
        try
        {
            for (var index = snapshots.Count - 1; index >= 0; index--)
            {
                var snapshot = snapshots[index];
                if (snapshot.AppliedState is not { } applied ||
                    !TryGetApplicablePeer(snapshot, out var peer) ||
                    !SameState(
                        peer.View.GetWorksheetState(snapshot.Worksheet),
                        applied))
                {
                    continue;
                }

                peer.View.SetWorksheetState(
                    snapshot.Worksheet,
                    snapshot.BeforeState,
                    source: snapshot);
                snapshot.AppliedState = null;
                restored.Add((snapshot, applied));
            }
        }
        catch (Exception original)
        {
            var recoveryErrors = new List<Exception>();
            for (var index = restored.Count - 1; index >= 0; index--)
            {
                var (snapshot, applied) = restored[index];
                try
                {
                    if (TryGetApplicablePeer(snapshot, out var peer) &&
                        SameState(
                            peer.View.GetWorksheetState(snapshot.Worksheet),
                            snapshot.BeforeState))
                    {
                        peer.View.SetWorksheetState(
                            snapshot.Worksheet,
                            applied,
                            source: snapshot);
                        snapshot.AppliedState = applied;
                    }
                }
                catch (Exception recovery)
                {
                    recoveryErrors.Add(recovery);
                }
            }
            if (recoveryErrors.Count > 0)
            {
                original.Data["NeraSpreadSheet.CrossSessionStructuralRecoveryErrors"] =
                    recoveryErrors.ToArray();
            }
            ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
    }

    private static void RestoreOneIfUnchanged(PeerViewSnapshot snapshot)
    {
        if (snapshot.AppliedState is not { } applied ||
            !TryGetApplicablePeer(snapshot, out var peer) ||
            !SameState(
                peer.View.GetWorksheetState(snapshot.Worksheet),
                applied))
        {
            return;
        }

        peer.View.SetWorksheetState(
            snapshot.Worksheet,
            snapshot.BeforeState,
            source: snapshot);
        snapshot.AppliedState = null;
    }

    private static bool TryGetApplicablePeer(
        PeerViewSnapshot snapshot,
        out SpreadsheetSession peer)
    {
        if (!snapshot.Session.TryGetTarget(out peer!) ||
            !peer.Workbook.Worksheets.Contains(snapshot.Worksheet) ||
            ReferenceEquals(peer.ActiveWorksheet, snapshot.Worksheet))
        {
            peer = null!;
            return false;
        }
        return true;
    }

    private static SpreadsheetWorksheetViewState MapState(
        Worksheet worksheet,
        SpreadsheetWorksheetViewState state,
        WorksheetStructuralChange change,
        WorksheetStructuralState worksheetBefore)
    {
        var active = MapAddress(state.Selection.ActiveCell, change);
        var anchor = MapAddress(state.Selection.AnchorCell, change);
        var ranges = new List<CellRange>(state.Selection.Ranges.Count);
        foreach (var range in state.Selection.Ranges)
        {
            if (TryMapRange(range, change, out var mapped))
            {
                ranges.Add(mapped);
            }
        }
        if (ranges.Count == 0)
        {
            ranges.Add(new CellRange(active, active));
        }
        else if (!ranges.Any(range => range.Contains(active)))
        {
            ranges.Add(new CellRange(active, active));
        }

        var frozenRows = change.Axis == WorksheetAxis.Row
            ? change.MapBoundary(state.FrozenRows)
            : state.FrozenRows;
        var frozenColumns = change.Axis == WorksheetAxis.Column
            ? change.MapBoundary(state.FrozenColumns)
            : state.FrozenColumns;

        var split = state.SplitState;
        foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
        {
            var scroll = state.SplitState.GetPaneScroll(pane);
            var offsetX = change.Axis == WorksheetAxis.Column
                ? MapScrollOffset(worksheet, worksheetBefore, change, scroll.OffsetX)
                : scroll.OffsetX;
            var offsetY = change.Axis == WorksheetAxis.Row
                ? MapScrollOffset(worksheet, worksheetBefore, change, scroll.OffsetY)
                : scroll.OffsetY;
            split = split.WithPaneScroll(pane, offsetX, offsetY);
        }

        return new SpreadsheetWorksheetViewState(
            new SelectionSnapshot(
                active,
                anchor,
                Array.AsReadOnly(ranges.ToArray()),
                state.Selection.Version),
            state.Zoom,
            split,
            frozenRows,
            frozenColumns);
    }

    private static CellAddress MapAddress(
        CellAddress source,
        WorksheetStructuralChange change)
    {
        if (change.TryMapAddress(source, out var mapped))
        {
            return mapped;
        }

        var replacementIndex = Math.Min(change.Index, change.AxisLength - 1);
        return change.Axis == WorksheetAxis.Row
            ? new CellAddress(replacementIndex, source.ColumnIndex)
            : new CellAddress(source.RowIndex, replacementIndex);
    }

    private static bool TryMapRange(
        CellRange source,
        WorksheetStructuralChange change,
        out CellRange mapped)
    {
        var startsAtOrigin = change.Axis == WorksheetAxis.Row
            ? source.Top == 0
            : source.Left == 0;
        var reachesAxisEnd = change.Axis == WorksheetAxis.Row
            ? source.Bottom == change.AxisLength - 1
            : source.Right == change.AxisLength - 1;
        if (startsAtOrigin && reachesAxisEnd)
        {
            mapped = source;
            return true;
        }
        return change.TryMapRange(source, out mapped);
    }

    private static double MapScrollOffset(
        Worksheet worksheet,
        WorksheetStructuralState before,
        WorksheetStructuralChange change,
        double offset)
    {
        var overrides = change.Axis == WorksheetAxis.Row
            ? before.RowHeights
            : before.ColumnWidths;
        var hiddenRanges = change.Axis == WorksheetAxis.Row
            ? before.HiddenRows
            : before.HiddenColumns;
        var defaultSize = change.Axis == WorksheetAxis.Row
            ? worksheet.Dimensions.DefaultRowHeight
            : worksheet.Dimensions.DefaultColumnWidth;
        var changeStart = GetAxisOffset(
            change.Index,
            defaultSize,
            overrides,
            hiddenRanges);
        if (change.Kind == WorksheetStructuralChangeKind.Insert)
        {
            return offset < changeStart
                ? offset
                : offset + (change.Count * defaultSize);
        }

        var changeEnd = GetAxisOffset(
            change.Index + change.Count,
            defaultSize,
            overrides,
            hiddenRanges);
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
            if (overrideIndex < index)
            {
                offset += size - defaultSize;
            }
        }
        return offset;
    }

    private static bool SameState(
        SpreadsheetWorksheetViewState left,
        SpreadsheetWorksheetViewState right) =>
        left.Zoom == right.Zoom &&
        left.SplitState == right.SplitState &&
        left.FrozenRows == right.FrozenRows &&
        left.FrozenColumns == right.FrozenColumns &&
        left.Selection.ActiveCell == right.Selection.ActiveCell &&
        left.Selection.AnchorCell == right.Selection.AnchorCell &&
        left.Selection.Ranges.SequenceEqual(right.Selection.Ranges);

    private static void Prune(SessionBucket bucket) =>
        bucket.Sessions.RemoveAll(static reference => !reference.TryGetTarget(out _));

    private sealed class SessionBucket
    {
        public object Gate { get; } = new();
        public List<WeakReference<SpreadsheetSession>> Sessions { get; } = [];
    }

    internal sealed class PeerViewSnapshot
    {
        internal PeerViewSnapshot(
            SpreadsheetSession session,
            Worksheet worksheet,
            SpreadsheetWorksheetViewState beforeState)
        {
            Session = new WeakReference<SpreadsheetSession>(session);
            Worksheet = worksheet;
            BeforeState = beforeState;
        }

        internal WeakReference<SpreadsheetSession> Session { get; }
        internal Worksheet Worksheet { get; }
        internal SpreadsheetWorksheetViewState BeforeState { get; }
        internal SpreadsheetWorksheetViewState? AppliedState { get; set; }
    }
}
