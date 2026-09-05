using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfRibbonVisualRegressionTests
{
    private static readonly double[] ReferenceWidths = [1536d, 1280d, 1024d, 820d];
    [TestMethod]
    [Timeout(120_000)]
    public void LoadedRibbonShouldHonorDenseBoundsBottomCaptionsAndThemeAtReferenceWidths()
    {
        RunInSta(() =>
        {
            using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(CreateRuntime());
            var window = CreateWindow(ribbon);
            try
            {
                window.Show();
                foreach (var theme in Enum.GetValues<NeraIconTheme>())
                {
                    ribbon.IconTheme = theme;
                    foreach (var width in ReferenceWidths)
                    {
                        ribbon.Width = width;
                        window.Width = width + 32d;
                        Flush(window);
                        ribbon.Rebuild();
                        Flush(window);
                        var tab = ribbon.LayoutSnapshot.Tabs[0];
                        var scale = ribbon.LayoutSnapshot.Scale;
                        foreach (var group in tab.Groups.Where(static group => group.Mode != RibbonGroupLayoutMode.Overflow))
                        {
                            var native = Descendants<System.Windows.Controls.GroupBox>(ribbon).Single(control =>
                                AutomationProperties.GetAutomationId(control) == $"ribbon-group-{group.Presentation.Id}");
                            var canvas = (Canvas)native.Content;
                            var caption = canvas.Children.OfType<TextBlock>().Single();
                            Assert.AreEqual(group.CaptionY / scale, Canvas.GetTop(caption), 0.01d);
                            Assert.AreEqual(group.CaptionHeight / scale, caption.Height, 0.01d);
                            foreach (var item in group.Items)
                            {
                                var control = canvas.Children.OfType<FrameworkElement>().Single(element =>
                                    AutomationProperties.GetAutomationId(element) == $"ribbon-command-{item.Presentation.Command.CommandId.Value}");
                                Assert.AreEqual(item.X / scale, Canvas.GetLeft(control), 0.01d);
                                Assert.AreEqual(item.Y / scale, Canvas.GetTop(control), 0.01d);
                                Assert.AreEqual(item.Width / scale, control.ActualWidth, 1d / scale);
                                Assert.AreEqual(item.Height / scale, control.ActualHeight, 1d / scale);
                                Assert.IsTrue(Canvas.GetTop(control) + control.ActualHeight <= Canvas.GetTop(caption) + (1d / scale));
                                Assert.AreEqual(new Thickness(0d), control.Margin);
                            }
                            var commandBounds = canvas.Children.OfType<FrameworkElement>()
                                .Where(static element => element is not TextBlock)
                                .Select(element => new Rect(Canvas.GetLeft(element), Canvas.GetTop(element), element.ActualWidth, element.ActualHeight))
                                .ToArray();
                            for (var first = 0; first < commandBounds.Length; first++)
                            {
                                for (var second = first + 1; second < commandBounds.Length; second++)
                                {
                                    Assert.IsFalse(commandBounds[first].IntersectsWith(commandBounds[second]),
                                        $"Native command bounds overlap in {group.Presentation.Id} at {width} DIP ({theme}).");
                                }
                            }
                        }
                        var qat = Descendants<WpfButton>(ribbon).Single(button =>
                            AutomationProperties.GetAutomationId(button) == "ribbon-qat-edit.copy");
                        Assert.AreEqual(28d, qat.ActualWidth, 0.01d);
                        Assert.AreEqual(16d, Descendants<System.Windows.Controls.Image>(qat).Single().Width);
                        var primary = Descendants<WpfButton>(ribbon).Single(button =>
                            AutomationProperties.GetAutomationId(button) == "ribbon-command-edit.paste");
                        var primaryCaption = Descendants<TextBlock>(primary).Single();
                        Assert.AreEqual(TextWrapping.Wrap, primaryCaption.TextWrapping);
                        Assert.AreEqual(28d, primaryCaption.MaxHeight);
                        Assert.AreEqual(32d, Descendants<System.Windows.Controls.Image>(primary).Single().Width);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void GalleryShouldPageVisualTilesAndResizeShouldKeepWorksheetFocus()
    {
        RunInSta(() =>
        {
            var runtime = CreateRuntime(out var galleryHandler);
            using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime) { Width = 1280d };
            var worksheetEditor = new WpfTextBox { Text = "Nội dung ô", Margin = new Thickness(16d) };
            var content = new DockPanel();
            DockPanel.SetDock(ribbon, Dock.Top);
            content.Children.Add(ribbon);
            content.Children.Add(worksheetEditor);
            var window = CreateWindow(content);
            try
            {
                window.Show();
                Flush(window);
                var next = Descendants<WpfButton>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-next");
                var scroll = Descendants<ScrollViewer>(ribbon).Single(control =>
                    AutomationProperties.GetAutomationId(control) == "ribbon-command-table.styles-viewport");
                Assert.AreEqual(ScrollBarVisibility.Hidden, scroll.HorizontalScrollBarVisibility);
                Assert.IsTrue(next.IsEnabled);
                next.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Flush(window);
                Assert.IsGreaterThan(0d, scroll.HorizontalOffset);
                var tiles = ((StackPanel)scroll.Content).Children.OfType<ToggleButton>().ToArray();
                Assert.HasCount(8, tiles);
                Assert.IsTrue(tiles[0].IsChecked == true);
                Assert.IsTrue(tiles.All(tile => tile.ActualWidth == 72d));
                galleryHandler.IsEnabled = false;
                tiles[1].IsChecked = true;
                tiles[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Flush(window);
                var rejectedChoice = Descendants<ToggleButton>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-choice-1");
                Assert.IsFalse(rejectedChoice.IsChecked == true, "A rejected gallery choice must return to the runtime selection.");
                runtime.Refresh();
                Flush(window);
                Assert.IsFalse(Descendants<WpfButton>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-more").IsEnabled);
                galleryHandler.IsEnabled = true;
                runtime.Refresh();
                Flush(window);
                Descendants<WpfButton>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-more")
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Flush(window);
                var popupTile = PresentationSource.CurrentSources.Cast<PresentationSource>()
                    .Where(source => source.Dispatcher == window.Dispatcher)
                    .Select(source => source.RootVisual).OfType<FrameworkElement>()
                    .SelectMany(Descendants<ToggleButton>)
                    .Single(button => AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-popup-choice-7");
                popupTile.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Flush(window);
                Assert.IsTrue(Descendants<ToggleButton>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-table.styles-choice-7").IsChecked == true);
                Assert.IsTrue(worksheetEditor.Focus());
                ribbon.Width = 820d;
                Flush(window);
                Assert.IsTrue(worksheetEditor.IsKeyboardFocused);
                Assert.AreEqual("home", ribbon.LayoutSnapshot.SelectedTabId);
            }
            finally { window.Close(); }
        });
    }

    private static RibbonRuntimeController CreateRuntime() => CreateRuntime(out _);

    private static RibbonRuntimeController CreateRuntime(out GalleryHandler galleryHandler)
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("edit.paste", "Dán nội dung", iconKey: "edit.paste"), new EnabledHandler());
        registry.Register(new CommandDescriptor("edit.copy", "Sao chép", iconKey: "edit.copy"), new EnabledHandler());
        registry.Register(new CommandDescriptor("edit.cut", "Cắt", iconKey: "edit.cut"), new EnabledHandler());
        registry.Register(new CommandDescriptor("edit.format", "Sao chép định dạng", iconKey: "format.painter"), new EnabledHandler());
        galleryHandler = new GalleryHandler();
        registry.Register(new CommandDescriptor("table.styles", "Kiểu bảng", iconKey: "table.styles"), galleryHandler);
        return new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("clipboard", "Bảng tạm", [
                    new RibbonItemDefinition("edit.paste", IsLarge: true),
                    new RibbonItemDefinition("edit.copy", Order: 1),
                    new RibbonItemDefinition("edit.cut", Order: 2),
                    new RibbonItemDefinition("edit.format", Order: 3),
                ]),
                new RibbonGroupDefinition("styles", "Kiểu bảng", [
                    new RibbonItemDefinition("table.styles", RibbonItemKind.Gallery)
                    {
                        GalleryPreview = static _ => new RibbonGalleryPreview(4, 3,
                            Enumerable.Range(0, 12).Select(index => new RibbonGalleryPreviewCell(
                                index < 3 ? 0xFF18734A : index < 6 ? 0xFFE0EEE6 : 0xFFFFFFFF,
                                index < 3 ? 0xFFFFFFFF : 0xFF405449))),
                    },
                ]),
            ]),
        ], contextualTabs: [], quickAccessToolbar: [new RibbonCommandSurfaceItem("edit.copy", "1")], backstage: []), registry);
    }

    private static Window CreateWindow(object content) => new()
    {
        Content = content,
        Width = 1568d,
        Height = 420d,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -32000d,
        Top = -32000d,
    };

    private static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });
        window.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void RunInSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90d)));
        failure?.Throw();
    }

    private sealed class EnabledHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class GalleryHandler : IStatefulCommandHandler
    {
        private string _selectedValue = "0";
        public bool IsEnabled { get; set; } = true;
        public bool CanExecute(CommandContext context) => IsEnabled;
        public ValueTask ExecuteAsync(CommandContext context)
        {
            _selectedValue = ((RibbonItemActivation)context.Parameter!).SelectedValue ?? throw new InvalidOperationException("A gallery choice is required.");
            return ValueTask.CompletedTask;
        }
        public CommandState GetState(CommandContext context) => new(IsEnabled, null, null, _selectedValue,
            Enumerable.Range(0, 8).Select(index => new CommandItem(index.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Kiểu {index + 1}")));
    }
}
