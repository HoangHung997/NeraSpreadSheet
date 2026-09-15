using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardMultiRangeCut016Tests
{
    [TestMethod]
    public void SynchronousCutShouldRejectDisjointRangesAtomically()
    {
        var session = CreateDisjointSelection();
        var worksheet = session.ActiveWorksheet;
        var previous = session.Clipboard.ImportTabSeparatedText("previous clipboard");
        var version = worksheet.Version;
        var selection = session.Selection.Capture();

        Assert.IsFalse(session.Clipboard.CanCut);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.CutPrimarySelection());

        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("A1", worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("C1", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.AreEqual("B1-outside", worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(selection, session.Selection.Capture());
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
        Assert.IsFalse(session.Redo());
    }

    [TestMethod]
    public async Task AsynchronousCutShouldRejectDisjointRangesBeforeTransport()
    {
        var session = CreateDisjointSelection();
        var worksheet = session.ActiveWorksheet;
        var previous = session.Clipboard.ImportTabSeparatedText("previous clipboard");
        var writes = 0;
        var version = worksheet.Version;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.Clipboard.CopyToClipboardAsync((_, _) =>
            {
                writes++;
                return ValueTask.CompletedTask;
            }, cut: true).AsTask());

        Assert.AreEqual(0, writes);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("A1", worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("C1", worksheet.GetValue(new CellAddress(0, 2)));
        Assert.AreEqual("B1-outside", worksheet.GetValue(new CellAddress(0, 1)));
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CopyAvailabilityShouldRejectDisjointRangesInsteadOfPublishingPrimaryOnly()
    {
        var session = CreateDisjointSelection();
        var previous = session.Clipboard.ImportTabSeparatedText("previous clipboard");

        Assert.IsFalse(session.Clipboard.CanCopy);
        Assert.IsFalse(session.Clipboard.CanCut);
        Assert.AreSame(previous, session.Clipboard.Clipboard);
    }

    private static SpreadsheetSession CreateDisjointSelection()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "A1");
        worksheet.SetValue(new CellAddress(0, 1), "B1-outside");
        worksheet.SetValue(new CellAddress(0, 2), "C1");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));
        session.Selection.Select(new CellRange(new CellAddress(0, 2), new CellAddress(0, 2)), additive: true);
        Assert.AreEqual(2, session.Selection.Ranges.Count);
        return session;
    }
}
