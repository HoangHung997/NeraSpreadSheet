using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class DialogFidelity007Tests
{
    [TestMethod]
    public Task ProtectionTabAppliesOnlyTheChosenProtectionProperty() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new Fixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Protection));
        Assert.AreEqual(NeraFormatCellsTab.Protection, ((NeraFormatCellsDialog)fixture.Dialog).SelectedFormatTab);
        var locked = fixture.Find<CheckBox>("format-protection-locked");
        var hidden = fixture.Find<CheckBox>("format-protection-hidden");
        Assert.AreEqual(true, locked.IsChecked);
        Assert.AreEqual(false, hidden.IsChecked);
        hidden.IsChecked = true;
        fixture.Click("dialog-ok");
        var style = session.Styles.ActiveCellStyle;
        Assert.IsTrue(style.Protection.Locked);
        Assert.IsTrue(style.Protection.FormulaHidden);
        Assert.AreEqual(1, session.History.UndoCount);
        session.Undo();
        Assert.IsFalse(session.Styles.ActiveCellStyle.Protection.FormulaHidden);
    });

    [TestMethod]
    public Task OutlinePresetUsesSelectionPerimeterWithoutInventingInteriorEdges() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        using var fixture = new Fixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Border));
        fixture.Click("format-border-preset-outline");
        fixture.Click("dialog-ok");

        var a1 = Style(session, 0, 0).Border;
        var b1 = Style(session, 0, 1).Border;
        var a2 = Style(session, 1, 0).Border;
        var b2 = Style(session, 1, 1).Border;
        Assert.AreEqual(CellBorderLineStyle.Thin, a1.Top.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, a1.Left.Style);
        Assert.AreEqual(CellBorderLineStyle.None, a1.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.None, a1.Bottom.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, b1.Top.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, b1.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, a2.Left.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, a2.Bottom.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, b2.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, b2.Bottom.Style);
        Assert.AreEqual(1, session.History.UndoCount);
        session.Undo();
        Assert.AreEqual(CellBorderLineStyle.None, Style(session, 0, 0).Border.Top.Style);
    });

    [TestMethod]
    public Task InsidePresetStoresSingleSharedSeparators() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        using var fixture = new Fixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Border));
        fixture.Click("format-border-preset-inside");
        fixture.Click("dialog-ok");

        Assert.AreEqual(CellBorderLineStyle.Thin, Style(session, 0, 0).Border.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, Style(session, 0, 0).Border.Bottom.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, Style(session, 0, 1).Border.Bottom.Style);
        Assert.AreEqual(CellBorderLineStyle.Thin, Style(session, 1, 0).Border.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.None, Style(session, 1, 1).Border.Right.Style);
        Assert.AreEqual(CellBorderLineStyle.None, Style(session, 1, 1).Border.Bottom.Style);
    });

    [TestMethod]
    public Task ZoomDialogExposesExcelStylePresetsAndFitSelectionIntent() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var preset = new Fixture(new NeraZoomDialog(1));
        preset.Click("zoom-preset-200");
        preset.Click("dialog-ok");
        Assert.AreEqual(2d, ((NeraZoomDialog)preset.Dialog).Zoom);
        Assert.IsFalse(((NeraZoomDialog)preset.Dialog).FitSelectionRequested);

        using var fit = new Fixture(new NeraZoomDialog(1));
        fit.Click("zoom-fit-selection");
        fit.Click("dialog-ok");
        Assert.IsTrue(((NeraZoomDialog)fit.Dialog).FitSelectionRequested);
    });

    private static CellStyle Style(SpreadsheetSession session, int row, int column) =>
        session.ActiveWorksheet.GetEffectiveStyle(new CellAddress(row, column), session.Workbook.Styles);

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

        internal void Click(string id) =>
            Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        public void Dispose()
        {
            Dialog.Close();
            _owner.Close();
        }
    }
}
