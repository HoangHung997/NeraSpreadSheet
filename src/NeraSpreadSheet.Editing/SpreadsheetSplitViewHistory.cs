using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetSplitViewHistoryTransaction : IDisposable
{
    private SpreadsheetViewController? _owner;

    internal SpreadsheetSplitViewHistoryTransaction(
        SpreadsheetViewController owner,
        Worksheet worksheet,
        SpreadsheetSplitViewState beforeState,
        SpreadsheetSplitViewChangeKind changeKind,
        string description)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        BeforeState = beforeState;
        ChangeKind = changeKind;
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description.Trim();
    }

    public Worksheet Worksheet { get; }

    public SpreadsheetSplitViewState BeforeState { get; }

    public SpreadsheetSplitViewChangeKind ChangeKind { get; }

    public string Description { get; }

    public bool IsCompleted => _owner is null;

    public bool Commit()
    {
        var owner = GetOwner();
        var recorded = owner.CommitSplitViewHistoryTransaction(this);
        _owner = null;
        return recorded;
    }

    public bool Cancel(bool restoreBeforeState = true)
    {
        var owner = GetOwner();
        var restored = owner.CancelSplitViewHistoryTransaction(
            this,
            restoreBeforeState);
        _owner = null;
        return restored;
    }

    public void Dispose()
    {
        if (_owner is not null)
        {
            Cancel(restoreBeforeState: true);
        }
    }

    private SpreadsheetViewController GetOwner()
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _owner!;
    }
}

public sealed partial class SpreadsheetViewController
{
    private const int DefaultSplitViewHistoryDepth = 256;
    private readonly Dictionary<Worksheet, UndoRedoManager>
        _splitViewHistories = [];

    public bool CanUndoSplitViewChange =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history) &&
        history.CanUndo;

    public bool CanRedoSplitViewChange =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history) &&
        history.CanRedo;

    public int SplitViewUndoCount =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history)
            ? history.UndoCount
            : 0;

    public int SplitViewRedoCount =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history)
            ? history.RedoCount
            : 0;

    public string? NextSplitViewUndoDescription =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history)
            ? history.NextUndoDescription
            : null;

    public string? NextSplitViewRedoDescription =>
        TryGetSplitViewHistory(_session.ActiveWorksheet, out var history)
            ? history.NextRedoDescription
            : null;

    public bool ExecuteSplitViewChange(
        SpreadsheetSplitViewState state,
        string description,
        SpreadsheetSplitViewChangeKind changeKind =
            SpreadsheetSplitViewChangeKind.State) =>
        ExecuteSplitViewChange(
            _session.ActiveWorksheet,
            state,
            description,
            changeKind);

    public bool ExecuteSplitViewChange(
        Worksheet worksheet,
        SpreadsheetSplitViewState state,
        string description,
        SpreadsheetSplitViewChangeKind changeKind =
            SpreadsheetSplitViewChangeKind.State)
    {
        ValidateWorksheet(worksheet);
        ValidateHistoryChangeKind(changeKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var before = GetSplitState(worksheet);
        if (before == state)
        {
            return false;
        }

        GetOrCreateSplitViewHistory(worksheet).Execute(
            new SpreadsheetSplitViewOperation(
                this,
                worksheet,
                before,
                state,
                changeKind,
                description));
        return true;
    }

    public bool ExecuteSplitTopologyChange(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY,
        string description = "Change split topology") =>
        ExecuteSplitViewChange(
            SplitState.WithTopology(mode, splitX, splitY),
            description,
            SpreadsheetSplitViewChangeKind.Topology);

    public bool ExecuteSplitActivePaneChange(
        SpreadsheetSplitViewPane pane,
        string description = "Change active split pane") =>
        ExecuteSplitViewChange(
            SplitState.WithActivePane(pane),
            description,
            SpreadsheetSplitViewChangeKind.ActivePane);

    public bool ExecuteSplitPaneScrollChange(
        SpreadsheetSplitViewPane pane,
        double offsetX,
        double offsetY,
        string description = "Scroll split pane") =>
        ExecuteSplitViewChange(
            SplitState.WithPaneScroll(pane, offsetX, offsetY),
            description,
            SpreadsheetSplitViewChangeKind.PaneScroll);

    public SpreadsheetSplitViewHistoryTransaction
        BeginSplitViewHistoryTransaction(
            string description,
            SpreadsheetSplitViewChangeKind changeKind =
                SpreadsheetSplitViewChangeKind.State,
            Worksheet? worksheet = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ValidateHistoryChangeKind(changeKind);
        var target = worksheet ?? _session.ActiveWorksheet;
        ValidateWorksheet(target);
        return new SpreadsheetSplitViewHistoryTransaction(
            this,
            target,
            GetSplitState(target),
            changeKind,
            description);
    }

    public bool UndoSplitViewChange()
    {
        if (!TryGetSplitViewHistory(
                _session.ActiveWorksheet,
                out var history))
        {
            return false;
        }
        return history.Undo();
    }

    public bool RedoSplitViewChange()
    {
        if (!TryGetSplitViewHistory(
                _session.ActiveWorksheet,
                out var history))
        {
            return false;
        }
        return history.Redo();
    }

    public void ClearSplitViewHistory(Worksheet? worksheet = null)
    {
        var target = worksheet ?? _session.ActiveWorksheet;
        ValidateWorksheet(target);
        _splitViewHistories.Remove(target);
    }

    internal bool CommitSplitViewHistoryTransaction(
        SpreadsheetSplitViewHistoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ValidateWorksheet(transaction.Worksheet);
        var after = GetSplitState(transaction.Worksheet);
        if (after == transaction.BeforeState)
        {
            return false;
        }

        GetOrCreateSplitViewHistory(transaction.Worksheet).RecordExecuted(
            new SpreadsheetSplitViewOperation(
                this,
                transaction.Worksheet,
                transaction.BeforeState,
                after,
                transaction.ChangeKind,
                transaction.Description));
        return true;
    }

    internal bool CancelSplitViewHistoryTransaction(
        SpreadsheetSplitViewHistoryTransaction transaction,
        bool restoreBeforeState)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ValidateWorksheet(transaction.Worksheet);
        if (!restoreBeforeState ||
            GetSplitState(transaction.Worksheet) == transaction.BeforeState)
        {
            return false;
        }

        return SetSplitState(
            transaction.Worksheet,
            transaction.BeforeState,
            SpreadsheetSplitViewChangeKind.History,
            transaction);
    }

    private UndoRedoManager GetOrCreateSplitViewHistory(
        Worksheet worksheet)
    {
        if (_splitViewHistories.TryGetValue(worksheet, out var history))
        {
            return history;
        }

        history = new UndoRedoManager(DefaultSplitViewHistoryDepth);
        _splitViewHistories.Add(worksheet, history);
        return history;
    }

    private bool TryGetSplitViewHistory(
        Worksheet worksheet,
        out UndoRedoManager history) =>
        _splitViewHistories.TryGetValue(worksheet, out history!);

    private static void ValidateHistoryChangeKind(
        SpreadsheetSplitViewChangeKind changeKind)
    {
        if (!Enum.IsDefined(changeKind) ||
            changeKind is SpreadsheetSplitViewChangeKind.ActiveWorksheet or
                SpreadsheetSplitViewChangeKind.History)
        {
            throw new ArgumentOutOfRangeException(nameof(changeKind));
        }
    }

    private sealed class SpreadsheetSplitViewOperation : IUndoableOperation
    {
        private readonly SpreadsheetViewController _owner;
        private readonly Worksheet _worksheet;
        private readonly SpreadsheetSplitViewState _before;
        private readonly SpreadsheetSplitViewState _after;
        private readonly SpreadsheetSplitViewChangeKind _changeKind;

        public SpreadsheetSplitViewOperation(
            SpreadsheetViewController owner,
            Worksheet worksheet,
            SpreadsheetSplitViewState before,
            SpreadsheetSplitViewState after,
            SpreadsheetSplitViewChangeKind changeKind,
            string description)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _worksheet = worksheet ??
                throw new ArgumentNullException(nameof(worksheet));
            _before = before;
            _after = after;
            _changeKind = changeKind;
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            Description = description.Trim();
        }

        public string Description { get; }

        public void Execute() => _owner.SetSplitState(
            _worksheet,
            _after,
            _changeKind,
            this);

        public void Undo() => _owner.SetSplitState(
            _worksheet,
            _before,
            SpreadsheetSplitViewChangeKind.History,
            this);
    }
}
