using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardOwnershipRegressionTests
{
    [TestMethod]
    public async Task ObservedExternalDataShouldRevokeOldLeaseEvenWhenPasteIsDenied()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var old = session.Clipboard.Clipboard;
        var version = session.ActiveWorksheet.Version;
        session.Clipboard.PasteAuthorization = (_, _, _) => false;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await session.Clipboard.PasteFromClipboardAsync(_ => Read("external")));

        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.IsFalse(session.Clipboard.State.IsCopyMode);
        Assert.IsFalse(session.Clipboard.Paste(new CellAddress(4, 4)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task SuccessfulReadWithoutTextShouldNotEnableOldPrivateFallback()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var old = session.Clipboard.Clipboard;
        var version = session.ActiveWorksheet.Version;

        Assert.IsFalse(await session.Clipboard.PasteFromClipboardAsync(_ =>
            ValueTask.FromResult<SpreadsheetClipboardReadResult?>(null)));

        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.AreEqual(SpreadsheetClipboardStatus.Stale, session.Clipboard.State.Status);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task ExternalFormulaWithoutCacheShouldRevokeOldLeaseBeforeValuesRejects()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var old = session.Clipboard.Clipboard;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await session.Clipboard.PasteFromClipboardAsync(_ => Read("=9+1"), SpreadsheetClipboardPasteMode.Values));

        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task SuccessfulExternalPasteShouldNotClaimOsWriteOwnership()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);

        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ => Read("external")));

        Assert.AreEqual("external", session.ActiveWorksheet.GetValue(default));
        Assert.AreEqual(SpreadsheetClipboardOperation.External, session.Clipboard.State.Operation);
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.IsFalse(session.Clipboard.State.IsCopyMode);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task ActualExternalReadShouldInvalidateOtherControllersPrivateLeases()
    {
        var session = CreateSession();
        var other = new SpreadsheetClipboardController(session);
        var otherPackage = other.CopyPrimarySelection();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);

        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ => Read("replacement")));

        Assert.AreSame(otherPackage, other.Clipboard);
        Assert.IsFalse(other.CanPaste);
        Assert.IsFalse(other.State.IsCopyMode);
        Assert.IsFalse(other.Paste(new CellAddress(8, 8)));
        Assert.IsTrue(session.ActiveWorksheet.GetCell(new CellAddress(8, 8)).IsEmpty);
    }

    [TestMethod]
    public async Task LaterControllerWriteShouldRetireEarlierOsOwnershipWithoutDeletingRecoveryPackage()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var original = session.Clipboard.Clipboard;
        var other = new SpreadsheetClipboardController(session);

        Assert.IsTrue(await other.CopyToClipboardAsync(Acknowledge));

        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.IsTrue(other.State.HasOsOwnership);
        Assert.AreSame(original, session.Clipboard.Clipboard);
        Assert.AreNotSame(original, other.Clipboard);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task FailedWriteAttemptShouldRetireStampButKeepSourceAndRecoveryPackage()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var original = session.Clipboard.Clipboard;
        var version = session.ActiveWorksheet.Version;
        var transportError = new IOException("Write may have changed OS data before flush failed.");

        var caught = await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await session.Clipboard.CopyToClipboardAsync((_, _) => throw transportError, cut: true));

        Assert.AreSame(transportError, caught);
        Assert.AreSame(original, session.Clipboard.Clipboard);
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task RetiredStampShouldNotTriggerNativeFormulaTranslationOnLaterRead()
    {
        var session = CreateSession();
        session.ActiveWorksheet.SetFormula(default, "=B1");
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var original = session.Clipboard.Clipboard!;
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await session.Clipboard.CopyToClipboardAsync((_, _) => throw new IOException("flush failed")));
        var destination = new CellAddress(3, 3);
        session.Selection.SetActiveCell(destination);

        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ =>
            ValueTask.FromResult<SpreadsheetClipboardReadResult?>(new(original.ToTabSeparatedText(), original.PayloadId))));

        Assert.AreEqual("=B1", session.ActiveWorksheet.GetFormula(destination));
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
    }

    [TestMethod]
    public async Task ReadFailureBeforeAnyObservationShouldPreserveExistingPayload()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var old = session.Clipboard.Clipboard;

        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await session.Clipboard.PasteFromClipboardAsync(_ => throw new IOException("read unavailable")));

        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.IsTrue(session.Clipboard.State.HasOsOwnership);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task StaleAcknowledgementShouldNotLeaveOldStampClaimingOwnership()
    {
        var session = CreateSession();
        await session.Clipboard.CopyToClipboardAsync(Acknowledge);
        var old = session.Clipboard.Clipboard;
        var acknowledgement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = session.Clipboard.CopyToClipboardAsync((_, _) => new ValueTask(acknowledgement.Task), cut: true).AsTask();
        session.Selection.SetActiveCell(new CellAddress(5, 5));
        acknowledgement.SetResult();

        Assert.IsFalse(await pending);

        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.State.HasOsOwnership);
        Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "original");
        return new SpreadsheetSession(workbook);
    }

    private static ValueTask Acknowledge(SpreadsheetClipboardPackage package, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.IsNotNull(package);
        return ValueTask.CompletedTask;
    }

    private static ValueTask<SpreadsheetClipboardReadResult?> Read(string text) =>
        ValueTask.FromResult<SpreadsheetClipboardReadResult?>(new(text));
}
