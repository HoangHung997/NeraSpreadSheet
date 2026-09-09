using global::Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class ClipboardTests
{
    [TestMethod]
    public Task CopyShouldPublishTextAndPasteShouldTranslateNativeFormula() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); fixture.Session.SetValue(new CellAddress(0, 1), 7d); fixture.Session.SetFormula(default, "=B1*2");
        Assert.IsTrue(await fixture.Control.CopyToClipboardAsync()); Assert.AreEqual("=B1*2", fixture.Transport.Data!.Text);
        fixture.Session.Selection.SetActiveCell(new CellAddress(1, 0)); Assert.IsTrue(await fixture.Control.PasteFromClipboardAsync());
        Assert.AreEqual("=B2*2", fixture.Session.ActiveWorksheet.GetCell(new CellAddress(1, 0)).Formula);
    });
    [TestMethod]
    public Task ExternalTextShouldReplaceStaleNativeClipboard() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); fixture.Session.SetValue(default, "Old"); await fixture.Control.CopyToClipboardAsync();
        fixture.Transport.Data = new NeraClipboardData("Mới\t12\r\n\"Dòng\ntrong ô\"\t=SUM(B1,2)");
        Assert.IsTrue(await fixture.Control.PasteFromClipboardAsync());
        Assert.AreEqual("Mới", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.AreEqual("Dòng\ntrong ô", fixture.Session.ActiveWorksheet.GetCell(new CellAddress(1, 0)).Value.ToString());
        Assert.AreEqual("=SUM(B1,2)", fixture.Session.ActiveWorksheet.GetCell(new CellAddress(1, 1)).Formula);
        Assert.IsTrue(fixture.Session.Undo()); Assert.AreEqual("Old", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });
    [TestMethod]
    public Task FailedCutWriteShouldNotClearSource() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); fixture.Session.SetValue(default, "Keep"); var version = fixture.Session.Workbook.Version;
        fixture.Transport.WriteFailure = new IOException("Clipboard unavailable");
        try { await fixture.Control.CopyToClipboardAsync(true); Assert.Fail("Write failure must propagate."); } catch (IOException) { }
        Assert.AreEqual(version, fixture.Session.Workbook.Version); Assert.AreEqual("Keep", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });
    [TestMethod]
    public Task DelayedPasteShouldNotWriteAfterSheetSwitch() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); var second = fixture.Session.Workbook.AddWorksheet("Second");
        var pending = new TaskCompletionSource<NeraClipboardData?>(TaskCreationOptions.RunContinuationsAsynchronously); fixture.Transport.PendingRead = pending.Task;
        var paste = fixture.Control.PasteFromClipboardAsync(); fixture.Session.ActivateWorksheet(second); pending.SetResult(new NeraClipboardData("Wrong sheet"));
        Assert.IsFalse(await paste); Assert.IsTrue(second.GetCell(default).IsEmpty); Assert.IsTrue(fixture.Session.Workbook.Worksheets[0].GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task DelayedCutShouldNotClearNewSelection() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); fixture.Session.SetValue(default, "Original");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); fixture.Transport.PendingWrite = pending.Task;
        var cut = fixture.Control.CopyToClipboardAsync(true); fixture.Session.Selection.SetActiveCell(new CellAddress(4, 4)); pending.SetResult();
        Assert.IsFalse(await cut); Assert.AreEqual("Original", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });
    [TestMethod]
    public Task DelayedPasteShouldNotWriteAfterDetachAndReattach() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); var pending = new TaskCompletionSource<NeraClipboardData?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.PendingRead = pending.Task; var paste = fixture.Control.PasteFromClipboardAsync();
        fixture.Window.Content = null; fixture.Window.Content = fixture.Control; pending.SetResult(new NeraClipboardData("Stale"));
        Assert.IsFalse(await paste); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task DelayedPasteShouldNotWriteAfterDispose() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); var pending = new TaskCompletionSource<NeraClipboardData?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.PendingRead = pending.Task; var paste = fixture.Control.PasteFromClipboardAsync(); fixture.Control.Dispose();
        pending.SetResult(new NeraClipboardData("Stale")); Assert.IsFalse(await paste); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task CancellationShouldNotMutateWorkbook() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await fixture.Control.PasteFromClipboardAsync(cancellation.Token); Assert.Fail("Cancellation must propagate."); } catch (OperationCanceledException) { }
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook()); Transport = new Transport();
            Control = new NeraSpreadsheetControl { Session = Session, ClipboardTransportOverride = Transport };
            Window = new Window { Width = 800, Height = 500, Content = Control }; Window.Show(); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public Transport Transport { get; }
        public NeraSpreadsheetControl Control { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
    private sealed class Transport : INeraClipboardTransport
    {
        public NeraClipboardData? Data { get; set; }
        public IOException? WriteFailure { get; set; }
        public Task? PendingWrite { get; set; }
        public Task<NeraClipboardData?>? PendingRead { get; set; }
        public async ValueTask WriteAsync(NeraClipboardData value, CancellationToken cancellationToken)
        {
            if (WriteFailure is { } failure) throw failure;
            if (PendingWrite is { } wait) await wait.WaitAsync(cancellationToken);
            Data = value;
        }
        public async ValueTask<NeraClipboardData?> ReadAsync(CancellationToken cancellationToken) =>
            PendingRead is { } wait ? await wait.WaitAsync(cancellationToken) : Data;
    }
}
