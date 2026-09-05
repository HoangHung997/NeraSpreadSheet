using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopRibbonItemModelSmokeTests
{
    [TestMethod]
    [Timeout(120_000)]
    public void LoadedDesktopRibbonsShouldRenderAndActivateEveryItemKind()
    {
        Exception? failure = null;
        using var finished = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                VerifyWpfRibbon();
                VerifyWinFormsRibbon();
                VerifyWpfOverflowSemantics();
                VerifyWinFormsOverflowSemantics();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                finished.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(finished.Wait(TimeSpan.FromSeconds(90d)));
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void VerifyWpfRibbon()
    {
        var (runtime, handlers) = CreateRuntime();
        using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime)
        {
            Width = 1_600d,
        };
        var window = new System.Windows.Window
        {
            Content = ribbon,
            Width = 1_620d,
            Height = 220d,
            ShowInTaskbar = false,
            Left = -32_000d,
            Top = -32_000d,
        };
        try
        {
            window.Show();
            FlushWpf(window);
            ribbon.Rebuild();
            FlushWpf(window);

            Assert.AreEqual(2, FindWpfDescendants<System.Windows.Controls.ComboBox>(ribbon).Count());
            var separator = FindWpfDescendants<System.Windows.Controls.Separator>(ribbon)
                .Single();
            Assert.AreEqual(8d, separator.Width);
            Assert.AreEqual(0d, separator.Margin.Left + separator.Margin.Right);
            var gallery = FindWpfDescendants<System.Windows.Controls.ScrollViewer>(ribbon)
                .Single(control =>
                    System.Windows.Automation.AutomationProperties.GetName(control) ==
                    "Automation Gallery");
            Assert.AreEqual(
                System.Windows.Controls.ScrollBarVisibility.Hidden,
                gallery.HorizontalScrollBarVisibility);
            Assert.IsNotNull(FindWpfDescendants<System.Windows.Controls.Button>(ribbon).Single(button =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "ribbon-command-item.Gallery-more"));
            Assert.IsInstanceOfType<System.Windows.Controls.StackPanel>(gallery.Content);
            Assert.AreEqual(
                12,
                ((System.Windows.Controls.StackPanel)gallery.Content).Children.Count);
            Assert.IsTrue(FindWpfDescendants<System.Windows.Controls.DockPanel>(ribbon).Any());
            var splitPrimary = FindWpfDescendants<System.Windows.FrameworkElement>(ribbon)
                .Single(element =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(element) ==
                    "ribbon-command-item.SplitButton-primary");
            var splitMenu = FindWpfDescendants<System.Windows.FrameworkElement>(ribbon)
                .Single(element =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(element) ==
                    "ribbon-command-item.SplitButton-menu");
            Assert.AreNotSame(splitPrimary, splitMenu);
            Assert.IsTrue(splitMenu.Focus());
            FlushWpf(window);
            ribbon.Rebuild();
            FlushWpf(window);
            Assert.IsTrue(FindWpfDescendants<System.Windows.FrameworkElement>(ribbon)
                .Single(element =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(element) ==
                    "ribbon-command-item.SplitButton-menu")
                .IsKeyboardFocused);
            Assert.IsInstanceOfType<System.Windows.Controls.Button>(
                FindWpfDescendants<System.Windows.FrameworkElement>(ribbon).Single(element =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(element) ==
                    "ribbon-command-item.Button"));
            Assert.IsInstanceOfType<System.Windows.Controls.Primitives.ToggleButton>(
                FindWpfDescendants<System.Windows.FrameworkElement>(ribbon).Single(element =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(element) ==
                    "ribbon-command-item.Toggle"));
            FindWpfDescendants<System.Windows.Controls.Button>(ribbon)
                .Single(button =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(button) ==
                    "ribbon-command-item.Button")
                .RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            FlushWpf(window);
            FindWpfDescendants<System.Windows.Controls.Primitives.ToggleButton>(ribbon)
                .Single(button =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(button) ==
                    "ribbon-command-item.Toggle")
                .RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            FlushWpf(window);
            Assert.AreEqual(1, handlers[RibbonItemKind.Button].ExecutionCount);
            Assert.AreEqual(1, handlers[RibbonItemKind.Toggle].ExecutionCount);
            var combo = FindWpfDescendants<System.Windows.Controls.ComboBox>(ribbon)
                .Single(control =>
                    System.Windows.Automation.AutomationProperties.GetName(control) ==
                    "Automation ComboBox");
            combo.IsDropDownOpen = true;
            FlushWpf(window);
            Assert.IsFalse(((System.Windows.Controls.ComboBoxItem)combo
                .ItemContainerGenerator.ContainerFromIndex(2)).IsEnabled);
            combo.IsDropDownOpen = false;
            combo.SelectedValue = "disabled";
            FlushWpf(window);
            Assert.AreEqual("one", combo.SelectedValue);
            Assert.AreEqual(0, handlers[RibbonItemKind.ComboBox].ExecutionCount);
            combo.SelectedValue = "two";
            FlushWpf(window);

            Assert.AreEqual("two", handlers[RibbonItemKind.ComboBox].SelectedValue);
            Assert.AreEqual(1, handlers[RibbonItemKind.ComboBox].ExecutionCount);
        }
        finally
        {
            window.Close();
            FlushWpf(window);
        }
    }

    private static void VerifyWinFormsRibbon()
    {
        var (runtime, handlers) = CreateRuntime();
        using var form = new System.Windows.Forms.Form
        {
            ClientSize = new System.Drawing.Size(1_620, 220),
            ShowInTaskbar = false,
            StartPosition = System.Windows.Forms.FormStartPosition.Manual,
            Location = new System.Drawing.Point(-32_000, -32_000),
        };
        using var ribbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(runtime)
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
        };
        form.Controls.Add(ribbon);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        ribbon.Rebuild();
        System.Windows.Forms.Application.DoEvents();

        Assert.AreEqual(2, FindWinFormsDescendants<System.Windows.Forms.ComboBox>(ribbon).Count());
        var separator = FindWinFormsDescendants<System.Windows.Forms.Panel>(ribbon)
            .Single(static panel => panel.AccessibleName == "Dấu phân cách");
        Assert.AreEqual((int)Math.Round(8d * ribbon.DeviceDpi / 96d), separator.Width);
        Assert.AreEqual(0, separator.Margin.Horizontal);
        var gallery = FindWinFormsDescendants<System.Windows.Forms.FlowLayoutPanel>(ribbon)
            .Single(static panel => panel.AccessibleName == "Automation Gallery");
        Assert.IsTrue(gallery.AutoScroll);
        Assert.IsFalse(gallery.WrapContents);
        Assert.AreEqual(12, gallery.Controls.Count);
        Assert.IsNotNull(((System.Windows.Forms.ButtonBase)gallery.Controls[0]).Image);
        var splitPrimary = FindWinFormsDescendants<System.Windows.Forms.Control>(ribbon)
            .Single(static control =>
                control.Name == "ribbon-command-item.SplitButton-primary");
        var splitMenu = FindWinFormsDescendants<System.Windows.Forms.Control>(ribbon)
            .Single(static control =>
                control.Name == "ribbon-command-item.SplitButton-menu");
        Assert.AreNotSame(splitPrimary, splitMenu);
        Assert.IsTrue(splitMenu.Focus());
        System.Windows.Forms.Application.DoEvents();
        ribbon.Rebuild();
        System.Windows.Forms.Application.DoEvents();
        Assert.IsTrue(FindWinFormsDescendants<System.Windows.Forms.Control>(ribbon)
            .Single(static control =>
                control.Name == "ribbon-command-item.SplitButton-menu")
            .Focused);
        Assert.IsInstanceOfType<System.Windows.Forms.Button>(
            FindWinFormsDescendants<System.Windows.Forms.Control>(ribbon)
                .Single(static control => control.Name == "ribbon-command-item.Button"));
        Assert.IsInstanceOfType<System.Windows.Forms.CheckBox>(
            FindWinFormsDescendants<System.Windows.Forms.Control>(ribbon)
                .Single(static control => control.Name == "ribbon-command-item.Toggle"));
        FindWinFormsDescendants<System.Windows.Forms.Button>(ribbon)
            .Single(static button => button.Name == "ribbon-command-item.Button")
            .PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var toggle = FindWinFormsDescendants<System.Windows.Forms.CheckBox>(ribbon)
            .Single(static button => button.Name == "ribbon-command-item.Toggle");
        typeof(System.Windows.Forms.CheckBox).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(toggle, [EventArgs.Empty]);
        System.Windows.Forms.Application.DoEvents();
        Assert.AreEqual(1, handlers[RibbonItemKind.Button].ExecutionCount);
        Assert.AreEqual(1, handlers[RibbonItemKind.Toggle].ExecutionCount);
        var combo = FindWinFormsDescendants<System.Windows.Forms.ComboBox>(ribbon)
            .Single(static control => control.AccessibleName == "Automation ComboBox");
        combo.SelectedValue = "disabled";
        System.Windows.Forms.Application.DoEvents();
        Assert.AreEqual("one", combo.SelectedValue);
        Assert.AreEqual(0, handlers[RibbonItemKind.ComboBox].ExecutionCount);
        combo.SelectedValue = "two";
        System.Windows.Forms.Application.DoEvents();

        Assert.AreEqual("two", handlers[RibbonItemKind.ComboBox].SelectedValue);
        Assert.AreEqual(1, handlers[RibbonItemKind.ComboBox].ExecutionCount);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void VerifyWpfOverflowSemantics()
    {
        var (runtime, button, toggle) = CreateOverflowRuntime();
        using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime)
        {
            Width = 90d,
        };
        var window = new System.Windows.Window
        {
            Content = ribbon,
            Width = 110d,
            Height = 180d,
            ShowInTaskbar = false,
            Left = -32_000d,
            Top = -32_000d,
        };
        try
        {
            window.Show();
            FlushWpf(window);
            ribbon.Rebuild();
            FlushWpf(window);
            Assert.IsTrue(ribbon.LayoutSnapshot.Tabs[0].HasOverflow);
            var menu = FindWpfDescendants<System.Windows.Controls.Menu>(ribbon).Single();
            var root = (System.Windows.Controls.MenuItem)menu.Items[0];
            root.IsSubmenuOpen = true;
            FlushWpf(window);
            Assert.IsTrue(root.IsSubmenuOpen);
            var group = (System.Windows.Controls.MenuItem)root.Items[0];
            var commands = group.Items.OfType<System.Windows.Controls.MenuItem>().ToArray();
            var buttonItem = commands.Single(item => Equals(item.Header, "Nút rõ ràng"));
            var toggleItem = commands.Single(item => Equals(item.Header, "Bật/tắt rõ ràng"));
            Assert.IsFalse(buttonItem.IsCheckable);
            Assert.IsTrue(toggleItem.IsCheckable);

            buttonItem.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent));
            FlushWpf(window);
            toggleItem = FindOverflowWpfCommand(ribbon, "Bật/tắt rõ ràng");
            toggleItem.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent));
            FlushWpf(window);
            Assert.AreEqual(1, button.ExecutionCount);
            Assert.AreEqual(1, toggle.ExecutionCount);
        }
        finally
        {
            window.Close();
            FlushWpf(window);
        }
    }

    private static void VerifyWinFormsOverflowSemantics()
    {
        var (runtime, button, toggle) = CreateOverflowRuntime();
        using var form = new System.Windows.Forms.Form
        {
            ClientSize = new System.Drawing.Size(80, 180),
            ShowInTaskbar = false,
            StartPosition = System.Windows.Forms.FormStartPosition.Manual,
            Location = new System.Drawing.Point(-32_000, -32_000),
        };
        using var ribbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(runtime)
        {
            Width = 60,
            Height = 150,
        };
        form.Controls.Add(ribbon);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        ribbon.Width = 60;
        ribbon.Rebuild();
        System.Windows.Forms.Application.DoEvents();
        Assert.IsTrue(ribbon.LayoutSnapshot.Tabs[0].HasOverflow);
        var overflowButton = FindWinFormsDescendants<System.Windows.Forms.Button>(ribbon)
            .Single(static item => item.Name == "ribbon-overflow");
        overflowButton.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var menu = GetOverflowMenus(ribbon).Single();
        Assert.IsTrue(menu.Visible);
        var group = (System.Windows.Forms.ToolStripMenuItem)menu.Items[0];
        var commands = group.DropDownItems.OfType<System.Windows.Forms.ToolStripMenuItem>()
            .ToArray();
        var buttonItem = commands.Single(item => item.Text == "Nút rõ ràng");
        var toggleItem = commands.Single(item => item.Text == "Bật/tắt rõ ràng");
        Assert.IsFalse(buttonItem.Checked);
        Assert.AreNotEqual(System.Windows.Forms.AccessibleRole.CheckButton, buttonItem.AccessibleRole);
        Assert.AreEqual(System.Windows.Forms.AccessibleRole.CheckButton, toggleItem.AccessibleRole);

        buttonItem.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var refreshedMenu = GetOverflowMenus(ribbon).Single();
        var refreshedGroup = (System.Windows.Forms.ToolStripMenuItem)refreshedMenu.Items[0];
        var refreshedToggle = refreshedGroup.DropDownItems
            .OfType<System.Windows.Forms.ToolStripMenuItem>()
            .Single(item => item.Text == "Bật/tắt rõ ràng");
        refreshedToggle.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        Assert.AreEqual(1, button.ExecutionCount);
        Assert.AreEqual(1, toggle.ExecutionCount);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static System.Windows.Controls.MenuItem FindOverflowWpfCommand(
        System.Windows.DependencyObject ribbon,
        string caption)
    {
        var menu = FindWpfDescendants<System.Windows.Controls.Menu>(ribbon).Single();
        var root = (System.Windows.Controls.MenuItem)menu.Items[0];
        var group = (System.Windows.Controls.MenuItem)root.Items[0];
        return group.Items.OfType<System.Windows.Controls.MenuItem>()
            .Single(item => Equals(item.Header, caption));
    }

    private static IReadOnlyList<System.Windows.Forms.ContextMenuStrip> GetOverflowMenus(
        NeraSpreadSheet.WinForms.NeraRibbonControl ribbon)
    {
        var field = typeof(NeraSpreadSheet.WinForms.NeraRibbonControl).GetField(
            "_overflowMenus",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        return (IReadOnlyList<System.Windows.Forms.ContextMenuStrip>)field.GetValue(ribbon)!;
    }

    private static (RibbonRuntimeController Runtime,
        StaticStateHandler Button,
        StaticStateHandler Toggle) CreateOverflowRuntime()
    {
        var registry = new CommandRegistry();
        var button = new StaticStateHandler(true);
        var toggle = new StaticStateHandler(null);
        registry.Register(new CommandDescriptor("explicit.button", "Nút rõ ràng"), button);
        registry.Register(
            new CommandDescriptor("explicit.toggle", "Bật/tắt rõ ràng"),
            toggle);
        var definition = new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("items", "Mục", [
                    new RibbonItemDefinition("explicit.button", RibbonItemKind.Button),
                    new RibbonItemDefinition("explicit.toggle", RibbonItemKind.Toggle),
                ]),
            ]),
        ]);
        return (new RibbonRuntimeController(definition, registry), button, toggle);
    }

    private static (RibbonRuntimeController Runtime,
        Dictionary<RibbonItemKind, SelectionHandler> Handlers) CreateRuntime()
    {
        var registry = new CommandRegistry();
        var handlers = new Dictionary<RibbonItemKind, SelectionHandler>();
        var items = new List<RibbonItemDefinition>();
        var order = 0;
        foreach (var kind in Enum.GetValues<RibbonItemKind>()
                     .Where(static kind => kind != RibbonItemKind.Separator))
        {
            var handler = new SelectionHandler(
                kind == RibbonItemKind.Button
                    ? true
                    : kind == RibbonItemKind.Toggle ? false : null,
                kind == RibbonItemKind.Gallery
                    ? 12
                    : kind == RibbonItemKind.ComboBox ? 3 : 2,
                kind == RibbonItemKind.ComboBox ? 3 : null);
            handlers.Add(kind, handler);
            registry.Register(
                new CommandDescriptor(
                    $"item.{kind}",
                    kind.ToString(),
                    tooltip: $"Tooltip {kind}",
                    iconKey: "missing.icon.key",
                    shortcut: kind == RibbonItemKind.ComboBox ? "Ctrl+M" : null),
                handler);
            items.Add(new RibbonItemDefinition(
                $"item.{kind}",
                kind,
                order: order++,
                automationName: $"Automation {kind}",
                measurement: static context => context.Kind switch
                {
                    RibbonItemKind.Gallery => 160d,
                    RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker => 120d,
                    _ => context.DefaultWidth,
                }));
        }
        items.Add(RibbonItemDefinition.Separator("primary", order));
        var definition = new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("items", "Mục", items),
            ]),
        ]);
        return (new RibbonRuntimeController(definition, registry), handlers);
    }

    private static IEnumerable<T> FindWpfDescendants<T>(
        System.Windows.DependencyObject root)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0;
             index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
             index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindWpfDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<T> FindWinFormsDescendants<T>(
        System.Windows.Forms.Control root)
        where T : System.Windows.Forms.Control
    {
        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindWinFormsDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void FlushWpf(System.Windows.Window window)
    {
        window.Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            static () => { });
    }

    private sealed class SelectionHandler : IStatefulCommandHandler
    {
        private readonly bool? _isChecked;
        private readonly IReadOnlyList<CommandItem> _items;

        public SelectionHandler(
            bool? isChecked,
            int itemCount,
            int? disabledIndex)
        {
            _isChecked = isChecked;
            _items = Enumerable.Range(1, itemCount)
                .Select(index => new CommandItem(
                    index == disabledIndex
                        ? "disabled"
                        : index == 1 ? "one" : index == 2 ? "two" : $"choice-{index}",
                    $"Mục {index}",
                    isEnabled: index != disabledIndex,
                    iconKey: "file.new"))
                .ToArray();
        }

        public string? SelectedValue { get; private set; } = "one";

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(
            true,
            _isChecked,
            null,
            SelectedValue,
            _items);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            if (context.Parameter is RibbonItemActivation activation)
            {
                SelectedValue = activation.SelectedValue;
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticStateHandler(bool? isChecked) : IStatefulCommandHandler
    {
        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(true, isChecked);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            return ValueTask.CompletedTask;
        }
    }
}
