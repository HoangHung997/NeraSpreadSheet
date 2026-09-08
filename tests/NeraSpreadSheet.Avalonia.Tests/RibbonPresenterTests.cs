using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class RibbonPresenterTests
{
    [TestMethod]
    public Task RibbonShouldProjectNativeCommandKindsWithoutExecuting() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture();
        Assert.IsInstanceOfType<Button>(fixture.Find("ribbon-command-Test.Button"));
        Assert.IsInstanceOfType<ToggleButton>(fixture.Find("ribbon-command-Test.Toggle"));
        Assert.IsInstanceOfType<ComboBox>(fixture.Find("ribbon-command-Test.Combo"));
        Assert.IsInstanceOfType<ComboBox>(fixture.Find("ribbon-command-Test.Color"));
        Assert.IsInstanceOfType<Button>(fixture.Find("ribbon-command-Test.Split-primary"));
        Assert.IsInstanceOfType<Button>(fixture.Find("ribbon-command-Test.Menu"));
        Assert.IsNotNull(fixture.Find("ribbon-command-Test.Gallery"));
        Assert.AreEqual(0, fixture.Handler.Count);
    });
    [TestMethod]
    public Task QatAndPrimaryButtonShouldDispatchTheSameCommand() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture();
        ((Button)fixture.Find("ribbon-qat-Test.Button")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((Button)fixture.Find("ribbon-command-Test.Button")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(2, fixture.Handler.Count);
    });
    [TestMethod]
    public Task ChoiceShouldKeepSelectedValueSeparateFromHostContext() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new RibbonFixture();
        Assert.IsTrue(await fixture.Ribbon.ActivateChoiceAsync("Test.Combo", "a"));
        Assert.IsInstanceOfType<RibbonItemActivation>(fixture.Handler.LastContext.Parameter);
        Assert.AreEqual(1, fixture.Handler.Count);
    });
    [TestMethod]
    public Task DisabledOrUnknownChoiceShouldNotExecute() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new RibbonFixture();
        Assert.IsFalse(await fixture.Ribbon.ActivateChoiceAsync("Test.Combo", "disabled"));
        Assert.IsFalse(await fixture.Ribbon.ActivateChoiceAsync("Test.Combo", "not-a-choice"));
        Assert.AreEqual(0, fixture.Handler.Count);
    });
    [TestMethod]
    public Task ContextualTabShouldFollowSharedTableContext() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture();
        Assert.IsFalse(fixture.Ribbon.SelectTab("table-design"));
        fixture.Runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        Assert.IsTrue(fixture.Ribbon.SelectTab("table-design"));
        fixture.Runtime.SetSelectionContext(new RibbonSelectionContext(true, false));
        fixture.Ribbon.Rebuild(); Assert.AreEqual("home", fixture.Ribbon.SelectedTabId);
    });
    [TestMethod]
    public Task BackstageShouldRequireExplicitExecution() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture(); fixture.Ribbon.OpenBackstage(); fixture.Window.UpdateLayout();
        Assert.IsTrue(fixture.Ribbon.IsBackstageOpen); Assert.AreEqual(0, fixture.Handler.Count);
        ((Button)fixture.Find("ribbon-backstage-execute-Test.Button")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(1, fixture.Handler.Count); Assert.IsFalse(fixture.Ribbon.IsBackstageOpen);
    });
    [TestMethod]
    public Task MinimizedStateShouldUseSharedRuntimeAndSurviveRebuild() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture(); fixture.Ribbon.IsMinimized = true; fixture.Ribbon.Rebuild();
        Assert.IsTrue(fixture.Runtime.IsMinimized); Assert.IsNull(((TabItem)fixture.Ribbon.NativeTabControl.SelectedItem!).Content);
        fixture.Ribbon.IsMinimized = false; fixture.Ribbon.Rebuild();
        Assert.IsNotNull(((TabItem)fixture.Ribbon.NativeTabControl.SelectedItem!).Content);
    });
    [TestMethod]
    public Task NarrowRibbonShouldRetainCommandsInOverflow() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture(); fixture.Ribbon.Width = 100; fixture.Window.UpdateLayout(); fixture.Ribbon.Rebuild(); fixture.Window.UpdateLayout();
        Assert.IsTrue(fixture.Ribbon.LayoutSnapshot.Tabs[0].HasOverflow); Assert.IsNotNull(fixture.Find("ribbon-overflow"));
    });
    [TestMethod]
    public Task SharedRibbonAndBarShortcutShouldExecuteOnce() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture();
        using var bar = new NeraBarPresenter(new BarRuntimeController(new BarDefinition("test-bar", BarKind.Toolbar, [BarItemDefinition.Command("Test.Button")]), fixture.Registry));
        using var ribbonKeys = fixture.Ribbon.BindShortcuts(fixture.Window); using var barKeys = bar.BindShortcuts(fixture.Window);
        fixture.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control });
        Assert.AreEqual(1, fixture.Handler.Count);
    });
    [TestMethod]
    public Task KeyTipsShouldEnterAndEscapeWithoutExecutingACommand() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture(); fixture.Ribbon.EnterKeyTipMode(); Assert.AreEqual(RibbonKeyTipScope.Tabs, fixture.Ribbon.KeyTipScope);
        fixture.Ribbon.EscapeKeyTipMode(); Assert.AreEqual(RibbonKeyTipScope.Inactive, fixture.Ribbon.KeyTipScope); Assert.AreEqual(0, fixture.Handler.Count);
    });
    [TestMethod]
    public Task HeightOnlyArrangeShouldNotReplaceNativeCommandTree() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new RibbonFixture(); var tab = fixture.Ribbon.NativeTabControl.SelectedItem;
        fixture.Ribbon.Arrange(new Rect(0, 0, fixture.Ribbon.Bounds.Width, fixture.Ribbon.Bounds.Height + 2));
        Assert.AreSame(tab, fixture.Ribbon.NativeTabControl.SelectedItem);
    });
    private sealed class RibbonFixture : IDisposable
    {
        public RibbonFixture()
        {
            Registry = new CommandRegistry(); Handler = new TestHandler(); var items = new List<RibbonItemDefinition>();
            Add("Button", RibbonItemKind.Button); Add("Toggle", RibbonItemKind.Toggle); Add("Combo", RibbonItemKind.ComboBox);
            Add("Color", RibbonItemKind.ColorPicker); Add("Split", RibbonItemKind.SplitButton); Add("Menu", RibbonItemKind.Menu); Add("Gallery", RibbonItemKind.Gallery);
            var definition = new RibbonDefinition(
                [new RibbonTabDefinition("home", "Trang đầu", [new RibbonGroupDefinition("commands", "Lệnh", items)]),
                 new RibbonTabDefinition("table-design", "Thiết kế bảng", [new RibbonGroupDefinition("table", "Bảng", [new RibbonItemDefinition("Test.Button")])])],
                [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
                [new RibbonCommandSurfaceItem("Test.Button", "1")], [new RibbonCommandSurfaceItem("Test.Button", "S")]);
            Runtime = new RibbonRuntimeController(definition, Registry); Ribbon = new NeraRibbonControl(Runtime) { Width = 1200 };
            Window = new Window { Width = 1240, Height = 400, Content = Ribbon };
            Window.Show(); Window.UpdateLayout(); Ribbon.Rebuild(); Window.UpdateLayout();
            return;
            void Add(string name, RibbonItemKind kind)
            {
                var id = "Test." + name; Registry.Register(new CommandDescriptor(id, name, shortcut: name == "Button" ? "Ctrl+S" : null), Handler);
                items.Add(new RibbonItemDefinition(id, kind));
            }
        }
        public TestHandler Handler { get; }
        public CommandRegistry Registry { get; }
        public RibbonRuntimeController Runtime { get; }
        public NeraRibbonControl Ribbon { get; }
        public Window Window { get; }
        public Control Find(string id) => Ribbon.GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetAutomationId(control) == id);
        public void Dispose() { Window.Content = null; Window.Close(); Ribbon.Dispose(); }
    }
    private sealed class TestHandler : IStatefulCommandHandler
    {
        private static readonly CommandItem[] Choices = [new("a", "Lựa chọn A"), new("disabled", "Đã vô hiệu hóa", false)];
        public int Count { get; private set; }
        public CommandContext LastContext { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public CommandState GetState(CommandContext context) => new(true, false, null, "a", Choices);
        public ValueTask ExecuteAsync(CommandContext context) { Count++; LastContext = context; return ValueTask.CompletedTask; }
    }
}
