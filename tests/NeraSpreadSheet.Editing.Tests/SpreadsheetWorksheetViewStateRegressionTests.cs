using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetWorksheetViewStateRegressionTests
{
    [TestMethod]
    public void ScrollShouldNotNormalizeHiddenActiveCellOrRepublishFreeze()
    {
        var session = new SpreadsheetSession(new Workbook());
        var worksheet = session.ActiveWorksheet;
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        worksheet.Dimensions.HideRows(2);
        worksheet.Dimensions.HideColumns(2);
        var before = session.Selection.Capture();
        var version = worksheet.Version;
        var selectionEvents = 0;
        var freezeEvents = 0;
        var scrollEvents = 0;
        session.Selection.Changed += (_, _) => selectionEvents++;
        session.View.Changed += (_, _) => freezeEvents++;
        session.View.SplitChanged += (_, e) =>
        {
            Assert.AreEqual(SpreadsheetSplitViewChangeKind.PaneScroll, e.ChangeKind);
            scrollEvents++;
        };

        Assert.IsTrue(session.View.SetWorksheetViewport(worksheet, 12.125, 23.875, 1.5));

        Assert.AreEqual(before.ActiveCell, session.Selection.ActiveCell);
        Assert.AreEqual(before.AnchorCell, session.Selection.AnchorCell);
        Assert.AreEqual(before.Version, session.Selection.Version);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual(0, selectionEvents);
        Assert.AreEqual(0, freezeEvents);
        Assert.AreEqual(1, scrollEvents);
        Assert.AreEqual(12.125, session.View.WorksheetState.OffsetX);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void ZoomOnlyShouldNotifyViewWithoutSelectionFreezeOrSplitChanges()
    {
        var session = new SpreadsheetSession(new Workbook());
        var version = session.View.Version;
        var selections = 0;
        var freezes = 0;
        var splits = 0;
        var views = 0;
        session.Selection.Changed += (_, _) => selections++;
        session.View.Changed += (_, _) => freezes++;
        session.View.SplitChanged += (_, _) => splits++;
        session.View.WorksheetViewChanged += (_, _) => views++;

        Assert.IsTrue(session.View.SetWorksheetViewport(session.ActiveWorksheet, 0, 0, 1.25));

        Assert.AreEqual(0, selections);
        Assert.AreEqual(0, freezes);
        Assert.AreEqual(0, splits);
        Assert.AreEqual(1, views);
        Assert.AreEqual(version + 1, session.View.Version);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void IdenticalViewportUpdateShouldNotPublishOrAdvanceVersion()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 11.5, 22.25, 1.5);
        var version = session.View.Version;
        var events = 0;
        session.View.WorksheetViewChanged += (_, _) => events++;

        Assert.IsFalse(session.View.SetWorksheetViewport(session.ActiveWorksheet, 11.5, 22.25, 1.5));

        Assert.AreEqual(version, session.View.Version);
        Assert.AreEqual(0, events);
    }

    [TestMethod]
    public void ViewportObserverFailureShouldReleaseFeedbackGuardAndKeepOriginalException()
    {
        var session = new SpreadsheetSession(new Workbook());
        var error = new IOException("view observer");
        EventHandler<SpreadsheetWorksheetViewChangedEventArgs> handler = (_, _) => throw error;
        session.View.WorksheetViewChanged += handler;
        try
        {
            var caught = Assert.ThrowsExactly<IOException>(() =>
                session.View.SetWorksheetViewport(session.ActiveWorksheet, 10.5, 20.5, 1.2));
            Assert.AreSame(error, caught);
        }
        finally { session.View.WorksheetViewChanged -= handler; }

        Assert.IsFalse(session.View.IsRestoringWorksheetView);
        Assert.IsTrue(session.View.SetWorksheetViewport(session.ActiveWorksheet, 30.5, 40.5, 1.3));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void ActivationFailureBeforeSwitchShouldNotPublishCompletionOrMaskOriginalError()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.Editor.BeginEdit();
        var original = new InvalidOperationException("editor cancel observer");
        var completions = 0;
        EventHandler<CellEditStateChangedEventArgs> editorHandler = (_, _) => throw original;
        EventHandler<SpreadsheetWorksheetViewChangedEventArgs> completionHandler = (_, _) =>
        {
            completions++;
            throw new IOException("must not replace original error");
        };
        session.Editor.StateChanged += editorHandler;
        session.View.WorksheetViewChanged += completionHandler;
        try
        {
            var caught = Assert.ThrowsExactly<InvalidOperationException>(() => session.ActivateWorksheet(second));
            Assert.AreSame(original, caught);
        }
        finally
        {
            session.Editor.StateChanged -= editorHandler;
            session.View.WorksheetViewChanged -= completionHandler;
        }

        Assert.AreSame(first, session.ActiveWorksheet);
        Assert.AreEqual(0, completions);
        Assert.IsFalse(session.View.IsRestoringWorksheetView);
        session.ActivateWorksheet(second);
        Assert.AreSame(second, session.ActiveWorksheet);
    }

    [TestMethod]
    public void ActivationObserverFailureAfterSwitchShouldReleaseGuardWithoutFalseCompletion()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        var error = new IOException("activation observer");
        var completions = 0;
        EventHandler handler = (_, _) => throw error;
        session.ActiveWorksheetChanged += handler;
        session.View.WorksheetViewChanged += (_, _) => completions++;
        try
        {
            Assert.AreSame(error, Assert.ThrowsExactly<IOException>(() => session.ActivateWorksheet(second)));
        }
        finally { session.ActiveWorksheetChanged -= handler; }

        Assert.AreSame(second, session.ActiveWorksheet);
        Assert.AreEqual(0, completions);
        Assert.IsFalse(session.View.IsRestoringWorksheetView);
        session.ActivateWorksheet(first);
        Assert.AreEqual(1, completions);
    }

    [TestMethod]
    public void InvalidActiveCellShouldBeRejectedBeforeItCanEnterViewCache()
    {
        var session = new SpreadsheetSession(new Workbook());
        var before = session.View.WorksheetState;
        var invalid = new SelectionSnapshot(new CellAddress(9, 9), default,
            new[] { new CellRange(default, default) }, 0);

        Assert.ThrowsExactly<ArgumentException>(() => new SpreadsheetWorksheetViewState(invalid));

        Assert.AreEqual(before.Zoom, session.View.WorksheetState.Zoom);
        Assert.AreEqual(before.Selection.ActiveCell, session.Selection.ActiveCell);
        Assert.IsFalse(session.View.IsRestoringWorksheetView);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void InactiveViewportUpdateShouldKeepBothSelectionsAndPropagateSourceTag()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.Selection.SetActiveCell(new CellAddress(3, 3));
        var before = session.Selection.Capture();
        var source = new object();
        var events = 0;
        session.View.WorksheetViewChanged += (_, e) =>
        {
            Assert.AreSame(second, e.Worksheet);
            Assert.AreSame(source, e.Source);
            Assert.IsTrue(session.View.IsRestoringWorksheetView);
            Assert.IsFalse(session.View.SetWorksheetViewport(second, 0, 0, 1));
            events++;
        };

        Assert.IsTrue(session.View.SetWorksheetViewport(second, 88.5, 99.25, 1.75, source));

        Assert.AreSame(first, session.ActiveWorksheet);
        Assert.AreEqual(before.ActiveCell, session.Selection.ActiveCell);
        Assert.AreEqual(before.Version, session.Selection.Version);
        Assert.AreEqual(default(CellAddress), session.View.GetWorksheetState(second).Selection.ActiveCell);
        Assert.AreEqual(1, events);
        Assert.IsFalse(session.Undo());
    }
}
