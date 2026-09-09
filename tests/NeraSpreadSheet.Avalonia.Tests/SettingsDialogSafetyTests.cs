using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SettingsDialogSafetyTests
{
    [TestMethod]
    public Task RestoringTheInitialFieldTextShouldPreserveRedoAndStyleCatalog() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        session.SetValue(default, 12.5);
        session.Undo();
        var version = session.ActiveWorksheet.Version;
        var catalog = session.Workbook.Styles.Count;
        using var fixture = new Fixture(new NeraFormatCellsDialog(session));
        var code = fixture.Find<TextBox>("format-code");
        var original = code.Text;
        code.Text = "0.000";
        Assert.IsTrue(((NeraFormatCellsDialog)fixture.Dialog).HasPendingChanges);
        code.Text = original;
        Assert.IsFalse(((NeraFormatCellsDialog)fixture.Dialog).HasPendingChanges);
        fixture.Click("dialog-ok");
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual(catalog, session.Workbook.Styles.Count);
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.AreEqual(1, session.History.RedoCount);
    });

    [TestMethod]
    public Task InvalidDecimalGeneratorParameterShouldNotApplyTheLastValidFormat() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraFormatCellsDialog(session));
        var category = fixture.Find<ComboBox>("format-category");
        category.SelectedItem = category.Items.OfType<ComboBoxItem>().Single(item => (string?)item.Tag == nameof(SpreadsheetNumberFormatCategory.Number));
        fixture.Find<TextBox>("format-decimals").Text = "16";
        fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible);
        Assert.IsTrue(fixture.Find<TextBlock>("dialog-validation").IsVisible);
        Assert.AreEqual(0, session.History.UndoCount);
        fixture.Find<TextBox>("format-decimals").Text = "3";
        fixture.Click("dialog-ok");
        Assert.IsFalse(fixture.Dialog.IsVisible);
        Assert.AreEqual("#,##0.000", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(1, session.History.UndoCount);
    });

    [TestMethod]
    public Task ChangingOneMixedFontPropertyShouldKeepDifferentNumberFormatsAndValues() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var workbook = new Workbook(); var sheet = workbook.Worksheets[0];
        var first = workbook.Styles.Intern(CellStyle.Default with { NumberFormat = new CellNumberFormatStyle { FormatCode = "0.00" } });
        var second = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Italic = true }, NumberFormat = new CellNumberFormatStyle { FormatCode = "0%" } });
        sheet.SetCells([
            new KeyValuePair<CellAddress, CellData>(default, new CellData(CellValue.FromObject(12.5), styleId: first)),
            new KeyValuePair<CellAddress, CellData>(new CellAddress(0, 1), new CellData(CellValue.FromObject(.25), styleId: second)),
        ]);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(0, 1)));
        using var fixture = new Fixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Font));
        fixture.Find<TextBox>("format-font-size").Text = "14";
        fixture.Click("dialog-ok");
        var a = sheet.GetEffectiveStyle(default, workbook.Styles);
        var b = sheet.GetEffectiveStyle(new CellAddress(0, 1), workbook.Styles);
        Assert.AreEqual(14d, a.Font.Size); Assert.AreEqual(14d, b.Font.Size);
        Assert.IsFalse(a.Font.Italic); Assert.IsTrue(b.Font.Italic);
        Assert.AreEqual("0.00", a.NumberFormat.FormatCode); Assert.AreEqual("0%", b.NumberFormat.FormatCode);
        Assert.AreEqual(12.5, sheet.GetValue(default)); Assert.AreEqual(.25, sheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(1, session.History.UndoCount);
        session.Undo(); Assert.AreEqual(first, sheet.GetCell(default).StyleId); Assert.AreEqual(second, sheet.GetCell(new CellAddress(0, 1)).StyleId);
    });

    [TestMethod]
    public Task DisposingVisibleFormatAndPageDialogsShouldCancelWithoutApplying() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using (var fixture = new Fixture(new NeraFormatCellsDialog(session)))
        {
            fixture.Find<TextBox>("format-code").Text = "0.00";
            ((NeraFormatCellsDialog)fixture.Dialog).Dispose();
            Assert.IsFalse(fixture.Dialog.IsVisible);
        }
        using (var fixture = new Fixture(new NeraPageSetupDialog(session, NeraPageSetupTab.Sheet)))
        {
            fixture.Find<CheckBox>("page-gridlines").IsChecked = true;
            ((NeraPageSetupDialog)fixture.Dialog).Dispose();
            Assert.IsFalse(fixture.Dialog.IsVisible);
        }
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.AreEqual("General", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.IsFalse(session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintGridlines);
    });

    [TestMethod]
    public Task UnshownDialogsShouldHaveAnIdempotentDisposalPath() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        var version = session.ActiveWorksheet.Version;
        using var formatting = new NeraFormatCellsDialog(session);
        using var page = new NeraPageSetupDialog(session);
        formatting.Dispose(); formatting.Dispose(); page.Dispose(); page.Dispose();
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual(0, session.History.UndoCount);
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        Assert.AreEqual(new CellAddress(2, 2), session.Selection.ActiveCell);
    });

    private sealed class Fixture : IDisposable
    {
        private readonly Window _owner = new() { Width = 950, Height = 750 };
        internal NeraSettingsDialog Dialog { get; }
        internal Fixture(NeraSettingsDialog dialog)
        {
            Dialog = dialog;
            _owner.Show();
            _ = dialog.ShowDialog<bool>(_owner);
            dialog.UpdateLayout();
        }
        internal T Find<T>(string id) where T : Control => Dialog.GetVisualDescendants().OfType<T>()
            .Single(control => AutomationProperties.GetAutomationId(control) == id);
        internal void Click(string id) => Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Dispose()
        {
            if (Dialog is IDisposable disposable) disposable.Dispose();
            else Dialog.Close();
            _owner.Close();
        }
    }
}
