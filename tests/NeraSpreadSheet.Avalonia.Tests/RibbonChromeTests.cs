using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class RibbonChromeTests
{
    [TestMethod]
    public Task TabsShouldHaveRibbonDensityInsteadOfPageHeadingDefaults() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var tab = (TabItem)fixture.Ribbon.NativeTabControl.SelectedItem!;
        Assert.AreEqual(13d, tab.FontSize);
        Assert.IsTrue(tab.Bounds.Height is >= 30 and <= 38, $"Unexpected tab height: {tab.Bounds.Height}");
        Assert.IsTrue(fixture.Ribbon.Bounds.Height <= 170, $"Unexpected Ribbon height: {fixture.Ribbon.Bounds.Height}");
    });

    [TestMethod]
    public Task CommandsShouldUseScopedTransparentChrome() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var button = (Button)fixture.Find("ribbon-command-Chrome.Action");
        Assert.AreEqual(Colors.Transparent, ((ISolidColorBrush)button.Background!).Color);
        Assert.AreEqual(0d, button.MinHeight);
        Assert.IsNotNull(button.GetVisualDescendants().OfType<Border>().SingleOrDefault(part => part.Name == "PART_Chrome"));
        Assert.AreEqual(0, fixture.Handler.Count);
    });

    [TestMethod]
    public Task NativeComboShouldFitTheSharedTwentyFourDipSlot() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var combo = (ComboBox)fixture.Find("ribbon-command-Chrome.Choice");
        Assert.AreEqual(0d, combo.MinHeight);
        Assert.IsTrue(combo.Bounds.Height <= 24.01);
        combo.IsDropDownOpen = true; fixture.Window.UpdateLayout();
        Assert.IsTrue(combo.IsDropDownOpen);
        Assert.AreEqual(2, combo.ItemCount);
        combo.IsDropDownOpen = false;
    });

    [TestMethod]
    public Task HighContrastPalettesShouldNotCollapseToOrdinaryDarkAndLight() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Ribbon.IconTheme = NeraIconTheme.HighContrastDark; fixture.Window.UpdateLayout();
        Assert.AreEqual(Colors.Black, ((ISolidColorBrush)fixture.Ribbon.Background!).Color);
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)fixture.Ribbon.Foreground!).Color);
        var resources = fixture.Ribbon.Resources.MergedDictionaries.OfType<NeraRibbonResources>().Single();
        Assert.AreEqual(Color.Parse("#FFEF00"), ((ISolidColorBrush)resources.Brush("Accent")).Color);
        fixture.Ribbon.IconTheme = NeraIconTheme.HighContrastLight; fixture.Window.UpdateLayout();
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)fixture.Ribbon.Background!).Color);
        Assert.AreEqual(Color.Parse("#0035B2"), ((ISolidColorBrush)resources.Brush("Accent")).Color);
    });

    [TestMethod]
    public Task TwoRibbonsShouldNotShareMutablePaletteOrChangeApplicationResources() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var first = new Fixture(); using var second = new Fixture();
        first.Ribbon.IconTheme = NeraIconTheme.Dark;
        Assert.AreEqual(Colors.White, ((ISolidColorBrush)second.Ribbon.Background!).Color);
        Assert.AreEqual(Color.Parse("#252525"), ((ISolidColorBrush)first.Ribbon.Background!).Color);
        Assert.IsFalse(Application.Current!.Resources.ContainsKey("NeraRibbonSurface"));
        Assert.AreNotSame(first.Ribbon.Resources.MergedDictionaries[0], second.Ribbon.Resources.MergedDictionaries[0]);
    });

    [TestMethod]
    public Task RepeatedThemeChangesShouldNotAccumulateResourceDictionaries() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        for (var iteration = 0; iteration < 10; iteration++)
        {
            fixture.Ribbon.IconTheme = NeraIconTheme.Dark;
            fixture.Ribbon.IconTheme = NeraIconTheme.Light;
        }
        Assert.HasCount(1, fixture.Ribbon.Resources.MergedDictionaries);
        Assert.AreEqual("home", fixture.Ribbon.SelectedTabId);
        Assert.AreEqual(0, fixture.Handler.Count);
    });

    [TestMethod]
    public Task StyledToggleShouldRetainNativeTypeAndExactlyOnceDispatch() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var button = (ToggleButton)fixture.Find("ribbon-command-Chrome.Toggle");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(1, fixture.Handler.Count);
        Assert.AreEqual("Chrome.Toggle", fixture.Handler.Last.Value);
    });

    [TestMethod]
    public Task RebuildShouldPreserveCallerFocusOutsideRibbon() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Outside.Focus();
        fixture.Ribbon.IconTheme = NeraIconTheme.Dark;
        fixture.Ribbon.Rebuild(); fixture.Window.UpdateLayout();
        Assert.IsTrue(fixture.Outside.IsFocused);
    });

    [TestMethod]
    public Task NativeSlotsShouldMatchSharedGeometryAcrossSupportedWidths() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        foreach (var width in new[] { 1024d, 1280d, 1600d })
        {
            fixture.Window.Width = width; fixture.Ribbon.Width = width;
            fixture.Window.UpdateLayout(); fixture.Ribbon.Rebuild(); fixture.Window.UpdateLayout();
            foreach (var group in fixture.Ribbon.LayoutSnapshot.Tabs[0].Groups.Where(group => group.Mode != RibbonGroupLayoutMode.Overflow))
            foreach (var item in group.Items)
            {
                var control = fixture.Find("ribbon-command-" + item.Presentation.Command.CommandId.Value);
                Assert.AreEqual(item.Height / fixture.Ribbon.LayoutSnapshot.Scale, control.Bounds.Height, 0.51);
                Assert.AreEqual(item.Width / fixture.Ribbon.LayoutSnapshot.Scale, control.Bounds.Width, 0.51);
            }
        }
    });

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            var registry = new CommandRegistry(); Handler = new Handler();
            foreach (var id in new[] { "Chrome.Action", "Chrome.Toggle", "Chrome.Choice" })
                registry.Register(new CommandDescriptor(id, id, iconKey: "file.save"), new BoundHandler(Handler, id));
            var definition = new RibbonDefinition(
                [new RibbonTabDefinition("home", "Trang đầu", [new RibbonGroupDefinition("test", "Định dạng",
                    [new RibbonItemDefinition("Chrome.Action", IsLarge: true), new RibbonItemDefinition("Chrome.Toggle", RibbonItemKind.Toggle),
                     new RibbonItemDefinition("Chrome.Choice", RibbonItemKind.ComboBox)])])], [],
                [new RibbonCommandSurfaceItem("Chrome.Action", "1")], [new RibbonCommandSurfaceItem("Chrome.Action", "S")]);
            Runtime = new RibbonRuntimeController(definition, registry);
            Ribbon = new NeraRibbonControl(Runtime) { Width = 1200 };
            Outside = new TextBox { Text = "External editor" };
            var root = new StackPanel(); root.Children.Add(Ribbon); root.Children.Add(Outside);
            Window = new Window { Width = 1240, Height = 400, Content = root };
            Window.Show(); Window.UpdateLayout(); Ribbon.Rebuild(); Window.UpdateLayout();
        }
        public Handler Handler { get; }
        public RibbonRuntimeController Runtime { get; }
        public NeraRibbonControl Ribbon { get; }
        public TextBox Outside { get; }
        public Window Window { get; }
        public Control Find(string id) => Ribbon.GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetAutomationId(control) == id);
        public void Dispose() { Window.Content = null; Window.Close(); Ribbon.Dispose(); }
    }
    private sealed class Handler { public int Count { get; set; } public CommandId Last { get; set; } }
    private sealed class BoundHandler(Handler owner, CommandId id) : IStatefulCommandHandler
    {
        private static readonly CommandItem[] Choices = [new("a", "Lựa chọn A"), new("b", "Lựa chọn B")];
        public bool CanExecute(CommandContext context) => true;
        public CommandState GetState(CommandContext context) => new(true, false, null, "a", Choices);
        public ValueTask ExecuteAsync(CommandContext context) { owner.Count++; owner.Last = id; return ValueTask.CompletedTask; }
    }
}
