using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSplitViewHistoryTests
{
    [TestMethod]
    public void ExecuteTopologyChangeSupportsExactUndoAndRedo()
    {
        var session = new SpreadsheetSession(new Workbook());
        var before = CreateState(
            SpreadsheetSplitViewMode.Both,
            splitX: 280.5d,
            splitY: 175.25d,
            activePane: SpreadsheetSplitViewPane.BottomRight,
            seed: 10d);
        Assert.IsTrue(session.View.SetSplitState(before));
        var after = before.WithTopology(
            SpreadsheetSplitViewMode.Vertical,
            340.75d,
            null);

        Assert.IsTrue(session.View.ExecuteSplitViewChange(
            after,
            "Change split topology",
            SpreadsheetSplitViewChangeKind.Topology));

        Assert.AreEqual(after, session.View.SplitState);
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
        Assert.AreEqual(0, session.View.SplitViewRedoCount);
        Assert.AreEqual(
            "Change split topology",
            session.View.NextSplitViewUndoDescription);
        Assert.AreEqual(0, session.History.UndoCount);

        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.AreEqual(before, session.View.SplitState);
        Assert.AreEqual(0, session.View.SplitViewUndoCount);
        Assert.AreEqual(1, session.View.SplitViewRedoCount);

        Assert.IsTrue(session.View.RedoSplitViewChange());
        Assert.AreEqual(after, session.View.SplitState);
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
        Assert.AreEqual(0, session.View.SplitViewRedoCount);
    }

    [TestMethod]
    public void HistoriesAreIsolatedPerWorksheet()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        var firstState = CreateState(
            SpreadsheetSplitViewMode.Vertical,
            splitX: 260d,
            splitY: null,
            activePane: SpreadsheetSplitViewPane.TopRight,
            seed: 20d);
        Assert.IsTrue(session.View.ExecuteSplitViewChange(
            first,
            firstState,
            "First sheet split",
            SpreadsheetSplitViewChangeKind.Topology));
        Assert.AreEqual(1, session.View.SplitViewUndoCount);

        session.ActivateWorksheet(second);
        Assert.IsFalse(session.View.CanUndoSplitViewChange);
        var secondState = CreateState(
            SpreadsheetSplitViewMode.Horizontal,
            splitX: null,
            splitY: 190d,
            activePane: SpreadsheetSplitViewPane.BottomLeft,
            seed: 30d);
        Assert.IsTrue(session.View.ExecuteSplitViewChange(
            second,
            secondState,
            "Second sheet split",
            SpreadsheetSplitViewChangeKind.Topology));
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
        Assert.AreEqual(
            "Second sheet split",
            session.View.NextSplitViewUndoDescription);

        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.AreEqual(default, session.View.SplitState);
        Assert.AreEqual(firstState, session.View.GetSplitState(first));

        session.ActivateWorksheet(first);
        Assert.IsTrue(session.View.CanUndoSplitViewChange);
        Assert.AreEqual(
            "First sheet split",
            session.View.NextSplitViewUndoDescription);
        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.AreEqual(default, session.View.SplitState);
        Assert.AreEqual(secondState, session.View.GetSplitState(second));
    }

    [TestMethod]
    public void TransactionCoalescesManyLowLevelScrollUpdates()
    {
        var session = new SpreadsheetSession(new Workbook());
        var before = CreateState(
            SpreadsheetSplitViewMode.Both,
            splitX: 300d,
            splitY: 200d,
            activePane: SpreadsheetSplitViewPane.BottomRight,
            seed: 40d);
        Assert.IsTrue(session.View.SetSplitState(before));

        using (var transaction = session.View.BeginSplitViewHistoryTransaction(
            "Drag pane scrollbar",
            SpreadsheetSplitViewChangeKind.PaneScroll))
        {
            for (var step = 1; step <= 100; step++)
            {
                Assert.IsTrue(session.View.SetSplitPaneScroll(
                    SpreadsheetSplitViewPane.BottomRight,
                    40d + (step * 2.25d),
                    80d + (step * 1.5d)));
            }
            Assert.IsTrue(transaction.Commit());
        }

        var after = session.View.SplitState;
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(265d, 230d),
            after.BottomRightScroll);
        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.AreEqual(before, session.View.SplitState);
        Assert.IsTrue(session.View.RedoSplitViewChange());
        Assert.AreEqual(after, session.View.SplitState);
    }

    [TestMethod]
    public void NoChangeTransactionDoesNotCreateHistory()
    {
        var session = new SpreadsheetSession(new Workbook());

        using var transaction = session.View.BeginSplitViewHistoryTransaction(
            "No change",
            SpreadsheetSplitViewChangeKind.State);

        Assert.IsFalse(transaction.Commit());
        Assert.AreEqual(0, session.View.SplitViewUndoCount);
        Assert.IsFalse(session.View.CanUndoSplitViewChange);
    }

    [TestMethod]
    public void CancelRestoresBeforeStateWithoutHistory()
    {
        var session = new SpreadsheetSession(new Workbook());
        var before = CreateState(
            SpreadsheetSplitViewMode.Both,
            splitX: 310d,
            splitY: 205d,
            activePane: SpreadsheetSplitViewPane.TopRight,
            seed: 50d);
        Assert.IsTrue(session.View.SetSplitState(before));
        using var transaction = session.View.BeginSplitViewHistoryTransaction(
            "Cancelled separator drag",
            SpreadsheetSplitViewChangeKind.Topology);
        Assert.IsTrue(session.View.SetSplitTopology(
            SpreadsheetSplitViewMode.Vertical,
            470d,
            null));

        Assert.IsTrue(transaction.Cancel());

        Assert.AreEqual(before, session.View.SplitState);
        Assert.AreEqual(0, session.View.SplitViewUndoCount);
    }

    [TestMethod]
    public void DisposingOpenTransactionRestoresBeforeState()
    {
        var session = new SpreadsheetSession(new Workbook());
        var before = CreateState(
            SpreadsheetSplitViewMode.Vertical,
            splitX: 260d,
            splitY: null,
            activePane: SpreadsheetSplitViewPane.TopRight,
            seed: 60d);
        Assert.IsTrue(session.View.SetSplitState(before));

        using (session.View.BeginSplitViewHistoryTransaction(
            "Abandoned scroll",
            SpreadsheetSplitViewChangeKind.PaneScroll))
        {
            Assert.IsTrue(session.View.SetSplitPaneScroll(
                SpreadsheetSplitViewPane.TopRight,
                900d,
                120d));
        }

        Assert.AreEqual(before, session.View.SplitState);
        Assert.AreEqual(0, session.View.SplitViewUndoCount);
    }

    [TestMethod]
    public void NewCommittedChangeClearsRedoHistory()
    {
        var session = new SpreadsheetSession(new Workbook());
        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Vertical,
            280d,
            null));
        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.IsTrue(session.View.CanRedoSplitViewChange);

        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Horizontal,
            null,
            190d,
            "New split topology"));

        Assert.IsFalse(session.View.CanRedoSplitViewChange);
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
        Assert.AreEqual(
            "New split topology",
            session.View.NextSplitViewUndoDescription);
    }

    [TestMethod]
    public void ClearHistoryTargetsOnlySelectedWorksheet()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Vertical,
            250d,
            null));
        session.ActivateWorksheet(second);
        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Horizontal,
            null,
            180d));

        session.View.ClearSplitViewHistory();

        Assert.IsFalse(session.View.CanUndoSplitViewChange);
        session.ActivateWorksheet(first);
        Assert.IsTrue(session.View.CanUndoSplitViewChange);
        Assert.AreEqual(1, session.View.SplitViewUndoCount);
    }

    [TestMethod]
    public async Task CommandsReflectAndExecuteSplitViewHistory()
    {
        var session = new SpreadsheetSession(new Workbook());
        Assert.IsFalse(session.CommandDispatcher.QueryState(
            SpreadsheetViewCommandIds.UndoSplitViewChange).IsEnabled);
        Assert.IsFalse(session.CommandDispatcher.QueryState(
            SpreadsheetViewCommandIds.RedoSplitViewChange).IsEnabled);
        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Vertical,
            275d,
            null));

        Assert.IsTrue(session.CommandDispatcher.QueryState(
            SpreadsheetViewCommandIds.UndoSplitViewChange).IsEnabled);
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetViewCommandIds.UndoSplitViewChange));
        Assert.AreEqual(default, session.View.SplitState);
        Assert.IsTrue(session.CommandDispatcher.QueryState(
            SpreadsheetViewCommandIds.RedoSplitViewChange).IsEnabled);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetViewCommandIds.RedoSplitViewChange));
        Assert.AreEqual(
            SpreadsheetSplitViewMode.Vertical,
            session.View.SplitState.Mode);
    }

    [TestMethod]
    public void HistoryEventsIdentifyUndoAndRedo()
    {
        var session = new SpreadsheetSession(new Workbook());
        var observedKinds = new List<SpreadsheetSplitViewChangeKind>();
        session.View.SplitChanged += (_, args) =>
            observedKinds.Add(args.ChangeKind);
        Assert.IsTrue(session.View.ExecuteSplitTopologyChange(
            SpreadsheetSplitViewMode.Vertical,
            265d,
            null));
        Assert.IsTrue(session.View.UndoSplitViewChange());
        Assert.IsTrue(session.View.RedoSplitViewChange());

        CollectionAssert.AreEqual(
            new[]
            {
                SpreadsheetSplitViewChangeKind.Topology,
                SpreadsheetSplitViewChangeKind.History,
                SpreadsheetSplitViewChangeKind.Topology,
            },
            observedKinds);
    }

    private static SpreadsheetSplitViewState CreateState(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY,
        SpreadsheetSplitViewPane activePane,
        double seed) => new(
        mode,
        splitX,
        splitY,
        activePane,
        new SpreadsheetPaneScrollOffset(seed + 1d, seed + 2d),
        new SpreadsheetPaneScrollOffset(seed + 3d, seed + 4d),
        new SpreadsheetPaneScrollOffset(seed + 5d, seed + 6d),
        new SpreadsheetPaneScrollOffset(seed + 7d, seed + 8d));
}
