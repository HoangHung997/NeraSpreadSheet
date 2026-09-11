using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class PageSetupFidelity008Tests
{
    [TestMethod]
    public Task PageSetupExposesFourTabsAndAppliesExcelPrintFieldsInOneUndo() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraPageSetupDialog(session, NeraPageSetupTab.Sheet));
        var dialog = (NeraPageSetupDialog)fixture.Dialog;
        Assert.AreEqual(NeraPageSetupTab.Sheet, dialog.SelectedPageTab);
        Assert.AreEqual(4, fixture.Dialog.GetVisualDescendants().OfType<TabItem>().Count(tab => (tab.Tag as string) is "page" or "margins" or "header-footer" or "sheet"));

        fixture.Find<TextBox>("page-print-area").Text = "A1:H50";
        fixture.Find<TextBox>("page-repeat-rows").Text = "1:2";
        fixture.Find<TextBox>("page-repeat-columns").Text = "A:B";
        fixture.Find<CheckBox>("page-black-white").IsChecked = true;
        fixture.Find<CheckBox>("page-draft-quality").IsChecked = true;
        fixture.Select("page-comments", "AtEnd");
        fixture.Select("page-errors", "Dash");
        fixture.Select("page-order", "OverThenDown");

        fixture.SelectTab("header-footer");
        fixture.Find<TextBox>("page-even-header").Text = "&LEven";
        fixture.Find<TextBox>("page-first-footer").Text = "&CFirst";
        fixture.Find<CheckBox>("page-different-odd-even").IsChecked = true;
        fixture.Find<CheckBox>("page-different-first").IsChecked = true;

        fixture.SelectTab("page");
        fixture.Find<TextBox>("page-print-quality").Text = "600";
        fixture.Find<TextBox>("page-first-page").Text = "3";
        fixture.Click("dialog-ok");

        var settings = session.ActiveWorksheet.GetPrintSettings();
        Assert.AreEqual(new CellRange(new CellAddress(0, 0), new CellAddress(49, 7)), settings.PrintArea);
        Assert.AreEqual(0, settings.PageSetup.RepeatTitles.Rows?.Top);
        Assert.AreEqual(1, settings.PageSetup.RepeatTitles.Rows?.Bottom);
        Assert.AreEqual(1, settings.PageSetup.RepeatTitles.Columns?.Right);
        Assert.AreEqual(SpreadsheetPageOrder.OverThenDown, settings.PageSetup.PageOrder);
        Assert.AreEqual(600, settings.PageSetup.PrintQualityDpi);
        Assert.AreEqual(3, settings.PageSetup.FirstPageNumber);
        Assert.IsTrue(settings.PageSetup.BlackAndWhite);
        Assert.IsTrue(settings.PageSetup.DraftQuality);
        Assert.AreEqual(SpreadsheetPrintComments.AtEnd, settings.PageSetup.PrintComments);
        Assert.AreEqual(SpreadsheetPrintErrors.Dash, settings.PageSetup.PrintErrors);
        Assert.AreEqual("&LEven", settings.PageSetup.EvenHeader);
        Assert.AreEqual("&CFirst", settings.PageSetup.FirstFooter);
        Assert.IsTrue(settings.PageSetup.DifferentOddEvenPages);
        Assert.IsTrue(settings.PageSetup.DifferentFirstPage);
        Assert.AreEqual(1, session.History.UndoCount);
        session.Undo();
        Assert.IsNull(session.ActiveWorksheet.GetPrintSettings().PrintArea);
    });

    [TestMethod]
    public Task PageSetupShowsPrintPreviewOptionsActions() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraPageSetupDialog(session));
        Assert.IsNotNull(fixture.Find<Button>("page-action-print"));
        Assert.IsNotNull(fixture.Find<Button>("page-action-preview"));
        Assert.IsNotNull(fixture.Find<Button>("page-action-options"));
        fixture.Click("page-action-preview");
        Assert.AreEqual(NeraPageSetupAction.PrintPreview, ((NeraPageSetupDialog)fixture.Dialog).RequestedAction);
    });

    private sealed class Fixture : IDisposable
    {
        private readonly Window _owner = new() { Width = 900, Height = 700 };
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

        internal void Click(string id) => Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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

        public void Dispose()
        {
            Dialog.Close();
            _owner.Close();
        }
    }
}
