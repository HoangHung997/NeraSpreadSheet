using System.Globalization;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SettingsDialogTests
{
    private static readonly bool[] CancelModes = [false, true];
    [TestMethod]
    [DataRow(NeraFormatCellsTab.Number)]
    [DataRow(NeraFormatCellsTab.Font)]
    [DataRow(NeraFormatCellsTab.Alignment)]
    [DataRow(NeraFormatCellsTab.Border)]
    [DataRow(NeraFormatCellsTab.Fill)]
    public Task FormatDialogShouldOpenTheRequestedTabWithoutMutation(NeraFormatCellsTab tab) => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook()); var version = session.ActiveWorksheet.Version;
        var count = session.Workbook.Styles.Count;
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session, tab));
        Assert.AreEqual(tab, ((NeraFormatCellsDialog)fixture.Dialog).SelectedFormatTab);
        Assert.IsFalse(((NeraFormatCellsDialog)fixture.Dialog).HasPendingChanges);
        fixture.Click("dialog-ok");
        Assert.AreEqual(version, session.ActiveWorksheet.Version); Assert.AreEqual(count, session.Workbook.Styles.Count);
        Assert.AreEqual(0, session.History.UndoCount); Assert.IsFalse(fixture.Dialog.IsVisible);
    });
    [TestMethod]
    public Task NumberDialogShouldApplyRealFormattingWithoutConvertingRawValue() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook()); session.ActiveWorksheet.SetValue(default, 12.5);
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session));
        fixture.Find<TextBox>("format-code").Text = "0.000";
        fixture.Click("dialog-ok");
        Assert.AreEqual("0.000", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(12.5, session.ActiveWorksheet.GetValue(default)); Assert.AreEqual(1, session.History.UndoCount);
        session.Undo(); Assert.AreEqual("General", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        session.Redo(); Assert.AreEqual("0.000", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
    });
    [TestMethod]
    public Task CancelAndEscapeShouldDiscardPendingFormatting() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        foreach (var escape in CancelModes)
        {
            var session = new SpreadsheetSession(new Workbook()); var version = session.ActiveWorksheet.Version;
            using var fixture = new DialogFixture(new NeraFormatCellsDialog(session));
            fixture.Find<TextBox>("format-code").Text = "0.00";
            if (escape) fixture.Dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            else fixture.Click("dialog-cancel");
            Assert.IsFalse(fixture.Dialog.IsVisible); Assert.AreEqual(version, session.ActiveWorksheet.Version);
            Assert.AreEqual(0, session.History.UndoCount);
        }
    });
    [TestMethod]
    public Task InvalidFormatShouldKeepTheDialogOpenAndShowValidation() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session));
        fixture.Find<TextBox>("format-code").Text = "\"unfinished"; fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible); Assert.IsTrue(fixture.Find<TextBlock>("dialog-validation").IsVisible);
        Assert.AreEqual(0, session.History.UndoCount);
        fixture.Find<TextBox>("format-code").Text = "0.00"; fixture.Click("dialog-ok");
        Assert.IsFalse(fixture.Dialog.IsVisible); Assert.AreEqual(1, session.History.UndoCount);
    });
    [TestMethod]
    public Task HostReplacementGuardShouldPreventConfirmingIntoAReplacementSession() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook()); var current = true;
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session, contextIsCurrent: () => current));
        fixture.Find<TextBox>("format-code").Text = "0.00"; current = false; fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible); Assert.AreEqual(0, session.History.UndoCount);
    });
    [TestMethod]
    public Task FontSizeShouldUseHostCultureAndLeaveOtherFieldsUntouched() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Font));
        fixture.Find<TextBox>("format-font-size").Text = "12,5";
        fixture.Click("dialog-ok");
        Assert.AreEqual(12.5, session.Styles.ActiveCellStyle.Font.Size);
        Assert.AreEqual("General", session.Styles.ActiveCellStyle.NumberFormat.FormatCode); Assert.AreEqual(1, session.History.UndoCount);
    });
    [TestMethod]
    public Task AlignmentShouldValidateRotationAndThenApplyOnlyChosenProperties() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var fixture = new DialogFixture(new NeraFormatCellsDialog(session, NeraFormatCellsTab.Alignment));
        fixture.Find<TextBox>("format-align-rotation").Text = "91"; fixture.Click("dialog-ok");
        Assert.IsTrue(fixture.Dialog.IsVisible); Assert.AreEqual(0, session.History.UndoCount);
        fixture.Find<TextBox>("format-align-rotation").Text = "45"; fixture.Find<CheckBox>("format-align-wrap").IsChecked = true; fixture.Click("dialog-ok");
        Assert.AreEqual(45, session.Styles.ActiveCellStyle.Alignment.TextRotationDegrees); Assert.IsTrue(session.Styles.ActiveCellStyle.Alignment.WrapText);
        Assert.AreEqual(1, session.History.UndoCount);
    });
    [TestMethod]
    public Task PageDialogShouldRetainUneditedMarginPrecisionAndCommitOneOperation() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        var initial = new WorksheetPrintSettings { PageSetup = new SpreadsheetPageSetup { Margins = new SpreadsheetPageMargins(.7123456789, .7, .75, .75) } };
        session.ActiveWorksheet.SetPrintSettings(initial);
        using var fixture = new DialogFixture(new NeraPageSetupDialog(session, NeraPageSetupTab.Sheet));
        fixture.Find<CheckBox>("page-gridlines").IsChecked = true; fixture.Click("dialog-ok");
        Assert.IsTrue(session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintGridlines);
        Assert.AreEqual(initial.PageSetup.Margins, session.ActiveWorksheet.GetPrintSettings().PageSetup.Margins);
        Assert.AreEqual(1, session.History.UndoCount); session.Undo(); Assert.IsFalse(session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintGridlines);
    });
    [TestMethod]
    public Task EnglishAndContrastDialogsShouldRemainScopedToTheirWindow() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook()); var original = CultureInfo.CurrentCulture;
        using var first = new DialogFixture(new NeraFormatCellsDialog(session, localization: new PresentationLocalization(CultureInfo.GetCultureInfo("en-US")), theme: NeraIconTheme.HighContrastDark));
        using var second = new DialogFixture(new NeraFormatCellsDialog(session));
        Assert.AreEqual("Format Cells", first.Dialog.Title); Assert.AreEqual("Định dạng ô", second.Dialog.Title);
        Assert.AreEqual(Colors.Black, ((ISolidColorBrush)first.Dialog.Background!).Color);
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)second.Dialog.Background!).Color);
        Assert.AreSame(original, CultureInfo.CurrentCulture); Assert.IsFalse(Application.Current!.Resources.ContainsKey("NeraRibbonSurface"));
    });
    [TestMethod]
    public Task ZoomDialogShouldValidateAndReturnViewOnlyResult() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new DialogFixture(new NeraZoomDialog(1));
        fixture.Find<TextBox>("zoom-percent").Text = "401"; fixture.Click("dialog-ok"); Assert.IsTrue(fixture.Dialog.IsVisible);
        fixture.Find<TextBox>("zoom-percent").Text = "125"; fixture.Click("dialog-ok");
        Assert.AreEqual(1.25, ((NeraZoomDialog)fixture.Dialog).Zoom); Assert.IsFalse(fixture.Dialog.IsVisible);
    });
    [TestMethod]
    public Task LauncherShouldOccupyCaptionCornerWithNativeIdentityAndSingleDispatch() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var handler = new Counter(); var registry = Registry(handler);
        var runtime = new RibbonRuntimeController(Definition(), registry); using var ribbon = new NeraRibbonControl(runtime) { Width = 700 };
        var window = new Window { Width = 740, Height = 300, Content = ribbon }; window.Show(); window.UpdateLayout(); ribbon.Rebuild(); window.UpdateLayout();
        try
        {
            var group = ribbon.LayoutSnapshot.Tabs[0].Groups[0]; var launcher = group.Items.Single(item => item.Presentation.Definition.IsDialogLauncher);
            Assert.AreEqual(group.CaptionY, launcher.Y); Assert.AreEqual(group.CaptionHeight, launcher.Height);
            var button = ribbon.GetVisualDescendants().OfType<Button>().Single(control => AutomationProperties.GetAutomationId(control) == "ribbon-command-Dialog.More");
            Assert.AreEqual("More settings", AutomationProperties.GetName(button)); Assert.IsTrue(button.Focusable);
            var caption = ribbon.GetVisualDescendants().OfType<TextBlock>().Single(control => AutomationProperties.GetAutomationId(control) == "ribbon-group-caption-group");
            Assert.IsTrue(caption.Bounds.Width <= launcher.X / ribbon.LayoutSnapshot.Scale);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.AreEqual(1, handler.Count);
        }
        finally { window.Content = null; window.Close(); }
    });
    [TestMethod]
    public void LauncherGeometryShouldScaleAndSurviveOverflowWithoutOverlap()
    {
        var runtime = new RibbonRuntimeController(Definition(), Registry(new Counter()));
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            var layout = new RibbonResponsiveLayoutEngine().Layout(runtime.Snapshot, new RibbonLayoutRequest(1600 * scale, scale));
            var group = layout.Tabs[0].Groups[0]; var launcher = group.Items.Single(item => item.Presentation.Definition.IsDialogLauncher);
            Assert.AreEqual(18 * scale, launcher.Width, .001); Assert.AreEqual(group.CaptionY, launcher.Y);
            Assert.IsTrue(launcher.X + launcher.Width <= group.Width); Assert.IsTrue(group.Items.Where(item => !item.Presentation.Definition.IsDialogLauncher).All(item => item.Y + item.Height <= group.CaptionY));
        }
        var overflow = new RibbonResponsiveLayoutEngine().Layout(runtime.Snapshot, new RibbonLayoutRequest(1));
        Assert.AreEqual(RibbonGroupLayoutMode.Overflow, overflow.Tabs[0].Groups[0].Mode);
        Assert.IsTrue(overflow.Tabs[0].Groups[0].Items.Any(item => item.Presentation.Command.CommandId.Value == "Dialog.More"));
    }
    [TestMethod]
    public void PresetShouldNotInventLaunchersUntilTheHostRegistersTheirHandlers()
    {
        var registry = new CommandRegistry(); var handler = new Counter(); registry.Register(new CommandDescriptor("Ui.Number", "Number"), handler);
        Assert.IsFalse(NeraSpreadsheetRibbonPreset.Create(registry).Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items).Any(item => item.IsDialogLauncher));
        registry.Register(new CommandDescriptor("Ui.Dialog.Number", "Format Cells", shortcut: "Ctrl+1"), handler);
        var group = NeraSpreadsheetRibbonPreset.Create(registry).Tabs.Single(tab => tab.Id == "home").Groups.Single(group => group.Id == "number");
        Assert.AreEqual(1, group.Items.Count(item => item.IsDialogLauncher)); Assert.AreEqual(0, handler.Count);
    }
    private static RibbonDefinition Definition() => new([new RibbonTabDefinition("home", "Home", [new RibbonGroupDefinition("group", "Group", [new RibbonItemDefinition("Dialog.Command", true), RibbonItemDefinition.DialogLauncher("Dialog.More")])])]);
    private static CommandRegistry Registry(Counter handler)
    {
        var registry = new CommandRegistry(); registry.Register(new CommandDescriptor("Dialog.Command", "Command"), handler);
        registry.Register(new CommandDescriptor("Dialog.More", "More settings"), handler); return registry;
    }
    private sealed class Counter : ICommandHandler
    {
        public int Count { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) { Count++; return ValueTask.CompletedTask; }
    }
    private sealed class DialogFixture : IDisposable
    {
        public Window Owner { get; } = new() { Width = 900, Height = 700 };
        public NeraSettingsDialog Dialog { get; }
        public DialogFixture(NeraSettingsDialog dialog)
        { Dialog = dialog; Owner.Show(); _ = Dialog.ShowDialog<bool>(Owner); Dialog.UpdateLayout(); }
        public T Find<T>(string id) where T : Control => Dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
        public void Click(string id) => Find<Button>(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Dispose() { Dialog.Close(); Owner.Close(); }
    }
}
