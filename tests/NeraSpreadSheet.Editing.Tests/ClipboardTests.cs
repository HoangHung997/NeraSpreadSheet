using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardTests
{
    [TestMethod]
    public void FormulaReferenceTranslatorPreservesAbsoluteReferenceParts()
    {
        var translated = FormulaReferenceTranslator.Translate(
            "=A1+$A1+A$1+$A$1+\"A1\"",
            new CellAddress(0, 0),
            new CellAddress(2, 3));

        Assert.AreEqual("=D3+$A3+D$1+$A$1+\"A1\"", translated);
    }

    [TestMethod]
    public void PasteCopiesBlankCellsAndTranslatesRelativeFormula()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 10d);
        sheet.SetFormula(new CellAddress(0, 1), "=A1*2");
        sheet.SetValue(new CellAddress(4, 4), "must be cleared");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        var clipboard = new SpreadsheetClipboardController(session);
        clipboard.CopyPrimarySelection();

        Assert.IsTrue(clipboard.Paste(new CellAddress(3, 3)));

        Assert.AreEqual(10d, sheet.GetCell(new CellAddress(3, 3)).Value.RawValue);
        Assert.AreEqual("=D4*2", sheet.GetCell(new CellAddress(3, 4)).Formula);
        Assert.IsTrue(sheet.GetCell(new CellAddress(4, 4)).IsEmpty);
    }

    [TestMethod]
    public void PasteIsUndoable()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "source");
        var session = new SpreadsheetSession(workbook);
        var clipboard = new SpreadsheetClipboardController(session);
        clipboard.CopyPrimarySelection();
        clipboard.Paste(new CellAddress(3, 3));

        Assert.AreEqual("source", sheet.GetCell(new CellAddress(3, 3)).Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(sheet.GetCell(new CellAddress(3, 3)).IsEmpty);
    }

    [TestMethod]
    public async Task ClipboardCommandsUseNeraNativeCommandIds()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "source");
        var session = new SpreadsheetSession(workbook);
        var clipboard = new SpreadsheetClipboardController(session);
        var registry = new CommandRegistry();
        SpreadsheetClipboardCommandCatalog.Register(registry, clipboard);
        var dispatcher = new CommandDispatcher(registry);

        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Copy));
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Paste));
        Assert.AreEqual("source", workbook.Worksheets[0].GetCell(new CellAddress(2, 2)).Value.RawValue);
    }

    [TestMethod]
    public void OversizedClipboardRangeIsRejectedBeforeMaterialization()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(100, 100)));
        var clipboard = new SpreadsheetClipboardController(session, maximumMaterializedCells: 100);
        Assert.ThrowsExactly<InvalidOperationException>(() => clipboard.CopyPrimarySelection());
    }
}

[TestClass]
public sealed class ClipboardSynchronousCutTests
{
    [TestMethod]
    public void SynchronousCutShouldRejectReentrantCopyCutImportAndPaste()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var observed = false;
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
        {
            observed = true;
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            Assert.IsFalse(session.Clipboard.CanPaste);
            Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CopyPrimarySelection());
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.ImportTabSeparatedText("overwrite"));
            Assert.IsFalse(session.Clipboard.Paste(new CellAddress(3, 3)));
        };
        worksheet.CellsChanged += observer;
        try
        {
            Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        }
        finally
        {
            worksheet.CellsChanged -= observer;
        }

        Assert.IsTrue(observed);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
        AssertSourcePackage(session);
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.AreEqual("outside", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.IsTrue(worksheet.GetCell(new CellAddress(3, 3)).IsEmpty);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public async Task SynchronousCutShouldRejectReentrantAsynchronousWrite()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        Task<bool>? reentrant = null;
        var writes = 0;
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
            reentrant = session.Clipboard.CopyToClipboardAsync((_, _) =>
            {
                writes++;
                return ValueTask.CompletedTask;
            }).AsTask();
        worksheet.CellsChanged += observer;
        try
        {
            Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        }
        finally
        {
            worksheet.CellsChanged -= observer;
        }

        var reentrantWrite = reentrant ??
            throw new AssertFailedException("The source observer did not request the nested write.");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => reentrantWrite);
        Assert.AreEqual(0, writes);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        AssertSourcePackage(session);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(Acknowledge));
        AssertSourcePackage(session);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void SynchronousCutShouldRejectActiveEditorWithoutPublishingClipboard()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var editor = session.Editor.BeginEdit();
        var version = session.ActiveWorksheet.Version;
        var selectionVersion = session.Selection.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreSame(editor, session.Editor.State);
        Assert.IsTrue(session.Editor.IsEditing);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual(selectionVersion, session.Selection.Version);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public void SynchronousCutShouldRejectRemovedWorksheetBeforePublishingClipboard()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var remaining = session.Workbook.AddWorksheet("Remaining");
        remaining.SetValue(default, "keep remaining");
        session.Workbook.RemoveWorksheet(worksheet);
        var previous = session.Clipboard.Clipboard;
        var version = worksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreSame(worksheet, session.ActiveWorksheet);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("keep remaining", remaining.GetValue(default));
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public void SynchronousCutShouldReleaseBusyAfterMaterializationRejection()
    {
        var session = CreateSession();
        var clipboard = new SpreadsheetClipboardController(session, maximumMaterializedCells: 1);
        var previous = clipboard.ImportTabSeparatedText("previous");
        session.Selection.Select(new CellRange(default, new CellAddress(0, 1)));
        var version = session.ActiveWorksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() => clipboard.CutPrimarySelection());

        Assert.IsFalse(clipboard.IsClipboardWritePending);
        Assert.IsTrue(clipboard.CanPaste);
        Assert.AreSame(previous, clipboard.Clipboard);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
        session.Selection.SetActiveCell(default);
        Assert.AreEqual("original", clipboard.CopyPrimarySelection().GetCell(0, 0).Value.RawValue);
    }

    [TestMethod]
    public void SynchronousCutShouldReleaseBusyAfterSpillRejection()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        worksheet.SetFormula(default, "=SEQUENCE(2)");
        session.Recalculate();
        var previous = session.Clipboard.Clipboard;
        var version = worksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsFalse(session.Undo());
        session.Selection.Select(new CellRange(default, new CellAddress(1, 0)));
        Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void SynchronousCutObserverFailureShouldReleaseBusyAndKeepRecoveryPackage()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
        {
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            throw new IOException("Injected downstream observer failure.");
        };
        worksheet.CellsChanged += observer;
        try
        {
            Assert.ThrowsExactly<IOException>(() => session.Clipboard.CutPrimarySelection());
        }
        finally
        {
            worksheet.CellsChanged -= observer;
        }

        // Recovery-package retention is not proof of transactional rollback/history.
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(session.Clipboard.CanPaste);
        AssertSourcePackage(session);
        Assert.AreEqual("outside", worksheet.GetValue(new CellAddress(0, 2)));
    }

    [TestMethod]
    public void SynchronousEmptyCutShouldReleaseBusyWithoutAddingHistory()
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
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.IsFalse(session.Undo());
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public void SynchronousCutShouldKeepRecoveryPackageWhenObserverChangesWorksheet()
    {
        var session = CreateSession();
        var original = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        second.SetValue(default, "second sheet");
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
        {
            session.ActivateWorksheet(second);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CopyPrimarySelection());
        };
        original.CellsChanged += observer;
        try
        {
            Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        }
        finally
        {
            original.CellsChanged -= observer;
        }

        Assert.AreSame(second, session.ActiveWorksheet);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        AssertSourcePackage(session);
        Assert.AreEqual(original.Name, session.Clipboard.Clipboard!.SourceWorksheetName);
        Assert.AreEqual("second sheet", second.GetValue(default));
        Assert.IsTrue(original.GetCell(default).IsEmpty);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", original.GetValue(default));
        Assert.AreEqual("second sheet", second.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task SharedCutCommandShouldUseSynchronousBusyGuard()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var observed = false;
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
        {
            observed = true;
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.ImportTabSeparatedText("overwrite"));
        };
        worksheet.CellsChanged += observer;
        try
        {
            Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Cut));
        }
        finally
        {
            worksheet.CellsChanged -= observer;
        }

        Assert.IsTrue(observed);
        AssertSourcePackage(session);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void ActiveEditorCutRejectionShouldPreserveRedoHistory()
    {
        var session = CreateSession();
        var address = new CellAddress(5, 5);
        session.SetValue(address, "redo value");
        Assert.IsTrue(session.Undo());
        var previous = session.Clipboard.Clipboard;
        var editor = session.Editor.BeginEdit();

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreSame(editor, session.Editor.State);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("redo value", session.ActiveWorksheet.GetValue(address));
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Redo());
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task AsynchronousCutObserverFailureShouldReleaseBusyAndKeepAcknowledgedPackage()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        EventHandler<CellsChangedEventArgs> observer = (_, _) =>
        {
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
            throw new IOException("Injected downstream observer failure.");
        };
        worksheet.CellsChanged += observer;
        try
        {
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                session.Clipboard.CopyToClipboardAsync(Acknowledge, cut: true).AsTask());
        }
        finally
        {
            worksheet.CellsChanged -= observer;
        }

        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(session.Clipboard.CanPaste);
        AssertSourcePackage(session);
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

    private static void AssertSourcePackage(SpreadsheetSession session)
    {
        var package = session.Clipboard.Clipboard;
        Assert.IsNotNull(package);
        Assert.AreEqual("original", package.GetCell(0, 0).Value.RawValue);
    }

    private static void AssertUnchanged(SpreadsheetSession session, SpreadsheetClipboardPackage? previous)
    {
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual("outside", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Undo());
        Assert.IsFalse(session.Redo());
    }

    private static ValueTask Acknowledge(SpreadsheetClipboardPackage package, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.IsNotNull(package);
        return ValueTask.CompletedTask;
    }
}


[TestClass]
public sealed class ClipboardCutAuthorizationTests
{
    [TestMethod]
    public void DeniedSynchronousCutShouldPreserveSourceClipboardAndHistory()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var version = session.ActiveWorksheet.Version;
        var calls = 0;
        session.Clipboard.CutAuthorization = (worksheet, range) =>
        {
            calls++;
            Assert.AreSame(session.ActiveWorksheet, worksheet);
            Assert.AreEqual(new CellRange(default, default), range);
            return false;
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreEqual(1, calls);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task DeniedAsynchronousCutShouldNotInvokeTransport()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writes = 0;
        session.Clipboard.CutAuthorization = (_, _) => false;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await session.Clipboard.CopyToClipboardAsync((_, _) =>
            {
                writes++;
                return ValueTask.CompletedTask;
            }, cut: true));

        Assert.AreEqual(0, writes);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task RevokedCutPermissionDuringWriteShouldPreserveSourceAndOldPackage()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var allowed = true;
        var queries = 0;
        session.Clipboard.CutAuthorization = (_, _) => { queries++; return allowed; };
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        Assert.AreEqual(1, writer.Calls);
        Assert.AreEqual(1, queries);
        allowed = false;
        writer.Release();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pending);

        Assert.AreEqual(2, queries);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task ReplacingPolicyThroughAnotherControllerShouldInvalidatePendingCut()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        session.Clipboard.CutAuthorization = (_, _) => true;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        var secondary = new SpreadsheetClipboardController(session);
        secondary.CutAuthorization = (_, _) => false;
        Assert.AreSame(secondary.CutAuthorization, session.Clipboard.CutAuthorization);
        writer.Release();

        Assert.IsFalse(await pending);

        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task RemovingPolicyWhilePendingShouldNotAuthorizeAnOldCut()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        session.Clipboard.CutAuthorization = (_, _) => true;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        session.Clipboard.CutAuthorization = null;
        writer.Release();

        Assert.IsFalse(await pending);

        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task AuthorizationFailureAfterAcknowledgementShouldNotPublishOrClear()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var calls = 0;
        session.Clipboard.CutAuthorization = (_, _) =>
            ++calls == 1 ? true : throw new InvalidOperationException("Policy unavailable");
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        writer.Release();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pending);

        Assert.AreEqual("Policy unavailable", exception.Message);
        Assert.AreEqual(2, calls);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task AuthorizationFailureBeforeTransportShouldReleaseTheSessionGate()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writes = 0;
        session.Clipboard.CutAuthorization = (_, _) =>
            throw new InvalidOperationException("Policy unavailable");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await session.Clipboard.CopyToClipboardAsync((_, _) =>
            {
                writes++;
                return ValueTask.CompletedTask;
            }, cut: true));

        Assert.AreEqual(0, writes);
        AssertUnchanged(session, previous);
        session.Clipboard.CutAuthorization = null;
        Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task AcknowledgedCutShouldRecheckItsLeaseAfterAuthorizationCallback()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var queries = 0;
        session.Clipboard.CutAuthorization = (worksheet, _) =>
        {
            if (++queries == 2)
            {
                worksheet.SetValue(default, "new value from policy callback");
            }
            return true;
        };
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        writer.Release();

        Assert.IsFalse(await pending);

        Assert.AreEqual("new value from policy callback", session.ActiveWorksheet.GetValue(default));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void SynchronousAuthorizationShouldNotRetargetCutAfterSheetRoundTrip()
    {
        var session = CreateSession();
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        var previous = session.Clipboard.Clipboard;
        session.Clipboard.CutAuthorization = (_, _) =>
        {
            session.ActivateWorksheet(second);
            session.ActivateWorksheet(first);
            return true;
        };

        Assert.IsFalse(session.Clipboard.CutPrimarySelection());

        Assert.AreSame(first, session.ActiveWorksheet);
        AssertUnchanged(session, previous);
        Assert.IsTrue(second.GetCell(default).IsEmpty);
    }

    [TestMethod]
    public void SynchronousCutShouldRecheckPermissionBeforePublishingPackage()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var queries = 0;
        session.Clipboard.CutAuthorization = (_, _) => ++queries == 1;

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreEqual(2, queries);
        AssertUnchanged(session, previous);
    }

    [TestMethod]
    public async Task AllowedCutShouldCheckExactRangeTwiceAndKeepSingleUndoRedo()
    {
        var session = CreateSession();
        var worksheet = session.ActiveWorksheet;
        var range = new CellRange(default, new CellAddress(0, 1));
        session.Selection.Select(range);
        var queries = 0;
        session.Clipboard.CutAuthorization = (source, selected) =>
        {
            queries++;
            Assert.AreSame(worksheet, source);
            Assert.AreEqual(range, selected);
            Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
            return true;
        };

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(Acknowledge, cut: true));

        Assert.AreEqual(2, queries);
        var package = session.Clipboard.Clipboard;
        Assert.IsNotNull(package);
        Assert.AreEqual(range, package.SourceRange);
        Assert.AreEqual("original", package.GetCell(0, 0).Value.RawValue);
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.AreEqual("outside", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", worksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(worksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public async Task CopyShouldRemainAvailableWhenCutPolicyDeniesMutation()
    {
        var session = CreateSession();
        var queries = 0;
        session.Clipboard.CutAuthorization = (_, _) => { queries++; return false; };

        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync(Acknowledge));
        session.Clipboard.CopyPrimarySelection();

        Assert.AreEqual(0, queries);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void AnotherControllerShouldNotBypassTheSessionCutPolicy()
    {
        var session = CreateSession();
        var secondary = new SpreadsheetClipboardController(session);
        var previous = secondary.ImportTabSeparatedText("secondary package");
        session.Clipboard.CutAuthorization = (_, _) => false;

        Assert.ThrowsExactly<InvalidOperationException>(() => secondary.CutPrimarySelection());

        Assert.AreSame(previous, secondary.Clipboard);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task AllControllersInSessionShouldRejectOverlappingClipboardOperations()
    {
        var session = CreateSession();
        var secondary = new SpreadsheetClipboardController(session);
        var previous = secondary.ImportTabSeparatedText("secondary package");
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync).AsTask();
        var createdWhilePending = new SpreadsheetClipboardController(session);
        try
        {
            Assert.IsTrue(secondary.IsClipboardWritePending);
            Assert.IsTrue(createdWhilePending.IsClipboardWritePending);
            Assert.IsFalse(secondary.CanPaste);
            Assert.ThrowsExactly<InvalidOperationException>(() => secondary.CopyPrimarySelection());
            Assert.ThrowsExactly<InvalidOperationException>(() => secondary.CutPrimarySelection());
            Assert.ThrowsExactly<InvalidOperationException>(() => secondary.ImportTabSeparatedText("wrong"));
            Assert.IsFalse(secondary.Paste(new CellAddress(3, 3)));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await createdWhilePending.CopyToClipboardAsync(Acknowledge));
        }
        finally
        {
            writer.Release();
            await pending;
        }

        Assert.AreSame(previous, secondary.Clipboard);
        Assert.IsFalse(secondary.IsClipboardWritePending);
        Assert.IsTrue(secondary.CanPaste);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsTrue(session.ActiveWorksheet.GetCell(new CellAddress(3, 3)).IsEmpty);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task CancellationThroughAnotherControllerShouldKeepGateUntilWriterSettles()
    {
        var session = CreateSession();
        var previous = session.Clipboard.Clipboard;
        var writer = new DeferredWriter();
        var pending = session.Clipboard.CopyToClipboardAsync(writer.WriteAsync, cut: true).AsTask();
        var secondary = new SpreadsheetClipboardController(session);
        try
        {
            Assert.IsTrue(secondary.CancelPendingClipboardWrite());
            Assert.IsFalse(session.Clipboard.CancelPendingClipboardWrite());
            Assert.IsTrue(secondary.IsClipboardWritePending);
            Assert.IsFalse(pending.IsCompleted);
            Assert.ThrowsExactly<InvalidOperationException>(() => secondary.CopyPrimarySelection());
        }
        finally
        {
            writer.Release();
        }

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => pending);

        AssertUnchanged(session, previous);
        Assert.IsFalse(secondary.IsClipboardWritePending);
    }

    [TestMethod]
    public void AuthorizationQueryShouldNotBypassBusyByCreatingAnotherController()
    {
        var session = CreateSession();
        var checks = 0;
        session.Clipboard.CutAuthorization = (_, _) =>
        {
            var nested = new SpreadsheetClipboardController(session);
            checks++;
            Assert.IsTrue(nested.IsClipboardWritePending);
            Assert.ThrowsExactly<InvalidOperationException>(() => nested.CopyPrimarySelection());
            return true;
        };

        Assert.IsTrue(session.Clipboard.CutPrimarySelection());

        Assert.AreEqual(2, checks);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task SeparateSessionsShouldNotSharePermissionOrBusyState()
    {
        var first = CreateSession();
        var second = CreateSession();
        first.Clipboard.CutAuthorization = (_, _) => false;
        var writer = new DeferredWriter();
        var pending = first.Clipboard.CopyToClipboardAsync(writer.WriteAsync).AsTask();
        try
        {
            Assert.IsNull(second.Clipboard.CutAuthorization);
            Assert.IsFalse(second.Clipboard.IsClipboardWritePending);
            Assert.IsTrue(second.Clipboard.CutPrimarySelection());
        }
        finally
        {
            writer.Release();
            await pending;
        }

        Assert.AreEqual("original", first.ActiveWorksheet.GetValue(default));
        Assert.IsTrue(second.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(first.Undo());
        Assert.IsTrue(second.Undo());
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "original");
        worksheet.SetValue(new CellAddress(0, 2), "outside");
        var session = new SpreadsheetSession(workbook);
        session.Clipboard.ImportTabSeparatedText("previous package");
        return session;
    }

    private static void AssertUnchanged(
        SpreadsheetSession session,
        SpreadsheetClipboardPackage? previous)
    {
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual("outside", session.ActiveWorksheet.GetValue(new CellAddress(0, 2)));
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Undo());
        Assert.IsFalse(session.Redo());
    }

    private static ValueTask Acknowledge(
        SpreadsheetClipboardPackage package,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    private sealed class DeferredWriter
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public async ValueTask WriteAsync(
            SpreadsheetClipboardPackage package,
            CancellationToken cancellationToken)
        {
            Calls++;
            // Deliberately ignore cancellation until release: the session must remain
            // busy until the actual native transport has settled, not just the request.
            await _completion.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Release() => _completion.TrySetResult();
    }
}
