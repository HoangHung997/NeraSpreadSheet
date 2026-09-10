using System.Globalization;
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
public sealed class NumberDialogSynchronizationTests
{
    [TestMethod]
    [DataRow("General", "General", 2, true, false)]
    [DataRow("0", "Number", 0, false, false)]
    [DataRow("#,##0.000", "Number", 3, true, false)]
    [DataRow("0.00;(0.00)", "Number", 2, false, true)]
    [DataRow("0.000%", "Percentage", 3, false, false)]
    [DataRow("0.00E+00", "Scientific", 2, false, false)]
    [DataRow("dd/mm/yyyy", "Date", 2, true, false)]
    [DataRow("hh:mm:ss", "Time", 2, true, false)]
    [DataRow("# ?/?", "Fraction", 2, true, false)]
    [DataRow("@", "Text", 2, true, false)]
    [DataRow("[Red][<0]0.00;0.000", "Custom", 2, true, false)]
    [DataRow("[$-409]dd mmmm yyyy", "Custom", 2, true, false)]
    public Task ExistingFormatShouldInitializeFieldsWithoutRewritingTheWorkbook(string format, string category, int decimals, bool thousands, bool parentheses) => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession(format);
        var version = session.ActiveWorksheet.Version;
        using var fixture = new Fixture(session);
        Assert.AreEqual(category, fixture.Category);
        Assert.AreEqual(decimals.ToString(CultureInfo.InvariantCulture), fixture.Find<TextBox>("format-decimals").Text);
        Assert.AreEqual(thousands, fixture.Find<CheckBox>("format-thousands").IsChecked);
        Assert.AreEqual(parentheses, fixture.Find<CheckBox>("format-negative").IsChecked);
        Assert.AreEqual(format, fixture.Find<TextBox>("format-code").Text);
        Assert.IsFalse(fixture.Dialog.HasPendingChanges);
        fixture.Click("dialog-ok");
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.AreEqual(format, session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(1234.5, session.ActiveWorksheet.GetValue(default));
    });

    [TestMethod]
    public Task EditingExistingPrecisionShouldKeepGroupingAndCreateOneUndo() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession("#,##0.000");
        using var fixture = new Fixture(session);
        fixture.Find<TextBox>("format-decimals").Text = "4";
        Assert.AreEqual("Number", fixture.Category);
        Assert.AreEqual("#,##0.0000", fixture.Find<TextBox>("format-code").Text);
        fixture.Click("dialog-ok");
        Assert.AreEqual("#,##0.0000", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(1234.5, session.ActiveWorksheet.GetValue(default));
        session.Undo();
        Assert.AreEqual("#,##0.000", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
    });

    [TestMethod]
    public Task DirectCustomCodeShouldSupersedeAnInvalidGeneratorDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession("0.00");
        using var fixture = new Fixture(session);
        fixture.Find<TextBox>("format-decimals").Text = "16";
        Assert.IsTrue(fixture.Dialog.HasPendingChanges);
        fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible);
        Assert.AreEqual(0, session.History.UndoCount);
        fixture.Find<TextBox>("format-code").Text = "0.0";
        Assert.AreEqual("Custom", fixture.Category);
        Assert.IsFalse(fixture.Find<TextBox>("format-decimals").IsEnabled);
        fixture.Click("dialog-ok");
        Assert.IsFalse(fixture.Dialog.IsVisible);
        Assert.AreEqual("0.0", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(1, session.History.UndoCount);
    });

    [TestMethod]
    public Task InapplicableHelperChangesShouldNotOverwriteADirectCode() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession("0.00");
        using var fixture = new Fixture(session);
        fixture.Find<TextBox>("format-code").Text = "0.00000";
        fixture.Find<TextBox>("format-decimals").Text = "999";
        fixture.Find<TextBox>("format-currency").Text = new string('x', 40);
        fixture.Find<CheckBox>("format-thousands").IsChecked = true;
        Assert.AreEqual("0.00000", fixture.Find<TextBox>("format-code").Text);
        fixture.SelectCategory("Date");
        Assert.AreEqual("dd/mm/yyyy", fixture.Find<TextBox>("format-code").Text);
        fixture.Click("dialog-ok");
        Assert.IsFalse(fixture.Dialog.IsVisible);
        Assert.AreEqual("dd/mm/yyyy", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
    });

    [TestMethod]
    public Task InvalidManualCodeShouldStillBeRejectedAfterLeavingTheGenerator() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession("0.00");
        using var fixture = new Fixture(session);
        fixture.Find<TextBox>("format-decimals").Text = "16";
        fixture.Find<TextBox>("format-code").Text = "0.00\"";
        Assert.AreEqual("Custom", fixture.Category);
        fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible);
        Assert.IsTrue(fixture.Find<TextBlock>("dialog-validation").IsVisible);
        Assert.AreEqual(0, session.History.UndoCount);
        fixture.Click("dialog-cancel");
        Assert.AreEqual("0.00", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
    });

    [TestMethod]
    public Task ReturningToTheOriginalCodeShouldNotLoseRedoAfterAnInvalidHelper() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = CreateSession("0.00");
        session.SetValue(default, 999);
        session.Undo();
        var version = session.ActiveWorksheet.Version;
        using var fixture = new Fixture(session);
        fixture.Find<TextBox>("format-decimals").Text = "16";
        fixture.Find<TextBox>("format-code").Text = "0.0";
        fixture.Find<TextBox>("format-code").Text = "0.00";
        Assert.IsFalse(fixture.Dialog.HasPendingChanges);
        fixture.Click("dialog-ok");
        Assert.AreEqual(version, session.ActiveWorksheet.Version);
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.AreEqual(1, session.History.RedoCount);
    });

    [TestMethod]
    [DataRow("Number", true, true, false, true)]
    [DataRow("Currency", true, true, true, true)]
    [DataRow("Accounting", true, true, true, false)]
    [DataRow("Percentage", true, false, false, false)]
    [DataRow("Scientific", true, false, false, false)]
    [DataRow("Date", false, false, false, false)]
    [DataRow("Custom", false, false, false, false)]
    public Task CategoryShouldEnableOnlyApplicableParameters(string category, bool decimals, bool thousands, bool currency, bool parentheses) => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(CreateSession("General"));
        fixture.SelectCategory(category);
        Assert.AreEqual(decimals, fixture.Find<TextBox>("format-decimals").IsEnabled);
        Assert.AreEqual(thousands, fixture.Find<CheckBox>("format-thousands").IsEnabled);
        Assert.AreEqual(currency, fixture.Find<TextBox>("format-currency").IsEnabled);
        Assert.AreEqual(parentheses, fixture.Find<CheckBox>("format-negative").IsEnabled);
    });

    private static SpreadsheetSession CreateSession(string code)
    {
        var workbook = new Workbook();
        var style = workbook.Styles.Intern(CellStyle.Default with { NumberFormat = new CellNumberFormatStyle { FormatCode = code } });
        workbook.Worksheets[0].SetCells([new KeyValuePair<CellAddress, CellData>(default, new CellData(CellValue.FromObject(1234.5), styleId: style))]);
        return new SpreadsheetSession(workbook);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Window _owner = new() { Width = 950, Height = 750 };
        internal NeraFormatCellsDialog Dialog { get; }
        internal Fixture(SpreadsheetSession session)
        {
            Dialog = new NeraFormatCellsDialog(session);
            _owner.Show();
            _ = Dialog.ShowDialog<bool>(_owner);
            Dialog.UpdateLayout();
        }
        internal string? Category => (Find<ComboBox>("format-category").SelectedItem as ComboBoxItem)?.Tag as string;
        internal void SelectCategory(string name)
        {
            var control = Find<ComboBox>("format-category");
            control.SelectedItem = control.Items.OfType<ComboBoxItem>().Single(item => (string?)item.Tag == name);
        }
        internal T Find<T>(string id) where T : Control => Dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
        internal void Click(string id) => Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Dispose() { Dialog.Dispose(); _owner.Close(); }
    }
}
