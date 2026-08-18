using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSplitViewStateTests
{
    [TestMethod]
    public void SplitStateIsStoredIndependentlyPerWorksheet()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        var firstState = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            320.5d,
            180.25d,
            SpreadsheetSplitViewPane.BottomRight,
            topLeftScroll: new SpreadsheetPaneScrollOffset(10.5d, 20.25d),
            topRightScroll: new SpreadsheetPaneScrollOffset(110.75d, 30.5d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(40d, 220.25d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(140.5d, 260.75d));

        Assert.IsTrue(session.View.SetSplitState(firstState));
        Assert.AreEqual(firstState, session.View.SplitState);

        session.ActivateWorksheet(second);
        Assert.AreEqual(default, session.View.SplitState);
        var secondState = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Horizontal,
            null,
            210d,
            SpreadsheetSplitViewPane.BottomLeft,
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(12d, 340d));
        Assert.IsTrue(session.View.SetSplitState(secondState));

        session.ActivateWorksheet(first);
        Assert.AreEqual(firstState, session.View.SplitState);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(140.5d, 260.75d),
            session.View.SplitState.BottomRightScroll);
    }

    [TestMethod]
    public void HiddenPaneScrollSurvivesTopologyChanges()
    {
        var state = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            300d,
            200d,
            SpreadsheetSplitViewPane.BottomRight,
            bottomRightScroll: new SpreadsheetPaneScrollOffset(720.5d, 840.25d));

        var vertical = state.WithTopology(
            SpreadsheetSplitViewMode.Vertical,
            260d,
            null);

        Assert.AreEqual(SpreadsheetSplitViewPane.TopLeft, vertical.ActivePane);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(720.5d, 840.25d),
            vertical.BottomRightScroll);

        var restored = vertical.WithTopology(
            SpreadsheetSplitViewMode.Both,
            260d,
            190d);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(720.5d, 840.25d),
            restored.GetPaneScroll(SpreadsheetSplitViewPane.BottomRight));
    }

    [TestMethod]
    public void ClearingSplitPreservesRememberedPaneOffsets()
    {
        var session = new SpreadsheetSession(new Workbook());
        Assert.IsTrue(session.View.SetSplitState(new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            280d,
            180d,
            SpreadsheetSplitViewPane.TopRight,
            topRightScroll: new SpreadsheetPaneScrollOffset(512.25d, 64.5d))));

        Assert.IsTrue(session.View.ClearSplitPanes());

        Assert.AreEqual(SpreadsheetSplitViewMode.None, session.View.SplitState.Mode);
        Assert.AreEqual(SpreadsheetSplitViewPane.TopLeft, session.View.SplitState.ActivePane);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(512.25d, 64.5d),
            session.View.SplitState.TopRightScroll);
        Assert.IsTrue(session.View.TryGetSplitState(
            session.ActiveWorksheet,
            out var stored));
        Assert.AreEqual(session.View.SplitState, stored);
    }

    [TestMethod]
    public void SplitChangeEventCarriesKindSourceAndWorksheet()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetSplitTopology(
            SpreadsheetSplitViewMode.Vertical,
            240d,
            null);
        var source = new object();
        SpreadsheetSplitViewChangedEventArgs? observed = null;
        session.View.SplitChanged += (_, args) => observed = args;

        Assert.IsTrue(session.View.SetSplitPaneScroll(
            SpreadsheetSplitViewPane.TopRight,
            350.5d,
            18.25d,
            source));

        Assert.IsNotNull(observed);
        Assert.AreSame(session.ActiveWorksheet, observed.Worksheet);
        Assert.AreEqual(
            SpreadsheetSplitViewChangeKind.PaneScroll,
            observed.ChangeKind);
        Assert.AreSame(source, observed.Source);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(350.5d, 18.25d),
            observed.State.TopRightScroll);
    }

    [TestMethod]
    public void TopologyRequiresMatchingFiniteCoordinates()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Vertical,
            null,
            null));
        Assert.ThrowsExactly<ArgumentException>(() => new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Horizontal,
            100d,
            200d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            double.NaN,
            200d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetPaneScrollOffset(-1d, 0d));
    }
}
