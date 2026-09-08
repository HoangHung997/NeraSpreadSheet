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
