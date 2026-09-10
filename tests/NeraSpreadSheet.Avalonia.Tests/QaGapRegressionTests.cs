using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class QaGapRegressionTests
{
    [TestMethod]
    public Task AltEnterShouldNotEnterRibbonKeyTipsButBareAltStillShould() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("Test.Command", "Test"), new Handler());
        var runtime = new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Home", [new RibbonGroupDefinition("g", "G", [new RibbonItemDefinition("Test.Command")])])
        ]), registry);
        using var ribbon = new NeraRibbonControl(runtime);
        var editor = new TextBox { AcceptsReturn = true, Text = "=SUM(" };
        var root = new DockPanel(); DockPanel.SetDock(ribbon, Dock.Top); root.Children.Add(ribbon); root.Children.Add(editor);
        var window = new Window { Width = 800, Height = 400, Content = root };
        using var binding = ribbon.BindShortcuts(window);
        try
        {
            window.Show(); window.UpdateLayout(); editor.Focus();
            var altDown = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.LeftAlt };
            editor.RaiseEvent(altDown);
            Assert.AreEqual(RibbonKeyTipScope.Inactive, ribbon.KeyTipScope, "Alt-down alone must wait for release before entering key tips.");

            var enter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Alt };
            editor.RaiseEvent(enter);
            Assert.AreEqual(RibbonKeyTipScope.Inactive, ribbon.KeyTipScope, "Alt+Enter must remain an editor chord, not a Ribbon chord.");
            var altUpAfterChord = new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.LeftAlt };
            editor.RaiseEvent(altUpAfterChord);
            Assert.AreEqual(RibbonKeyTipScope.Inactive, ribbon.KeyTipScope, "Releasing Alt after a chord must not toggle key tips.");

            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.LeftAlt });
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.LeftAlt });
            Assert.AreEqual(RibbonKeyTipScope.Tabs, ribbon.KeyTipScope, "A bare Alt press/release still opens key tips.");
            ribbon.EscapeKeyTipMode();
            Assert.AreEqual(RibbonKeyTipScope.Inactive, ribbon.KeyTipScope);
        }
        finally { window.Content = null; window.Close(); }
    });

    [TestMethod]
    public Task DynamicZoomValueShouldRemainVisibleWhenItIsNotAPresetChoice() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("Ui.Zoom", "Zoom"), new ZoomHandler());
        var definition = new RibbonDefinition([
            new RibbonTabDefinition("view", "View", [new RibbonGroupDefinition("zoom", "Zoom", [new RibbonItemDefinition("Ui.Zoom", RibbonItemKind.ComboBox)])])
        ]);
        var runtime = new RibbonRuntimeController(definition, registry);
        using var ribbon = new NeraRibbonControl(runtime) { Width = 500 };
        var window = new Window { Width = 540, Height = 220, Content = ribbon };
        try
        {
            window.Show(); window.UpdateLayout(); ribbon.Rebuild(); window.UpdateLayout();
            var combo = ribbon.GetVisualDescendants().OfType<ComboBox>().Single(control =>
                AutomationProperties.GetAutomationId(control) == "ribbon-command-Ui.Zoom");
            var selected = combo.SelectedItem as ComboBoxItem;
            Assert.IsNotNull(selected);
            Assert.AreEqual("110", selected.Tag);
            Assert.AreEqual("110%", selected.Content);
        }
        finally { window.Content = null; window.Close(); }
    });

    [TestMethod]
    public Task AvaloniaSplitHostShouldRestoreSelectionScrollAndZoomPerWorksheetWithoutHistory() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0]; workbook.RenameWorksheet(first, "A");
        var second = workbook.AddWorksheet("B");
        var session = new SpreadsheetSession(workbook);
        using var split = new NeraSpreadsheetSplitControl { Session = session };
        using var binding = new NeraWorksheetViewStateBinding(split);
        var window = new Window { Width = 900, Height = 600, Content = split };
        try
        {
            window.Show(); window.UpdateLayout();
            session.Selection.SetActiveCell(new CellAddress(9, 4));
            split.SetZoom(1.35);
            split.ActiveSpreadsheet.ScrollTo(31.125, 57.375);
            var firstState = session.View.GetWorksheetState(first);
            Assert.AreEqual(1.35, firstState.Zoom, 1e-9);
            Assert.AreEqual(31.125, firstState.OffsetX, 1e-9);
            Assert.AreEqual(57.375, firstState.OffsetY, 1e-9);

            session.ActivateWorksheet(second); window.UpdateLayout();
            session.Selection.SetActiveCell(new CellAddress(15, 10));
            split.SetZoom(0.85);
            split.ActiveSpreadsheet.ScrollTo(409.25, 917.625);
            var secondState = session.View.GetWorksheetState(second);
            Assert.AreEqual(0.85, secondState.Zoom, 1e-9);

            session.ActivateWorksheet(first); window.UpdateLayout();
            Assert.AreEqual(new CellAddress(9, 4), session.Selection.ActiveCell);
            Assert.AreEqual(1.35, split.ActiveSpreadsheet.Zoom, 1e-9);
            Assert.AreEqual(firstState.OffsetX, split.ActiveSpreadsheet.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(firstState.OffsetY, split.ActiveSpreadsheet.ScrollSnapshot.OffsetY, 1e-9);

            session.ActivateWorksheet(second); window.UpdateLayout();
            Assert.AreEqual(new CellAddress(15, 10), session.Selection.ActiveCell);
            Assert.AreEqual(0.85, split.ActiveSpreadsheet.Zoom, 1e-9);
            Assert.AreEqual(secondState.OffsetX, split.ActiveSpreadsheet.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(secondState.OffsetY, split.ActiveSpreadsheet.ScrollSnapshot.OffsetY, 1e-9);
            Assert.AreEqual(0, session.History.UndoCount);
        }
        finally { window.Content = null; window.Close(); }
    });

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class ZoomHandler : IStatefulCommandHandler
    {
        private static readonly CommandItem[] Items = [new("100", "100%"), new("125", "125%")];
        public bool CanExecute(CommandContext context) => true;
        public CommandState GetState(CommandContext context) => new(true, null, null, "110", Items);
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
