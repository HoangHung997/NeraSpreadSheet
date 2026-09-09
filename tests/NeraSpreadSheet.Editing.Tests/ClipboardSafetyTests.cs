using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardSafetyTests
{
    [TestMethod]
    public void CutShouldRejectDisjointRangesWithoutChangingDataOrHistory()
    {
        var session = CreateSession();
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.IsFalse(session.Undo());
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public void CutShouldRejectDisjointRangesRegardlessOfSelectionOrder()
    {
        var session = CreateSession();
        session.Selection.SetActiveCell(new CellAddress(0, 2));
        session.Selection.AddRange(new CellRange(default, default));

        AssertCutRejectedWithoutMutation(session);

        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CutShouldRejectEmptyPrimaryRangeWithPopulatedAdditionalRange()
    {
        var session = CreateSession();
        var empty = new CellAddress(2, 0);
        session.Selection.SetActiveCell(empty);
        session.Selection.AddRange(new CellRange(default, default));

        AssertCutRejectedWithoutMutation(session);

        Assert.AreEqual("A1 source", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void RejectedCutShouldPreservePreviouslyCopiedPackage()
    {
        var session = CreateSession();
        var previous = session.Clipboard.CopyPrimarySelection();
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual("A1 source", previous.GetCell(0, 0).Value.RawValue);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void RejectedCutShouldPreserveImportedClipboardAndItsPasteBehavior()
    {
        var session = CreateSession();
        var previous = session.Clipboard.ImportTabSeparatedText("external clipboard");
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.AreSame(previous, session.Clipboard.Clipboard);
        var destination = new CellAddress(4, 4);
        Assert.IsTrue(session.Clipboard.Paste(destination));
        Assert.AreEqual("external clipboard", session.ActiveWorksheet.GetValue(destination));
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Undo());
        Assert.AreEqual("A1 source", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual("C1 source", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
    }

    [TestMethod]
    public void RejectedCutShouldPreserveExistingRedoEntry()
    {
        var session = CreateSession();
        var address = new CellAddress(4, 4);
        session.SetValue(address, "history value");
        Assert.IsTrue(session.Undo());
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual("history value", session.ActiveWorksheet.GetValue(address));
        Assert.IsFalse(session.Redo());
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void RejectedCutShouldPreserveExistingUndoEntry()
    {
        var session = CreateSession();
        var address = new CellAddress(4, 4);
        session.SetValue(address, "history value");
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.ActiveWorksheet.GetCell(address).IsEmpty);
        Assert.AreEqual("A1 source", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual("C1 source", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CutShouldRejectOverlappingRangesInsteadOfGuessingACombinedPackage()
    {
        var session = CreateSession();
        session.Selection.Select(new CellRange(default, new CellAddress(0, 2)));
        session.Selection.AddRange(new CellRange(new CellAddress(0, 1), new CellAddress(0, 3)));

        AssertCutRejectedWithoutMutation(session);

        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CutShouldRejectAdjacentRangesInsteadOfSilentlyDroppingOne()
    {
        var session = CreateSession();
        session.Selection.SetActiveCell(default);
        session.Selection.AddRange(new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)));

        AssertCutRejectedWithoutMutation(session);

        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void RejectedCutShouldPreserveSpillInAnAdditionalRange()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var owner = new CellAddress(2, 2);
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        session.Recalculate();
        session.Selection.SetActiveCell(default);
        session.Selection.AddRange(new CellRange(owner, new CellAddress(3, 3)));

        AssertCutRejectedWithoutMutation(session);

        Assert.AreEqual("=SEQUENCE(2,2)", worksheet.GetFormula(owner));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(3, 3)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void RejectedCutShouldPreserveFormulasAndStylesInAllRanges()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var styleId = session.Workbook.Styles.Intern(new CellStyle
        {
            Alignment = new CellAlignmentStyle { WrapText = true },
        });
        worksheet.SetFormula(default, "=2+3");
        worksheet.SetStyle(new CellAddress(0, 2), styleId);
        session.Recalculate();
        SelectDisjointRanges(session);

        AssertCutRejectedWithoutMutation(session);

        Assert.AreEqual("=2+3", worksheet.GetFormula(default));
        Assert.AreEqual(styleId, worksheet.GetCell(new CellAddress(0, 2)).StyleId);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void SingleRangeCutShouldCopyOnlyItsRangeAndPreserveUndoRedo()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var range = new CellRange(default, new CellAddress(0, 1));
        session.Selection.Select(range);

        Assert.IsTrue(session.Clipboard.CutPrimarySelection());

        var package = session.Clipboard.Clipboard;
        Assert.IsNotNull(package);
        Assert.AreEqual(range, package.SourceRange);
        Assert.AreEqual("A1 source", package.GetCell(0, 0).Value.RawValue);
        Assert.AreEqual("B1 outside", package.GetCell(0, 1).Value.RawValue);
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsTrue(worksheet.GetCell(new CellAddress(0, 1)).IsEmpty);
        Assert.AreEqual("C1 source", worksheet.GetValue(new CellAddress(0, 2)));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("A1 source", worksheet.GetValue(default));
        Assert.AreEqual("B1 outside", worksheet.GetValue(new CellAddress(0, 1)));
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsTrue(worksheet.GetCell(new CellAddress(0, 1)).IsEmpty);
        Assert.AreEqual("C1 source", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public void SingleEmptyRangeCutShouldKeepExistingNoHistoryBehavior()
    {
        var session = CreateSession();
        var empty = new CellAddress(4, 4);
        session.Selection.SetActiveCell(empty);
        var version = session.ActiveWorksheet.Version;

        Assert.IsFalse(session.Clipboard.CutPrimarySelection());

        var package = session.Clipboard.Clipboard;
        Assert.IsNotNull(package);
        Assert.AreEqual(new CellRange(empty, empty), package.SourceRange);
        Assert.AreEqual(0, package.UsedCellCount);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void PartialSpillCutShouldPreserveOldClipboardAndSource()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var owner = new CellAddress(2, 2);
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        session.Recalculate();
        var previous = session.Clipboard.ImportTabSeparatedText("previous clipboard");
        session.Selection.Select(new CellRange(owner, new CellAddress(2, 3)));

        AssertCutRejectedWithoutMutation(session);

        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(3, 3)));
        Assert.IsFalse(session.Undo());
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "A1 source");
        worksheet.SetValue(new CellAddress(0, 1), "B1 outside");
        worksheet.SetValue(new CellAddress(0, 2), "C1 source");
        return new SpreadsheetSession(workbook);
    }

    private static void SelectDisjointRanges(SpreadsheetSession session)
    {
        session.Selection.SetActiveCell(default);
        session.Selection.AddRange(new CellRange(new CellAddress(0, 2), new CellAddress(0, 2)));
    }

    private static void AssertCutRejectedWithoutMutation(SpreadsheetSession session)
    {
        var worksheet = session.ActiveWorksheet;
        var cells = worksheet.EnumerateUsedCells().ToArray();
        var version = worksheet.Version;
        var selection = session.Selection.Capture();
        var clipboard = session.Clipboard.Clipboard;
        var canPaste = session.Clipboard.CanPaste;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Clipboard.CutPrimarySelection());

        Assert.AreSame(worksheet, session.ActiveWorksheet);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual(cells.Length, worksheet.UsedCellCount);
        foreach (var pair in cells)
        {
            Assert.AreEqual(pair.Value, worksheet.GetCell(pair.Key));
        }
        Assert.AreEqual(selection.Version, session.Selection.Version);
        Assert.AreEqual(selection.ActiveCell, session.Selection.ActiveCell);
        Assert.AreEqual(selection.AnchorCell, session.Selection.AnchorCell);
        Assert.AreEqual(selection.Ranges.Count, session.Selection.Ranges.Count);
        for (var index = 0; index < selection.Ranges.Count; index++)
        {
            Assert.AreEqual(selection.Ranges[index], session.Selection.Ranges[index]);
        }
        if (clipboard is null)
        {
            Assert.IsNull(session.Clipboard.Clipboard);
        }
        else
        {
            Assert.AreSame(clipboard, session.Clipboard.Clipboard);
        }
        Assert.AreEqual(canPaste, session.Clipboard.CanPaste);
    }
}

[TestClass]
public sealed class ClipboardAcknowledgementTests
{
    [TestMethod]
    public async Task PendingCutShouldKeepSourceAndOldClipboardUntilAcknowledged()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var previous = session.Clipboard.Clipboard;
        var version = worksheet.Version;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();

        Assert.AreEqual(1, writer.Calls);
        Assert.IsFalse(pending.IsCompleted);
        Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.AreEqual(version, worksheet.Version);
        Assert.IsFalse(session.Undo());

        writer.Acknowledge();
        Assert.IsTrue(await pending);
        Assert.AreSame(writer.Package, session.Clipboard.Clipboard);
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.AreEqual("outside", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public async Task FailedWriteShouldPreserveSourceOldClipboardAndHistory()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var version = session.ActiveWorksheet.Version;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        writer.Fail(new IOException("Clipboard unavailable"));

        await Assert.ThrowsExactlyAsync<IOException>(() => pending);

        AssertUnchanged(session, previous);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
    }

    [TestMethod]
    public async Task SynchronousTransportFailureShouldReleasePendingState()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;

        await Assert.ThrowsExactlyAsync<IOException>(() => session.Clipboard.CopyToClipboardAsync(
            static (_, _) => throw new IOException("Transport rejected write"), cut: true).AsTask());

        AssertUnchanged(session, previous);
        Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately, cut: true));
    }

    [TestMethod]
    public async Task FailedWriteShouldKeepAnInitiallyEmptyClipboardEmpty()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "original");
        var session = new SpreadsheetSession(workbook);

        await Assert.ThrowsExactlyAsync<IOException>(() => session.Clipboard.CopyToClipboardAsync(
            static (_, _) => throw new IOException("No clipboard"), cut: true).AsTask());

        Assert.IsNull(session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task PreCanceledWriteShouldNotCallTransport()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var writer = new DeferredWriter();
        writer.Acknowledge();

        var pending = session.Clipboard.CopyToClipboardAsync(
            writer.WriteAsync, cut: true, cancellation.Token).AsTask();
        await AssertCanceledAsync(pending);

        Assert.AreEqual(0, writer.Calls);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task CancellationShouldProtectSourceEvenWhenTransportIgnoresToken()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        using var cancellation = new CancellationTokenSource();
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(
            writer.WriteAsync, cut: true, cancellation.Token).AsTask();
        cancellation.Cancel();

        Assert.IsTrue(writer.Token.IsCancellationRequested);
        Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(pending.IsCompleted);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        writer.Acknowledge();
        await AssertCanceledAsync(pending);

        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task ExplicitCancelShouldBeIdempotentAndKeepBusyUntilTransportSettles()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();

        Assert.IsTrue(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(writer.Token.IsCancellationRequested);
        Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CopyPrimarySelection());
        writer.Acknowledge();
        await AssertCanceledAsync(pending);

        AssertUnchanged(session, previous);
        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately, cut: true));
    }

    [TestMethod]
    public async Task PendingWriteShouldRejectAnotherWriterWithoutInvokingIt()
    {
        var session = CreateSession();
        var first = new DeferredWriter();
        var second = new DeferredWriter();
        second.Acknowledge();
        var pending = session.Clipboard.CopyToClipboardAsync(first.WriteAsync, cut: true).AsTask();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(second.WriteAsync, cut: true).AsTask());

        Assert.AreEqual(0, second.Calls);
        Assert.AreEqual(1, first.Calls);
        first.Acknowledge();
        Assert.IsTrue(await pending);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task PendingWriteShouldBlockLegacyClipboardMutations()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CopyPrimarySelection());
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.ImportTabSeparatedText("replacement"));
        Assert.IsFalse(session.Clipboard.PasteAtActiveCell());
        Assert.IsFalse(session.Clipboard.Paste(new CellAddress(4, 4)));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsTrue(session.ActiveWorksheet.GetCell(new CellAddress(4, 4)).IsEmpty);

        session.Clipboard.CancelPendingClipboardWrite();
        writer.Acknowledge();
        await AssertCanceledAsync(pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldNotClearContentEditedByAnotherSession()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var worksheet = session.ActiveWorksheet;
        var other = new SpreadsheetSession(session.Workbook, worksheet);
        var workbookVersion = session.Workbook.Version;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        other.SetValue(default, "new content");
        Assert.AreEqual(workbookVersion, session.Workbook.Version);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        Assert.AreEqual("new content", worksheet.GetValue(default));
        Assert.AreEqual("original", writer.Package!.GetCell(0, 0).Value.RawValue);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(other.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectDirectWorksheetMutation()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.ActiveWorksheet.SetValue(default, "direct edit");
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        Assert.AreEqual("direct edit", session.ActiveWorksheet.GetValue(default));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task DelayedCutShouldRemainStaleAfterEditIsUndone()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var other = new SpreadsheetSession(session.Workbook, session.ActiveWorksheet);
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        other.SetValue(default, "temporary edit");
        Assert.IsTrue(other.Undo());
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectSelectionChangeAndReturnToOriginalCell()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.Selection.SetActiveCell(new CellAddress(0, 2));
        session.Selection.SetActiveCell(default);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldNotClearAnAdditionalSelectionRange()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.Selection.AddRange(new CellRange(new CellAddress(0, 2), new CellAddress(0, 2)));
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        Assert.AreEqual("outside", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectSheetSwitchAndReturnWithUnchangedSelection()
    {
        var session = CreateSession();
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        var previous = session.Clipboard.Clipboard;
        var selectionVersion = session.Selection.Version;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.ActivateWorksheet(second);
        session.ActivateWorksheet(first);
        Assert.AreEqual(selectionVersion, session.Selection.Version);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
        Assert.IsTrue(second.GetCell(default).IsEmpty);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectDirectWorksheetRename()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.ActiveWorksheet.Rename("Renamed");
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectWorkbookStructureChange()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        var temporary = session.Workbook.AddWorksheet("Temporary");
        session.Workbook.RemoveWorksheet(temporary);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldNotClearARemovedSourceWorksheet()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var survivor = session.Workbook.AddWorksheet("Survivor");
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.Workbook.RemoveWorksheet(worksheet);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsTrue(survivor.GetCell(default).IsEmpty);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectDimensionChange()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.ActiveWorksheet.Dimensions.SetRowHeight(0, 35.5d);
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectViewChangeAndRestore()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.View.FreezeTopRows(1);
        session.View.Unfreeze();
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCutShouldRejectAnEditorSessionEvenAfterCancel()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.Editor.BeginEdit();
        session.Editor.Cancel();
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DelayedCopyShouldNotPublishAStalePackage()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync).AsTask();
        session.ActiveWorksheet.SetValue(default, "new content");
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        Assert.AreEqual("new content", session.ActiveWorksheet.GetValue(default));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void SynchronousCutShouldRejectPartialMergeBeforeReplacingClipboard()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var merged = new CellRange(default, new CellAddress(0, 1));
        worksheet.MergeCells(merged);
        var previous = session.Clipboard.Clipboard;
        var version = worksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        AssertUnchanged(session, previous);
        Assert.AreEqual(version, worksheet.Version);
        Assert.IsTrue(worksheet.MergedCells.Ranges.Contains(merged));
    }

    [TestMethod]
    public async Task PartialMergeCutShouldNotInvokeTransport()
    {
        var session = CreateSession();
        session.ActiveWorksheet.MergeCells(new CellRange(default, new CellAddress(0, 1)));
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        writer.Acknowledge();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask());

        Assert.AreEqual(0, writer.Calls);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task CompleteMergedRangeCutShouldRetainMergeAndUndoAnchorValue()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var range = new CellRange(default, new CellAddress(0, 1));
        worksheet.MergeCells(range);
        session.Selection.Select(range);

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately, cut: true));

        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsTrue(worksheet.MergedCells.Ranges.Contains(range));
        Assert.AreEqual(range, session.Clipboard.Clipboard!.SourceRange);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsTrue(worksheet.MergedCells.Ranges.Contains(range));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task DelayedCutShouldDetectDirectMergedCollectionChange()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.ActiveWorksheet.MergedCells.Add(new CellRange(new CellAddress(4, 4), new CellAddress(5, 5)));
        writer.Acknowledge();

        Assert.IsFalse(await pending);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task PartialSpillCutShouldNotInvokeTransport()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        worksheet.SetFormula(default, "=SEQUENCE(2,2)");
        session.Recalculate();
        session.Selection.Select(new CellRange(default, new CellAddress(0, 1)));
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        writer.Acknowledge();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask());

        Assert.AreEqual(0, writer.Calls);
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task AcknowledgedSpillCutShouldClearAndUndoDerivedCells()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        worksheet.SetFormula(default, "=SEQUENCE(3)");
        session.Recalculate();
        session.Selection.Select(new CellRange(default, new CellAddress(2, 0)));
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 0)));
        writer.Acknowledge();

        Assert.IsTrue(await pending);
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("=SEQUENCE(3)", worksheet.GetFormula(default));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 0)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task EmptyAcknowledgedCutShouldRetainNoHistoryReturnValue()
    {
        var session = CreateSession();
        session.Selection.SetActiveCell(new CellAddress(4, 4));
        var version = session.ActiveWorksheet.Version;
        var writer = new DeferredWriter();
        writer.Acknowledge();

        Assert.IsFalse(await session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true));

        Assert.AreEqual(1, writer.Calls);
        Assert.AreSame(writer.Package, session.Clipboard.Clipboard);
        Assert.AreEqual(0, writer.Package!.UsedCellCount);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task AcknowledgedCopyShouldPreserveFormulaStyleAndPasteTranslation()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        worksheet.SetValue(new CellAddress(0, 2), 7d);
        worksheet.SetFormula(default, "=C1*2");
        var style = session.Workbook.Styles.Intern(new CellStyle
        {
            Alignment = new CellAlignmentStyle { WrapText = true },
        });
        worksheet.SetStyle(default, style);
        session.Recalculate();
        var version = worksheet.Version;

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately));

        Assert.AreEqual(version, worksheet.Version);
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(1, 0)));
        Assert.AreEqual("=C2*2", worksheet.GetFormula(new CellAddress(1, 0)));
        Assert.AreEqual(style, worksheet.GetCell(new CellAddress(1, 0)).StyleId);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(worksheet.GetCell(new CellAddress(1, 0)).IsEmpty);
    }

    [TestMethod]
    public async Task MultiRangeWriteShouldRejectBeforeCallingTransport()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        session.Selection.AddRange(new CellRange(new CellAddress(0, 2), new CellAddress(0, 2)));
        var writer = new DeferredWriter();
        writer.Acknowledge();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(writer.WriteAsync).AsTask());

        Assert.AreEqual(0, writer.Calls);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task ActiveEditorWriteShouldRejectWithoutCancelingEditor()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        session.Editor.BeginEdit();
        var writer = new DeferredWriter();
        writer.Acknowledge();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask());

        Assert.AreEqual(0, writer.Calls);
        Assert.IsTrue(session.Editor.IsEditing);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task NullTransportShouldRejectWithoutChangingState()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            session.Clipboard.CopyToClipboardAsync(null!, cut: true).AsTask());

        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task CutMutationCallbacksShouldNotStartAnotherClipboardOperation()
    {
        var session = CreateSession();
        var observed = false;
        session.ActiveWorksheet.CellsChanged += (_, _) =>
        {
            observed = true;
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            Assert.IsFalse(session.Clipboard.CanPaste);
            Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CopyPrimarySelection());
        };

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately, cut: true));

        Assert.IsTrue(observed);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
    }

    [TestMethod]
    public async Task SuccessfulWriteAfterStaleWriteShouldNotInheritInvalidation()
    {
        var session = CreateSession();
        var first = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(first.WriteAsync, cut: true).AsTask();
        session.Editor.BeginEdit();
        session.Editor.Cancel();
        first.Acknowledge();
        Assert.IsFalse(await pending);

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(AcknowledgeImmediately, cut: true));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "original");
        workbook.Worksheets[0].SetValue(new CellAddress(0, 2), "outside");
        var session = new SpreadsheetSession(workbook);
        session.Clipboard.ImportTabSeparatedText("previous clipboard");
        return session;
    }

    private static void AssertUnchanged(SpreadsheetSession session, SpreadsheetClipboardPackage? previous)
    {
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual("outside", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Undo());
    }

    private static ValueTask AcknowledgeImmediately(SpreadsheetClipboardPackage package, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.IsNotNull(package);
        return ValueTask.CompletedTask;
    }

    private static async Task AssertCanceledAsync(Task<bool> operation)
    {
        try
        {
            await operation;
            Assert.Fail("The clipboard operation must propagate cancellation.");
        }
        catch (OperationCanceledException)
        {
            // Both OperationCanceledException and its TaskCanceledException subtype are valid.
        }
    }

    private sealed class DeferredWriter
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public SpreadsheetClipboardPackage? Package { get; private set; }
        public CancellationToken Token { get; private set; }

        public ValueTask WriteAsync(SpreadsheetClipboardPackage package, CancellationToken token)
        {
            Calls++;
            Package = package;
            Token = token;
            // Deliberately uncooperative: cancellation cannot make a late write safe by itself.
            return new ValueTask(_completion.Task);
        }

        public void Acknowledge() => _completion.SetResult();
        public void Fail(Exception exception) => _completion.SetException(exception);
    }
}
