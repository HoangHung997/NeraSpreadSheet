using System.Windows.Documents;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private SpreadsheetSplitViewHistoryTransaction? _splitViewHistory;
    private bool _commitSplitViewHistoryWhenFrameIdle;

    internal bool CanUndoSplitViewChange =>
        _session?.View.CanUndoSplitViewChange == true;

    internal bool CanRedoSplitViewChange =>
        _session?.View.CanRedoSplitViewChange == true;

    internal string? NextSplitViewUndoDescription =>
        _session?.View.NextSplitViewUndoDescription;

    internal string? NextSplitViewRedoDescription =>
        _session?.View.NextSplitViewRedoDescription;

    internal void SetModeWithHistory(SpreadsheetSplitPaneMode mode)
    {
        ExecuteSplitViewChange(
            "Change split topology",
            SpreadsheetSplitViewChangeKind.Topology,
            () => SetMode(mode));
    }

    internal void SetSplitWithHistory(double? splitX, double? splitY)
    {
        ExecuteSplitViewChange(
            "Move split separator",
            SpreadsheetSplitViewChangeKind.Topology,
            () => SetSplit(splitX, splitY));
    }

    internal void SetActivePaneWithHistory(SpreadsheetPaneId paneId)
    {
        ExecuteSplitViewChange(
            "Change active split pane",
            SpreadsheetSplitViewChangeKind.ActivePane,
            () => SetActivePane(paneId));
    }

    internal void ScrollPaneToWithHistory(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated)
    {
        if (animated)
        {
            BeginSplitViewHistory(
                "Scroll split pane",
                SpreadsheetSplitViewChangeKind.PaneScroll);
            _commitSplitViewHistoryWhenFrameIdle = true;
            ScrollPaneTo(paneId, offsetX, offsetY, animated: true);
            return;
        }

        ExecuteSplitViewChange(
            "Scroll split pane",
            SpreadsheetSplitViewChangeKind.PaneScroll,
            () => ScrollPaneTo(
                paneId,
                offsetX,
                offsetY,
                animated: false));
    }

    internal void QueuePaneScrollWithHistory(
        SpreadsheetPaneId paneId,
        ScrollDelta delta)
    {
        BeginSplitViewHistory(
            "Scroll split pane",
            SpreadsheetSplitViewChangeKind.PaneScroll);
        _commitSplitViewHistoryWhenFrameIdle = true;
        QueuePaneScroll(paneId, delta);
    }

    internal void QueueActivePaneScrollWithHistory(ScrollDelta delta)
    {
        BeginSplitViewHistory(
            "Scroll split pane",
            SpreadsheetSplitViewChangeKind.PaneScroll);
        _commitSplitViewHistoryWhenFrameIdle = true;
        QueueActivePaneScroll(delta);
    }

    internal bool BeginSplitViewHistory(
        string description,
        SpreadsheetSplitViewChangeKind changeKind)
    {
        SynchronizeSession();
        if (_splitViewHistory is not null || _session is null)
        {
            return false;
        }

        _splitViewHistory = _session.View.BeginSplitViewHistoryTransaction(
            description,
            changeKind);
        return true;
    }

    internal bool CommitSplitViewHistory()
    {
        var transaction = _splitViewHistory;
        _splitViewHistory = null;
        _commitSplitViewHistoryWhenFrameIdle = false;
        if (transaction is null)
        {
            return false;
        }

        using (transaction)
        {
            return transaction.Commit();
        }
    }

    internal bool CancelSplitViewHistory(bool restoreBeforeState = true)
    {
        var transaction = _splitViewHistory;
        _splitViewHistory = null;
        _commitSplitViewHistoryWhenFrameIdle = false;
        if (transaction is null)
        {
            return false;
        }

        using (transaction)
        {
            return transaction.Cancel(restoreBeforeState);
        }
    }

    internal bool UndoSplitViewChange()
    {
        CommitSplitViewHistory();
        return _session?.View.UndoSplitViewChange() == true;
    }

    internal bool RedoSplitViewChange()
    {
        CommitSplitViewHistory();
        return _session?.View.RedoSplitViewChange() == true;
    }

    private void ExecuteSplitViewChange(
        string description,
        SpreadsheetSplitViewChangeKind changeKind,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var ownsTransaction = BeginSplitViewHistory(description, changeKind);
        try
        {
            action();
            if (ownsTransaction)
            {
                CommitSplitViewHistory();
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                CancelSplitViewHistory(restoreBeforeState: true);
            }
            throw;
        }
    }

    private void CommitSplitViewHistoryWhenFrameSettles()
    {
        if (_commitSplitViewHistoryWhenFrameIdle &&
            _engine?.HasPendingScroll != true)
        {
            CommitSplitViewHistory();
        }
    }
}
