using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DataReview009Tests
{
    [TestMethod]
    public void DataValidationDraftReplacesSelectionRulesInOneUndo()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)));
        using (var draft = new SpreadsheetDataValidationDraft(session))
        {
            draft.Apply(new DataValidationRule(
                Guid.NewGuid(),
                draft.TargetRanges,
                DataValidationType.List,
                null,
                "\"Yes,No\"",
                allowBlank: true,
                showDropDown: true));
        }

        Assert.AreEqual(1, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.AreEqual(DataValidationType.List, session.ActiveWorksheet.DataValidationRules[0].Type);
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.ActiveWorksheet.DataValidationRuleCount);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, session.ActiveWorksheet.DataValidationRuleCount);
    }

    [TestMethod]
    public void ProtectedSheetBlocksLockedCellsAndAllowsUnlockedCells()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(default, default));
        session.Styles.ApplyPatchToSelection(new CellStylePatch { ProtectionLocked = false }, "Unlock cell");
        session.History.Clear();
        var protection = new SpreadsheetProtectionController(session);
        protection.SetWorksheetProtection(new WorksheetProtectionSettings
        {
            Enabled = true,
            PasswordHash = SpreadsheetProtectionPassword.HashLegacy("pw"),
        });

        session.SetValue(default, 10d);
        Assert.AreEqual(10d, session.ActiveWorksheet.GetValue(default));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.SetValue(new CellAddress(0, 1), 20d));
        Assert.IsTrue(protection.WorksheetSettings.VerifyPassword("pw"));
        Assert.IsFalse(protection.WorksheetSettings.VerifyPassword("wrong"));
    }

    [TestMethod]
    public void WorkbookProtectionIsUndoableWithoutRetainingPlaintextPassword()
    {
        var session = new SpreadsheetSession(new Workbook());
        var controller = new SpreadsheetProtectionController(session);
        controller.SetWorkbookProtection(new WorkbookProtectionSettings
        {
            Enabled = true,
            LockStructure = true,
            PasswordHash = SpreadsheetProtectionPassword.HashLegacy("secret"),
        });

        Assert.IsTrue(session.Workbook.GetProtectionSettings().Enabled);
        Assert.AreNotEqual("secret", session.Workbook.GetProtectionSettings().PasswordHash);
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Workbook.GetProtectionSettings().Enabled);
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(session.Workbook.GetProtectionSettings().Enabled);
    }

    [TestMethod]
    public void AdvancedFilterUsesCriteriaRowsAndUndoableVisibility()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Name");
        sheet.SetValue(new CellAddress(0, 1), "Score");
        sheet.SetValue(new CellAddress(1, 0), "Bob");
        sheet.SetValue(new CellAddress(1, 1), 1d);
        sheet.SetValue(new CellAddress(2, 0), "Ana");
        sheet.SetValue(new CellAddress(2, 1), 2d);
        sheet.SetValue(new CellAddress(3, 0), "Ana");
        sheet.SetValue(new CellAddress(3, 1), 3d);
        sheet.SetValue(new CellAddress(0, 3), "Name");
        sheet.SetValue(new CellAddress(1, 3), "Ana");
        var session = new SpreadsheetSession(workbook);
        var controller = new SpreadsheetAdvancedFilterController(session);

        controller.Apply(new SpreadsheetAdvancedFilterOptions
        {
            Action = SpreadsheetAdvancedFilterAction.FilterInPlace,
            ListRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            CriteriaRange = new CellRange(new CellAddress(0, 3), new CellAddress(1, 3)),
        });

        Assert.IsTrue(sheet.Dimensions.TryGetHiddenRowRange(1, out _));
        Assert.IsFalse(sheet.Dimensions.TryGetHiddenRowRange(2, out _));
        Assert.IsFalse(sheet.Dimensions.TryGetHiddenRowRange(3, out _));
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(sheet.Dimensions.TryGetHiddenRowRange(1, out _));
    }

    [TestMethod]
    public void AdvancedFilterCanCopyUniqueRowsToAnotherLocation()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Name");
        sheet.SetValue(new CellAddress(1, 0), "Ana");
        sheet.SetValue(new CellAddress(2, 0), "Ana");
        sheet.SetValue(new CellAddress(3, 0), "Bob");
        var session = new SpreadsheetSession(workbook);
        new SpreadsheetAdvancedFilterController(session).Apply(new SpreadsheetAdvancedFilterOptions
        {
            Action = SpreadsheetAdvancedFilterAction.CopyToAnotherLocation,
            ListRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)),
            CopyTo = new CellAddress(0, 3),
            UniqueRecordsOnly = true,
        });

        Assert.AreEqual("Name", sheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual("Ana", sheet.GetValue(new CellAddress(1, 3)));
        Assert.AreEqual("Bob", sheet.GetValue(new CellAddress(2, 3)));
        Assert.IsNull(sheet.GetValue(new CellAddress(3, 3)));
        Assert.AreEqual(1, session.History.UndoCount);
    }

    [TestMethod]
    public void ConsolidateSumsSamePositionAcrossWorksheetsInOneUndo()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        first.SetValue(new CellAddress(0, 0), 1d);
        first.SetValue(new CellAddress(0, 1), 2d);
        second.SetValue(new CellAddress(0, 0), 10d);
        second.SetValue(new CellAddress(0, 1), 20d);
        var session = new SpreadsheetSession(workbook, first);
        new SpreadsheetConsolidationController(session).Consolidate(new SpreadsheetConsolidationOptions
        {
            Function = SpreadsheetConsolidationFunction.Sum,
            Sources =
            [
                new SpreadsheetConsolidationSource(first, new CellRange(new CellAddress(0, 0), new CellAddress(0, 1))),
                new SpreadsheetConsolidationSource(second, new CellRange(new CellAddress(0, 0), new CellAddress(0, 1))),
            ],
            Destination = new CellAddress(0, 3),
        });

        Assert.AreEqual(11d, first.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(22d, first.GetValue(new CellAddress(0, 4)));
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsNull(first.GetValue(new CellAddress(0, 3)));
        Assert.IsNull(first.GetValue(new CellAddress(0, 4)));
    }
}
