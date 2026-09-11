using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class DataReview009DialogTests
{
    [TestMethod]
    public Task DataValidationDialogCreatesAListRuleInOneUndo() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)));
        using var fixture = new Fixture(new NeraDataValidationDialog(session));
        fixture.Select("validation-type", "List");
        fixture.Find<TextBox>("validation-formula1").Text = "\"Yes,No\"";
        fixture.SelectTab("input");
        fixture.Find<CheckBox>("validation-show-input").IsChecked = true;
        fixture.Find<TextBox>("validation-prompt-title").Text = "Choose";
        fixture.Find<TextBox>("validation-prompt").Text = "Pick Yes or No";
        fixture.Click("dialog-ok");

        Assert.AreEqual(1, session.ActiveWorksheet.DataValidationRuleCount);
        var rule = session.ActiveWorksheet.DataValidationRules.Single();
        Assert.AreEqual(DataValidationType.List, rule.Type);
        Assert.AreEqual("=\"Yes,No\"", rule.Formula1);
        Assert.IsTrue(rule.ShowInputMessage);
        Assert.AreEqual("Choose", rule.PromptTitle);
        Assert.AreEqual(1, session.History.UndoCount);
    });

    [TestMethod]
    public Task AdvancedFilterDialogFiltersUsingCriteriaRange() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateFilterSession();
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)));
        using var fixture = new Fixture(new NeraAdvancedFilterDialog(session));
        fixture.Find<TextBox>("advanced-filter-list-range").Text = "A1:B4";
        fixture.Find<TextBox>("advanced-filter-criteria-range").Text = "D1:D2";
        fixture.Click("dialog-ok");
        Assert.IsTrue(session.ActiveWorksheet.Dimensions.TryGetHiddenRowRange(1, out _));
        Assert.IsFalse(session.ActiveWorksheet.Dimensions.TryGetHiddenRowRange(2, out _));
        Assert.AreEqual(1, session.History.UndoCount);
    });

    [TestMethod]
    public Task ConsolidateDialogAddsReferenceAndWritesResult() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, 3d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, default));
        using var fixture = new Fixture(new NeraConsolidateDialog(session));
        fixture.Find<TextBox>("consolidate-reference").Text = "Sheet1!A1:A1";
        fixture.Click("consolidate-add-reference");
        fixture.Find<TextBox>("consolidate-destination").Text = "C1";
        fixture.Click("dialog-ok");
        Assert.AreEqual(3d, sheet.GetValue(new CellAddress(0, 2)));
        Assert.AreEqual(1, session.History.UndoCount);
    });

    [TestMethod]
    public Task ProtectSheetDialogHashesPasswordAndProtectionBlocksLockedCells() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraProtectSheetDialog(session));
        fixture.Find<TextBox>("protect-sheet-password").Text = "pw";
        fixture.Find<TextBox>("protect-sheet-confirm").Text = "pw";
        fixture.Click("dialog-ok");
        var settings = session.ActiveWorksheet.GetProtectionSettings();
        Assert.IsTrue(settings.Enabled);
        Assert.AreNotEqual("pw", settings.PasswordHash);
        Assert.IsTrue(settings.VerifyPassword("pw"));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.SetValue(default, 1d));
    });

    [TestMethod]
    public Task ProtectWorkbookDialogCreatesUndoableStructureProtection() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraProtectWorkbookDialog(session));
        fixture.Find<TextBox>("protect-workbook-password").Text = "book";
        fixture.Find<TextBox>("protect-workbook-confirm").Text = "book";
        fixture.Click("dialog-ok");
        Assert.IsTrue(session.Workbook.GetProtectionSettings().Enabled);
        Assert.IsTrue(session.Workbook.GetProtectionSettings().LockStructure);
        Assert.AreEqual(1, session.History.UndoCount);
        session.Undo();
        Assert.IsFalse(session.Workbook.GetProtectionSettings().Enabled);
    });

    private static SpreadsheetSession CreateFilterSession()
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
        return new SpreadsheetSession(workbook);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Window _owner = new() { Width = 1000, Height = 780 };
        internal NeraSettingsDialog Dialog { get; }

        internal Fixture(NeraSettingsDialog dialog)
        {
            Dialog = dialog;
            _owner.Show();
            _ = Dialog.ShowDialog<bool>(_owner);
            Dialog.UpdateLayout();
        }

        internal T Find<T>(string id) where T : Control =>
            Dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);

        internal void Select(string id, string tag)
        {
            var choice = Find<ComboBox>(id);
            choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, tag));
        }

        internal void SelectTab(string tag)
        {
            var tabs = Dialog.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Tag, tag));
            Dialog.UpdateLayout();
        }

        internal void Click(string id) => Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        public void Dispose()
        {
            Dialog.Close();
            _owner.Close();
        }
    }
}
