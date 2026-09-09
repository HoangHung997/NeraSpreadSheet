using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class FormatCellsDraftTests
{
    [TestMethod]
    public void NumberPatchShouldPreserveMixedFontsValuesFormulasAndCreateOneUndo()
    {
        var session = new SpreadsheetSession(new Workbook()); var sheet = session.ActiveWorksheet;
        var first = CellStyle.Default with { Font = CellStyle.Default.Font with { Family = "Arial" } };
        var second = CellStyle.Default with { Font = CellStyle.Default.Font with { Family = "Times New Roman", Italic = true }, Fill = new CellFillStyle { IsVisible = true, Color = ColorRgba.Black } };
        sheet.SetCell(default, new CellData(CellValue.FromObject(12.5), styleId: session.Workbook.Styles.Intern(first)));
        var next = new CellAddress(1, 0);
        sheet.SetCell(next, new CellData(CellValue.FromObject(25d), "=A1*2", session.Workbook.Styles.Intern(second)));
        session.Selection.Select(new CellRange(default, next));
        using var draft = new SpreadsheetFormatCellsDraft(session);
        Assert.IsFalse(draft.TryGetCommon(style => style.Font.Family, out _));
        Assert.IsTrue(draft.Apply(new CellStylePatch { NumberFormatCode = "0.000" }));
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(first.Font, sheet.GetEffectiveStyle(default, session.Workbook.Styles).Font);
        Assert.AreEqual(second.Font, sheet.GetEffectiveStyle(next, session.Workbook.Styles).Font);
        Assert.AreEqual(second.Fill, sheet.GetEffectiveStyle(next, session.Workbook.Styles).Fill);
        Assert.AreEqual(12.5, sheet.GetValue(default)); Assert.AreEqual("=A1*2", sheet.GetFormula(next));
        Assert.IsTrue(session.Undo()); Assert.AreEqual(first, sheet.GetEffectiveStyle(default, session.Workbook.Styles));
        Assert.AreEqual(second, sheet.GetEffectiveStyle(next, session.Workbook.Styles));
        Assert.IsTrue(session.Redo()); Assert.AreEqual("0.000", sheet.GetEffectiveStyle(next, session.Workbook.Styles).NumberFormat.FormatCode);
    }
    [TestMethod]
    public void UntouchedAndEqualDraftShouldNotClearRedoOrInternPreviewStyles()
    {
        var session = new SpreadsheetSession(new Workbook()); session.SetValue(default, 1); session.Undo();
        var version = session.ActiveWorksheet.Version; var count = session.Workbook.Styles.Count;
        using (var draft = new SpreadsheetFormatCellsDraft(session)) Assert.IsFalse(draft.Apply(new CellStylePatch()));
        using (var draft = new SpreadsheetFormatCellsDraft(session)) Assert.IsFalse(draft.Apply(new CellStylePatch { NumberFormatCode = "General" }));
        Assert.AreEqual(version, session.ActiveWorksheet.Version); Assert.AreEqual(count, session.Workbook.Styles.Count);
        Assert.AreEqual(0, session.History.UndoCount); Assert.AreEqual(1, session.History.RedoCount);
    }
    [TestMethod]
    public void WholeColumnShouldRemainSparseAndApplyEvenWhenActiveCellAlreadyMatches()
    {
        var session = new SpreadsheetSession(new Workbook()); var sheet = session.ActiveWorksheet;
        sheet.SetCell(new CellAddress(9, 0), new CellData(CellValue.FromObject(42), styleId: session.Workbook.Styles.Intern(CellStyle.Default with
        { NumberFormat = new CellNumberFormatStyle { FormatCode = "0.00" }, Font = CellStyle.Default.Font with { Italic = true } })));
        session.Selection.SelectColumn(0);
        using var draft = new SpreadsheetFormatCellsDraft(session);
        Assert.IsFalse(draft.IsSelectionInspected);
        Assert.IsTrue(draft.Apply(new CellStylePatch { NumberFormatCode = "General" }));
        Assert.IsTrue(sheet.UsedCellCount <= 1); Assert.IsTrue(sheet.ColumnStyleSpanCount <= 1);
        Assert.AreEqual("General", sheet.GetEffectiveStyle(new CellAddress(9, 0), session.Workbook.Styles).NumberFormat.FormatCode);
        Assert.IsTrue(sheet.GetEffectiveStyle(new CellAddress(9, 0), session.Workbook.Styles).Font.Italic);
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo()); Assert.AreEqual("0.00", sheet.GetEffectiveStyle(new CellAddress(9, 0), session.Workbook.Styles).NumberFormat.FormatCode);
    }
    [TestMethod]
    public void WholeSheetOpeningAndFormattingShouldNeverMaterializeEmptyCells()
    {
        var session = new SpreadsheetSession(new Workbook()); session.Selection.SelectAll();
        using var draft = new SpreadsheetFormatCellsDraft(session);
        Assert.IsFalse(draft.IsSelectionInspected);
        draft.Apply(new CellStylePatch { FontFamily = "Arial", NumberFormatCode = "0.00" });
        Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount);
        Assert.AreEqual("Arial", session.ActiveWorksheet.GetEffectiveStyle(new CellAddress(900000, 10000), session.Workbook.Styles).Font.Family);
    }
    [TestMethod]
    public void OverlappingAndDisjointSelectionsShouldUseOnePatchTransaction()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(default, new CellAddress(2, 0)));
        session.Selection.Select(new CellRange(new CellAddress(1, 0), new CellAddress(3, 0)), true);
        session.Selection.Select(new CellRange(new CellAddress(5, 2), new CellAddress(5, 2)), true);
        using var draft = new SpreadsheetFormatCellsDraft(session); draft.Apply(new CellStylePatch { FontItalic = true });
        Assert.AreEqual(1, session.History.UndoCount); Assert.AreEqual(5, session.ActiveWorksheet.UsedCellCount);
        Assert.IsFalse(session.ActiveWorksheet.GetEffectiveStyle(new CellAddress(4, 0), session.Workbook.Styles).Font.Italic);
        session.Undo(); Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount);
    }
    [TestMethod]
    public void ReturningToAnOldSelectionShouldNotReactivateAStaleDraft()
    {
        var session = new SpreadsheetSession(new Workbook()); using var draft = new SpreadsheetFormatCellsDraft(session);
        session.Selection.SetActiveCell(new CellAddress(3, 3)); session.Selection.SetActiveCell(default);
        Assert.IsFalse(draft.IsCurrent); Refused(() => draft.Apply(new CellStylePatch { FontItalic = true }));
        Assert.AreEqual(0, session.History.UndoCount);
    }
    [TestMethod]
    public void ReturningToAnOldWorksheetShouldNotReactivateAStaleDraft()
    {
        var session = new SpreadsheetSession(new Workbook()); var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Other"); using var draft = new SpreadsheetFormatCellsDraft(session);
        session.ActivateWorksheet(second); session.ActivateWorksheet(first);
        Assert.IsFalse(draft.IsCurrent); Refused(() => draft.Apply(new CellStylePatch { FontItalic = true }));
        Assert.AreEqual(0, session.History.UndoCount);
    }
    [TestMethod]
    public void ExternalCellChangeAndDisposedDraftShouldRefuseMutation()
    {
        var session = new SpreadsheetSession(new Workbook()); var draft = new SpreadsheetFormatCellsDraft(session);
        session.ActiveWorksheet.SetValue(default, 99); Refused(() => draft.Apply(new CellStylePatch { FontItalic = true }));
        draft.Dispose(); Refused(() => draft.Apply(new CellStylePatch { FontItalic = true })); Assert.AreEqual(99d, session.ActiveWorksheet.GetValue(default));
    }
    [TestMethod]
    public void InvalidFontShouldBeRejectedBeforeAnyWorksheetMutation()
    {
        var session = new SpreadsheetSession(new Workbook()); using var draft = new SpreadsheetFormatCellsDraft(session);
        var version = session.ActiveWorksheet.Version; var catalog = session.Workbook.Styles.Count;
        Invalid(() => draft.Apply(new CellStylePatch { FontSize = -1 }));
        Assert.AreEqual(version, session.ActiveWorksheet.Version); Assert.AreEqual(catalog, session.Workbook.Styles.Count);
        Assert.AreEqual(0, session.History.UndoCount); Assert.IsTrue(draft.IsCurrent);
    }
    [TestMethod]
    public void ExcessiveFiniteSelectionShouldRefuseWithoutAllocatingCells()
    {
        var session = new SpreadsheetSession(new Workbook()); session.Selection.Select(new CellRange(default, new CellAddress(1000, 1000)));
        using var draft = new SpreadsheetFormatCellsDraft(session);
        Refused(() => draft.Apply(new CellStylePatch { FontItalic = true }));
        Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount); Assert.AreEqual(0, session.History.UndoCount);
    }
    [TestMethod]
    public void EveryGeneratedCategoryShouldHaveValidBoundedSyntax()
    {
        foreach (var category in Enum.GetValues<SpreadsheetNumberFormatCategory>().Where(value => value != SpreadsheetNumberFormatCategory.Custom))
        {
            var code = SpreadsheetNumberFormats.Create(category); SpreadsheetNumberFormats.Validate(code);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ExcelCellValueFormatter.Format(CellValue.FromObject(1234.5), code, culture: CultureInfo.GetCultureInfo("vi-VN"))));
        }
        Invalid(() => SpreadsheetNumberFormats.Create(SpreadsheetNumberFormatCategory.Number, 16));
    }
    [TestMethod]
    public void InvalidCustomSyntaxShouldBeRejectedWithoutTreatingFormatterFallbackAsValidation()
    {
        foreach (var code in new[] { "", "\"unfinished", "[Red", "0;0;0;@;0", "0\\", "0\n" }) Invalid(() => SpreadsheetNumberFormats.Validate(code));
        SpreadsheetNumberFormats.Validate("#,##0.00;(#,##0.00);\"-\";@");
        SpreadsheetNumberFormats.Validate("[h]:mm:ss");
    }
    [TestMethod]
    public void PreviewShouldRespectHostCultureWithoutChangingProcessCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        var value = CellValue.FromObject(1234.5);
        Assert.AreEqual("1.234,50", ExcelCellValueFormatter.Format(value, "#,##0.00", culture: CultureInfo.GetCultureInfo("vi-VN")));
        Assert.AreEqual("1,234.50", ExcelCellValueFormatter.Format(value, "#,##0.00", culture: CultureInfo.GetCultureInfo("en-US")));
        Assert.AreSame(culture, CultureInfo.CurrentCulture);
    }
    [TestMethod]
    public void PageSettingsShouldPreserveImportedMetadataAndUndoAsOneAction()
    {
        var session = new SpreadsheetSession(new Workbook()); var sheet = session.ActiveWorksheet;
        var original = new WorksheetPrintSettings { PrintArea = new CellRange(default, new CellAddress(50, 8)), PageSetup = new SpreadsheetPageSetup
        { OddHeader = "Existing", ManualRowBreaks = new[] { 20 }, Margins = new SpreadsheetPageMargins(.712345678, .7, .75, .75) } };
        sheet.SetPrintSettings(original);
        using (var draft = new SpreadsheetPageSetupDraft(session)) Assert.IsFalse(draft.Apply(draft.Initial));
        Assert.AreEqual(0, session.History.UndoCount);
        using (var draft = new SpreadsheetPageSetupDraft(session)) draft.Apply(draft.Initial with { PageSetup = draft.Initial.PageSetup with { Orientation = SpreadsheetPageOrientation.Landscape } });
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(original.PageSetup.Margins, sheet.GetPrintSettings().PageSetup.Margins);
        Assert.AreEqual(original.PrintArea, sheet.GetPrintSettings().PrintArea); Assert.AreEqual("Existing", sheet.GetPrintSettings().PageSetup.OddHeader);
        CollectionAssert.AreEqual(new[] { 20 }, sheet.GetPrintSettings().PageSetup.ManualRowBreaks.ToArray());
        session.Undo(); Assert.AreEqual(SpreadsheetPageOrientation.Portrait, sheet.GetPrintSettings().PageSetup.Orientation);
        session.Redo(); Assert.AreEqual(SpreadsheetPageOrientation.Landscape, sheet.GetPrintSettings().PageSetup.Orientation);
    }
    [TestMethod]
    public void InvalidPageMarginsAndConcurrentPrintChangeShouldRefuse()
    {
        var session = new SpreadsheetSession(new Workbook()); using var draft = new SpreadsheetPageSetupDraft(session);
        Invalid(() => draft.Apply(draft.Initial with { PageSetup = draft.Initial.PageSetup with { Margins = new SpreadsheetPageMargins(20, 20, 20, 20) } }));
        Assert.AreEqual(0, session.History.UndoCount);
        session.ActiveWorksheet.SetPrintSettings(draft.Initial with { PageSetup = draft.Initial.PageSetup with { OddHeader = "external" } });
        Refused(() => draft.Apply(draft.Initial)); Assert.AreEqual("external", session.ActiveWorksheet.GetPrintSettings().PageSetup.OddHeader);
    }
    private static void Invalid(Action action) { try { action(); Assert.Fail("Expected invalid input rejection."); } catch (ArgumentException) { } }
    private static void Refused(Action action) { try { action(); Assert.Fail("Expected stale/oversize target rejection."); } catch (InvalidOperationException) { } }
}
