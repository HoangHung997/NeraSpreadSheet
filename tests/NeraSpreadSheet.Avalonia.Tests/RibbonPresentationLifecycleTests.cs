using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class RibbonPresentationLifecycleTests
{
    [TestMethod]
    public Task OnlySelectedTabShouldHaveANativeBody() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        Assert.HasCount(6, fixture.Ribbon.NativeTabControl.Items);
        Assert.AreEqual(1, fixture.Bodies);
        var before = fixture.Ribbon.NativeBodyBuildCount;
        fixture.Ribbon.Rebuild();
        Assert.AreEqual(before + 1, fixture.Ribbon.NativeBodyBuildCount);
        Assert.AreEqual(1, fixture.Bodies);
        Assert.AreEqual(0, fixture.Handler.Count);
    });

    [TestMethod]
    public Task NativeTabSelectionShouldReleaseOldBodyAndSynchronizeLayoutIdentity() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var first = (TabItem)fixture.Ribbon.NativeTabControl.Items[0]!;
        var body = first.Content;
        fixture.Ribbon.NativeTabControl.SelectedIndex = 4;
        fixture.Window.UpdateLayout();
        Assert.IsNull(first.Content);
        Assert.AreEqual("tab4", fixture.Ribbon.SelectedTabId);
        Assert.AreEqual("tab4", fixture.Ribbon.LayoutSnapshot.SelectedTabId);
        Assert.AreEqual(1, fixture.Bodies);
        var selected = (TabItem)fixture.Ribbon.NativeTabControl.SelectedItem!;
        Assert.AreNotSame(body, selected.Content);
        Assert.IsTrue(fixture.Ribbon.GetVisualDescendants().OfType<Control>().Any(control => AutomationProperties.GetAutomationId(control) == "ribbon-command-Action4"));
        Assert.AreEqual(0, fixture.Handler.Count);
    });

    [TestMethod]
    public Task InactiveTabCommandShouldStillResolveItsShortcutWithoutMaterializingTheTab() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var before = fixture.Ribbon.NativeBodyBuildCount;
        // This fixture deliberately has a synchronous CompletedTask handler.
        // Never convert the Action-based UI test callback to async void.
        var activation = fixture.Ribbon.TryActivateShortcutAsync("Ctrl+F9");
        Assert.IsTrue(activation.IsCompletedSuccessfully);
        Assert.IsTrue(activation.Result);
        Assert.AreEqual(1, fixture.Handler.Count);
        Assert.AreEqual("tab0", fixture.Ribbon.SelectedTabId);
        Assert.IsNull(((TabItem)fixture.Ribbon.NativeTabControl.Items[5]!).Content);
        Assert.IsTrue(fixture.Ribbon.NativeBodyBuildCount <= before + 1);
    });

    [TestMethod]
    public Task MinimizedAndBackstageShouldNotRetainInvisibleCommandBodies() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Ribbon.IsMinimized = true; fixture.Ribbon.Rebuild();
        Assert.AreEqual(0, fixture.Bodies);
        fixture.Ribbon.IsMinimized = false; fixture.Ribbon.OpenBackstage();
        Assert.AreEqual(0, fixture.Bodies);
        fixture.Ribbon.CloseBackstage();
        Assert.AreEqual(1, fixture.Bodies);
        Assert.AreEqual(0, fixture.Handler.Count);
    });

    [TestMethod]
    public Task ThemeRefreshShouldBuildOnlyOneBodyAndPreserveExternalFocus() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Outside.Focus();
        var before = fixture.Ribbon.NativeBodyBuildCount;
        fixture.Ribbon.IconTheme = NeraIconTheme.Dark;
        Assert.AreEqual(before + 1, fixture.Ribbon.NativeBodyBuildCount);
        Assert.IsTrue(fixture.Outside.IsFocused);
        Assert.AreEqual(1, fixture.Bodies);
    });

    [TestMethod]
    public Task ChromeAffordancesShouldUseCatalogIconsRatherThanPlatformEmoji() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var customize = fixture.Find("ribbon-customize");
        var minimize = fixture.Find("ribbon-minimize");
        Assert.IsInstanceOfType<Image>(((Button)customize).Content);
        Assert.IsInstanceOfType<Image>(((Button)minimize).Content);
        Assert.IsNotNull(ToolTip.GetTip(minimize));
    });

    [TestMethod]
    public Task ChangingIconResolverShouldRefreshAnUnknownCommandIconWithoutChangingCommandState() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        using var images = new NeraAvaloniaIconProvider();
        var initial = fixture.Find("ribbon-command-Action0");
        Assert.IsFalse(initial.GetVisualDescendants().OfType<Image>().Any());
        fixture.Ribbon.IconRequestResolver = request => request.IconKey == "customer.action"
            ? images.Resolve(new NeraIconRequest("file.save", 16, request.Theme)) : null;
        fixture.Window.UpdateLayout();
        Assert.IsTrue(fixture.Find("ribbon-command-Action0").GetVisualDescendants().OfType<Image>().Any());
        Assert.AreEqual(0, fixture.Handler.Count);
        fixture.Ribbon.IconRequestResolver = null;
        fixture.Window.UpdateLayout();
        Assert.IsFalse(fixture.Find("ribbon-command-Action0").GetVisualDescendants().OfType<Image>().Any());
    });

    [TestMethod]
    public Task ToolbarSubmenuShouldResolveEachChildIconOnlyOnce() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var definition = new BarDefinition("toolbar", BarKind.Toolbar,
            [BarItemDefinition.Submenu("Lệnh", [BarItemDefinition.Command("Action1")], "commands")]);
        using var bar = new NeraBarPresenter(new BarRuntimeController(definition, fixture.Registry));
        var resolves = 0;
        bar.IconRequestResolver = _ => { resolves++; return null; };
        bar.Rebuild();
        Assert.AreEqual(1, resolves);
        Assert.HasCount(1, bar.NativeControl.Resources.MergedDictionaries);
    });

    [TestMethod]
    public Task StandaloneBarShouldHaveAnIndependentRibbonPalette() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        using var bar = new NeraBarPresenter(new BarRuntimeController(new BarDefinition("bar", BarKind.Toolbar,
            [BarItemDefinition.Command("Action1")]), fixture.Registry));
        bar.IconTheme = NeraIconTheme.HighContrastDark;
        var resources = bar.NativeControl.Resources.MergedDictionaries.OfType<NeraRibbonResources>().Single();
        Assert.AreEqual(Colors.Black, ((ISolidColorBrush)resources.Brush("Surface")).Color);
        Assert.AreEqual(Color.Parse("#FFEF00"), ((ISolidColorBrush)resources.Brush("Accent")).Color);
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)fixture.Ribbon.Background!).Color);
        Assert.IsFalse(Application.Current!.Resources.ContainsKey("NeraRibbonSurface"));
    });

    [TestMethod]
    public Task CustomizationThemeShouldNotCreateProfileEditsOrMutateOtherPresenters() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var editor = new NeraRibbonCustomizationControl(fixture.Runtime);
        var original = editor.ExportJson();
        editor.IconTheme = NeraIconTheme.HighContrastDark;
        Assert.AreEqual(Colors.Black, ((ISolidColorBrush)editor.Background!).Color);
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)editor.Foreground!).Color);
        Assert.IsFalse(editor.HasChanges);
        Assert.AreEqual(original, editor.ExportJson());
        Assert.IsNull(editor.Apply());
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)fixture.Ribbon.Background!).Color);
        Assert.HasCount(1, editor.Resources.MergedDictionaries);
    });

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Registry = new CommandRegistry(); Handler = new Counter();
            var tabs = new List<RibbonTabDefinition>();
            for (var index = 0; index < 6; index++)
            {
                var id = "Action" + index;
                Registry.Register(new CommandDescriptor(id, "Lệnh " + index,
                    iconKey: index == 0 ? "customer.action" : "file.save", shortcut: index == 5 ? "Ctrl+F9" : null), Handler);
                tabs.Add(new RibbonTabDefinition("tab" + index, "Trang " + index,
                    [new RibbonGroupDefinition("group" + index, "Nhóm " + index, [new RibbonItemDefinition(id, IsLarge: true)])]));
            }
            Runtime = new RibbonRuntimeController(new RibbonDefinition(tabs, [], [], [new RibbonCommandSurfaceItem("Action1", "S")]), Registry);
            Ribbon = new NeraRibbonControl(Runtime) { Width = 1100 };
            Outside = new TextBox { Text = "Caller editor" };
            var panel = new StackPanel(); panel.Children.Add(Ribbon); panel.Children.Add(Outside);
            Window = new Window { Width = 1140, Height = 450, Content = panel };
            Window.Show(); Window.UpdateLayout(); Ribbon.Rebuild(); Window.UpdateLayout();
        }
        public CommandRegistry Registry { get; }
        public Counter Handler { get; }
        public RibbonRuntimeController Runtime { get; }
        public NeraRibbonControl Ribbon { get; }
        public TextBox Outside { get; }
        public Window Window { get; }
        public int Bodies => Ribbon.NativeTabControl.Items.OfType<TabItem>().Count(tab => tab.Content is not null);
        public Control Find(string id) => Ribbon.GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetAutomationId(control) == id);
        public void Dispose() { Window.Content = null; Window.Close(); Ribbon.Dispose(); }
    }
    private sealed class Counter : ICommandHandler
    {
        public int Count { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) { Count++; return ValueTask.CompletedTask; }
    }
}
