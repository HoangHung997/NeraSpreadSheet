using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardPasteSpecialTests
{
    [TestMethod]
    public void ValuesShouldUseCopyTimeCachedValueAndKeepDestinationStyle()
    {
        var session = CreateFormulaSession();
        var sheet = session.ActiveWorksheet;
        var style = session.Workbook.Styles.Intern(new CellStyle { Alignment = new CellAlignmentStyle { WrapText = true } });
        var destination = new CellAddress(3, 3);
        sheet.SetStyle(destination, style);
        session.Clipboard.CopyPrimarySelection();
        sheet.SetValue(new CellAddress(0, 1), 100d);
        session.Recalculate();
        Assert.IsTrue(session.Clipboard.Paste(destination, SpreadsheetClipboardPasteMode.Values));
        Assert.AreEqual(10d, sheet.GetValue(destination));
        Assert.IsNull(sheet.GetFormula(destination));
        Assert.AreEqual(style, sheet.GetCell(destination).StyleId);
        Assert.IsTrue(session.Undo());
        Assert.IsNull(sheet.GetValue(destination));
        Assert.AreEqual(style, sheet.GetCell(destination).StyleId);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(10d, sheet.GetValue(destination));
    }

    [TestMethod]
    public void FormulasShouldTranslateRelativeReferencesAndKeepTargetStyle()
    {
        var session = CreateFormulaSession();
        var sheet = session.ActiveWorksheet;
        var destination = new CellAddress(3, 3);
        var style = session.Workbook.Styles.Intern(new CellStyle { Alignment = new CellAlignmentStyle { WrapText = true } });
        sheet.SetStyle(destination, style);
        sheet.SetFormula(default, "=B1+$B$1+\"B1\"");
        session.Clipboard.CopyPrimarySelection();
        Assert.IsTrue(session.Clipboard.Paste(destination, SpreadsheetClipboardPasteMode.Formulas));
        Assert.AreEqual("=E4+$B$1+\"B1\"", sheet.GetFormula(destination));
        Assert.AreEqual(style, sheet.GetCell(destination).StyleId);
        Assert.IsTrue(session.Undo());
        Assert.IsNull(sheet.GetFormula(destination));
    }

    [TestMethod]
    public void FormatsShouldKeepTargetValueAndFormulaWithoutTranslatingSourceFormula()
    {
        var session = CreateFormulaSession();
        var sheet = session.ActiveWorksheet;
        var style = session.Workbook.Styles.Intern(new CellStyle { Alignment = new CellAlignmentStyle { WrapText = true } });
        sheet.SetStyle(default, style);
        var destination = new CellAddress(4, 4);
        sheet.SetCell(destination, new CellData(CellValue.FromNumber(9), "=3*3"));
        session.Clipboard.CopyPrimarySelection();
        Assert.IsTrue(session.Clipboard.Paste(destination, SpreadsheetClipboardPasteMode.Formats));
        Assert.AreEqual("=3*3", sheet.GetFormula(destination));
        Assert.AreEqual(9d, sheet.GetValue(destination));
        Assert.AreEqual(style, sheet.GetCell(destination).StyleId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(CellStyleCatalog.DefaultStyleId, sheet.GetCell(destination).StyleId);
        Assert.AreEqual("=3*3", sheet.GetFormula(destination));
    }

    [TestMethod]
    public void ValuesShouldIncludeAllCachedDynamicArrayChildren()
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        sheet.SetFormula(default, "=SEQUENCE(2,2)");
        session.Recalculate();
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));
        session.Clipboard.CopyPrimarySelection();
        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(3, 3), SpreadsheetClipboardPasteMode.Values));
        Assert.AreEqual(1d, sheet.GetValue(new CellAddress(3, 3)));
        Assert.AreEqual(4d, sheet.GetValue(new CellAddress(4, 4)));
        Assert.IsNull(sheet.GetFormula(new CellAddress(3, 3)));
        Assert.AreEqual(1, sheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void BlankValuesShouldClearValuesButPreserveTargetFormatting()
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        var style = session.Workbook.Styles.Intern(new CellStyle { Alignment = new CellAlignmentStyle { WrapText = true } });
        sheet.SetValue(new CellAddress(2, 2), "old");
        sheet.SetStyle(new CellAddress(2, 2), style);
        session.Clipboard.CopyPrimarySelection();
        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(2, 2), SpreadsheetClipboardPasteMode.Values));
        Assert.IsNull(sheet.GetValue(new CellAddress(2, 2)));
        Assert.AreEqual(style, sheet.GetCell(new CellAddress(2, 2)).StyleId);
    }

    [TestMethod]
    public void AllShouldPasteIntoIdenticalMergeWithoutBlankingItsAnchor()
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        sheet.SetValue(default, "source");
        var source = new CellRange(default, new CellAddress(0, 1));
        var target = new CellRange(new CellAddress(2, 2), new CellAddress(2, 3));
        sheet.MergeCells(source);
        sheet.MergeCells(target);
        sheet.SetValue(target.TopLeft, "before");
        session.Selection.Select(source);
        session.Clipboard.CopyPrimarySelection();
        Assert.IsTrue(session.Clipboard.Paste(target.TopLeft));
        Assert.AreEqual("source", sheet.GetValue(target.TopLeft));
        Assert.IsTrue(sheet.MergedCells.Ranges.Contains(target));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("before", sheet.GetValue(target.TopLeft));
        Assert.IsTrue(sheet.MergedCells.Ranges.Contains(target));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("source", sheet.GetValue(target.TopLeft));
    }

    [TestMethod]
    public void AllShouldPreserveSourceValidationAndUndoDestinationRulesAsOneOperation()
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        sheet.SetValue(default, 5d);
        var rule = new DataValidationRule(Guid.NewGuid(), [new CellRange(default, default)],
            DataValidationType.Whole, DataValidationOperator.Between, "1", "10");
        sheet.AddDataValidationRule(rule);
        session.Clipboard.CopyPrimarySelection();
        var target = new CellAddress(3, 3);
        Assert.IsTrue(session.Clipboard.Paste(target));
        Assert.IsTrue(sheet.TryGetDataValidationRule(target, out var pasted));
        Assert.IsNotNull(pasted);
        Assert.AreNotEqual(rule.Id, pasted.Id);
        Assert.AreEqual(rule.Formula1, pasted.Formula1);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(sheet.TryGetDataValidationRule(target, out _));
        Assert.IsTrue(sheet.TryGetDataValidationRule(default, out _));
        Assert.IsFalse(session.Undo());
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(sheet.TryGetDataValidationRule(target, out _));
    }

    [TestMethod]
    public void PartialMergedTargetShouldRejectBeforeMutationOrHistory()
    {
        var session = CreateFormulaSession();
        var sheet = session.ActiveWorksheet;
        var target = new CellAddress(3, 3);
        sheet.MergeCells(new CellRange(target, new CellAddress(3, 4)));
        sheet.SetValue(target, "keep");
        session.Clipboard.CopyPrimarySelection();
        var before = sheet.Version;
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.Paste(target));
        Assert.AreEqual(before, sheet.Version);
        Assert.AreEqual("keep", sheet.GetValue(target));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void DeniedOrMutatingPastePolicyShouldNotApplyPendingCells()
    {
        var session = CreateFormulaSession();
        var sheet = session.ActiveWorksheet;
        session.Clipboard.CopyPrimarySelection();
        var target = new CellAddress(3, 3);
        session.Clipboard.PasteAuthorization = (_, _, _) => false;
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.Paste(target));
        Assert.IsTrue(sheet.GetCell(target).IsEmpty);
        session.Clipboard.PasteAuthorization = (_, _, _) => { session.Selection.SetActiveCell(new CellAddress(1, 1)); return true; };
        Assert.IsFalse(session.Clipboard.Paste(target));
        Assert.IsTrue(sheet.GetCell(target).IsEmpty);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void ExternalFormulaValuesShouldRejectInsteadOfSilentlyPastingBlank()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Clipboard.ImportTabSeparatedText("=2+3");
        Assert.IsFalse(session.Clipboard.CanPasteSpecial(SpreadsheetClipboardPasteMode.Values));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.Paste(default, SpreadsheetClipboardPasteMode.Values));
        Assert.IsTrue(session.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CancelCopyModeShouldKeepPayloadAndRespectActiveEditor()
    {
        var session = CreateFormulaSession();
        var package = session.Clipboard.CopyPrimarySelection();
        session.Selection.SetActiveCell(new CellAddress(4, 4));
        Assert.AreEqual(new CellRange(default, default), session.Clipboard.State.SourceRange);
        session.Editor.BeginEdit();
        Assert.IsFalse(session.Clipboard.CancelCopyMode());
        session.Editor.Cancel();
        Assert.IsTrue(session.Clipboard.CancelCopyMode());
        Assert.AreSame(package, session.Clipboard.Clipboard);
        Assert.IsTrue(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.State.IsCopyMode);
    }

    [TestMethod]
    public async Task DelayedReadShouldNotOverwriteAChangedWorksheet()
    {
        var session = CreateFormulaSession();
        var pending = new TaskCompletionSource<SpreadsheetClipboardReadResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = session.Clipboard.PasteFromClipboardAsync(_ => new ValueTask<SpreadsheetClipboardReadResult?>(pending.Task)).AsTask();
        session.ActiveWorksheet.SetValue(default, "newer");
        pending.SetResult(new SpreadsheetClipboardReadResult("old clipboard"));
        Assert.IsFalse(await read);
        Assert.AreEqual("newer", session.ActiveWorksheet.GetValue(default));
        Assert.IsFalse(session.Undo());
        Assert.AreEqual(SpreadsheetClipboardStatus.Stale, session.Clipboard.State.Status);
    }

    [TestMethod]
    public async Task ExternalReplacementShouldDisableOldPackageAndUseActualNewRead()
    {
        var session = CreateFormulaSession();
        session.Clipboard.CopyPrimarySelection();
        session.Clipboard.NotifyExternalClipboardChanged();
        Assert.IsFalse(session.Clipboard.CanPaste);
        Assert.IsFalse(session.Clipboard.Paste(new CellAddress(2, 2)));
        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ =>
            ValueTask.FromResult<SpreadsheetClipboardReadResult?>(new("actual external"))));
        Assert.AreEqual("actual external", session.ActiveWorksheet.GetValue(default));
    }

    [TestMethod]
    public async Task CanceledReadShouldKeepOldPayloadAndStayBusyUntilReaderSettles()
    {
        var session = CreateFormulaSession();
        var old = session.Clipboard.CopyPrimarySelection();
        var pending = new TaskCompletionSource<SpreadsheetClipboardReadResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = session.Clipboard.PasteFromClipboardAsync(_ => new ValueTask<SpreadsheetClipboardReadResult?>(pending.Task)).AsTask();
        Assert.IsTrue(session.Clipboard.CancelPendingClipboardWrite());
        Assert.IsTrue(session.Clipboard.IsClipboardWritePending);
        pending.SetResult(new SpreadsheetClipboardReadResult("late"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => read);
        Assert.AreSame(old, session.Clipboard.Clipboard);
        Assert.IsFalse(session.Clipboard.IsClipboardWritePending);
        Assert.AreEqual(SpreadsheetClipboardStatus.Canceled, session.Clipboard.State.Status);
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public async Task CommandsShouldUseSharedModesAndDisableMultiRangeCut()
    {
        var session = CreateFormulaSession();
        session.Clipboard.CopyPrimarySelection();
        session.Selection.SetActiveCell(new CellAddress(3, 3));
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.PasteValues));
        Assert.AreEqual(10d, session.ActiveWorksheet.GetValue(new CellAddress(3, 3)));
        Assert.IsNull(session.ActiveWorksheet.GetFormula(new CellAddress(3, 3)));
        session.Selection.AddRange(new CellRange(default, default));
        Assert.IsFalse(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Cut));
    }

    [TestMethod]
    public void OutOfBoundsAndInvalidModeShouldNotWriteOrCreateHistory()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Clipboard.ImportTabSeparatedText("1\t2");
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Clipboard.Paste(new CellAddress(0, SpreadsheetLimits.MaxColumns - 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Clipboard.Paste(default, (SpreadsheetClipboardPasteMode)100));
        Assert.IsFalse(session.Undo());
        Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount);
    }

    [TestMethod]
    public async Task MatchingOsPayloadShouldPreserveNativeFormulaTranslation()
    {
        var session = CreateFormulaSession();
        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync((_, _) => ValueTask.CompletedTask));
        var payload = session.Clipboard.Clipboard!;
        session.Selection.SetActiveCell(new CellAddress(3, 3));
        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ =>
            ValueTask.FromResult<SpreadsheetClipboardReadResult?>(new(payload.ToTabSeparatedText(), payload.PayloadId))));
        Assert.AreEqual("=E4*2", session.ActiveWorksheet.GetFormula(new CellAddress(3, 3)));
    }

    [TestMethod]
    public async Task MatchingTokenWithDifferentTextShouldUseExternalText()
    {
        var session = CreateFormulaSession();
        Assert.IsTrue(await session.Clipboard.CopyToClipboardAsync((_, _) => ValueTask.CompletedTask));
        var payload = session.Clipboard.Clipboard!;
        session.Selection.SetActiveCell(new CellAddress(3, 3));
        Assert.IsTrue(await session.Clipboard.PasteFromClipboardAsync(_ =>
            ValueTask.FromResult<SpreadsheetClipboardReadResult?>(new("42", payload.PayloadId))));
        Assert.AreEqual(42d, session.ActiveWorksheet.GetValue(new CellAddress(3, 3)));
        Assert.IsNull(session.ActiveWorksheet.GetFormula(new CellAddress(3, 3)));
    }

    private static SpreadsheetSession CreateFormulaSession()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.ActiveWorksheet.SetValue(new CellAddress(0, 1), 5d);
        session.ActiveWorksheet.SetFormula(default, "=B1*2");
        session.Recalculate();
        return session;
    }
}
