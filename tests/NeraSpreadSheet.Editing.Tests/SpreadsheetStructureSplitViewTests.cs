using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureSplitViewTests
{
    [TestMethod]
    public void InsertRowsMapsPaneScrollAndUndoRedoRestoreExactState()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Dimensions.SetRowHeight(1, 35d);
        var session = new SpreadsheetSession(workbook);
        var before = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            300d,
            200d,
            SpreadsheetSplitViewPane.BottomRight,
            topLeftScroll: new SpreadsheetPaneScrollOffset(11d, 40d),
            topRightScroll: new SpreadsheetPaneScrollOffset(22d, 55d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(33d, 100d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(44d, 0d));
        session.View.SetSplitState(before);

        session.Structure.InsertRows(2, 2);

        var mapped = session.View.SplitState;
        Assert.AreEqual(SpreadsheetSplitViewMode.Both, mapped.Mode);
        Assert.AreEqual(300d, mapped.SplitX);
        Assert.AreEqual(200d, mapped.SplitY);
        Assert.AreEqual(SpreadsheetSplitViewPane.BottomRight, mapped.ActivePane);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(11d, 40d), mapped.TopLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(22d, 95d), mapped.TopRightScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(33d, 140d), mapped.BottomLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(44d, 0d), mapped.BottomRightScroll);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, session.View.SplitState);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(mapped, session.View.SplitState);
    }

    [TestMethod]
    public void DeleteRowsCollapsesOffsetsInsideIntervalAndSubtractsExactDeletedExtent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Dimensions.SetRowHeight(2, 50d);
        var session = new SpreadsheetSession(workbook);
        session.View.SetSplitState(new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            280d,
            180d,
            topLeftScroll: new SpreadsheetPaneScrollOffset(0d, 10d),
            topRightScroll: new SpreadsheetPaneScrollOffset(0d, 20d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(0d, 60d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(0d, 100d)));

        session.Structure.DeleteRows(1, 2);

        var mapped = session.View.SplitState;
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(0d, 10d), mapped.TopLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(0d, 20d), mapped.TopRightScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(0d, 20d), mapped.BottomLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(0d, 30d), mapped.BottomRightScroll);
    }

    [TestMethod]
    public void DeleteColumnsMapsHorizontalOffsetsWithoutChangingVerticalOffsetsOrTopology()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Dimensions.SetColumnWidth(1, 120d);
        worksheet.Dimensions.SetColumnWidth(2, 60d);
        var session = new SpreadsheetSession(workbook);
        session.View.SetSplitState(new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Vertical,
            260d,
            null,
            SpreadsheetSplitViewPane.TopRight,
            topLeftScroll: new SpreadsheetPaneScrollOffset(40d, 13d),
            topRightScroll: new SpreadsheetPaneScrollOffset(200d, 23d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(300d, 33d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(260d, 43d)));

        session.Structure.DeleteColumns(1, 2);

        var mapped = session.View.SplitState;
        Assert.AreEqual(SpreadsheetSplitViewMode.Vertical, mapped.Mode);
        Assert.AreEqual(260d, mapped.SplitX);
        Assert.IsNull(mapped.SplitY);
        Assert.AreEqual(SpreadsheetSplitViewPane.TopRight, mapped.ActivePane);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(40d, 13d), mapped.TopLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(80d, 23d), mapped.TopRightScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(120d, 33d), mapped.BottomLeftScroll);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(80d, 43d), mapped.BottomRightScroll);
    }

    [TestMethod]
    public void FailedStructuralInsertDoesNotMutateSplitState()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(SpreadsheetLimits.MaxRows - 1, 0), "edge");
        var session = new SpreadsheetSession(workbook);
        var before = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Horizontal,
            null,
            190d,
            SpreadsheetSplitViewPane.BottomLeft,
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(12d, 340d));
        session.View.SetSplitState(before);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Structure.InsertRows(0));

        Assert.AreEqual(before, session.View.SplitState);
        Assert.IsFalse(session.History.CanUndo);
    }
}
