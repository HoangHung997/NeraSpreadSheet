using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;
using WpfAutomationProperties = System.Windows.Automation.AutomationProperties;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfPanel = System.Windows.Controls.StackPanel;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfPresentationSource = System.Windows.PresentationSource;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsButtonBase = System.Windows.Forms.ButtonBase;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsToolStripItem = System.Windows.Forms.ToolStripItem;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopRibbonPresenterSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WpfRibbonAndMenuShouldLoadActivateAndRefreshNativeState()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            var ribbonHandler = new OneShotHandler(isChecked: false);
            var menuHandler = new OneShotHandler(isChecked: null);
            registry.Register(
                new CommandDescriptor(
                    "view.gridlines",
                    "Đường lưới",
                    tooltip: "Hiện hoặc ẩn đường lưới",
                    shortcut: "Ctrl+G"),
                ribbonHandler);
            registry.Register(
                new CommandDescriptor("file.save", "Lưu", shortcut: "Ctrl+S"),
                menuHandler);
            var ribbonRuntime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                registry);
            var barRuntime = new BarRuntimeController(
                CreateMenuDefinition(),
                registry);
            using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(ribbonRuntime);
            using var menu = new NeraSpreadSheet.Wpf.NeraBarPresenter(barRuntime);
            var root = new WpfPanel();
            root.Children.Add(ribbon);
            root.Children.Add(menu.NativeControl);
            var window = new WpfWindow
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -32_000d,
                Top = -32_000d,
                Width = 640d,
                Height = 240d,
                Content = root,
            };

            try
            {
                window.Show();
                FlushWpf(window);
                var toggle = FindWpfDescendants<WpfToggleButton>(ribbon)
                    .Single();
                Assert.AreEqual(
                    "ribbon-command-view.gridlines",
                    WpfAutomationProperties.GetAutomationId(toggle));
                Assert.AreEqual(
                    "Đường lưới",
                    WpfAutomationProperties.GetName(toggle));
                Assert.IsTrue(toggle.IsEnabled);
                Assert.IsFalse(toggle.IsChecked);

                toggle.RaiseEvent(new System.Windows.RoutedEventArgs(
                    WpfButtonBase.ClickEvent));
                FlushWpf(window);

                var refreshed = FindWpfDescendants<WpfToggleButton>(ribbon)
                    .Single();
                Assert.AreEqual(1, ribbonHandler.ExecutionCount);
                Assert.IsFalse(refreshed.IsEnabled);
                Assert.IsTrue(refreshed.IsChecked);

                var save = FindWpfMenuItems(menu.NativeControl)
                    .Single(item => item.CommandParameter is CommandId);
                Assert.AreEqual("Ctrl+S", save.InputGestureText);
                save.RaiseEvent(new System.Windows.RoutedEventArgs(
                    WpfMenuItem.ClickEvent));
                FlushWpf(window);
                Assert.AreEqual(1, menuHandler.ExecutionCount);
                Assert.IsFalse(FindWpfMenuItems(menu.NativeControl)
                    .Single(item => item.CommandParameter is CommandId)
                    .IsEnabled);
            }
            finally
            {
                window.Close();
                FlushWpf(window);
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsRibbonAndMenuShouldLoadActivateAndRefreshNativeState()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            var ribbonHandler = new OneShotHandler(isChecked: false);
            var menuHandler = new OneShotHandler(isChecked: null);
            registry.Register(
                new CommandDescriptor("view.gridlines", "Đường lưới"),
                ribbonHandler);
            registry.Register(
                new CommandDescriptor("file.save", "Lưu", shortcut: "Ctrl+S"),
                menuHandler);
            var ribbonRuntime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                registry);
            var barRuntime = new BarRuntimeController(
                CreateMenuDefinition(),
                registry);
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(640, 240),
            };
            using var ribbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(ribbonRuntime)
            {
                Dock = WinFormsDockStyle.Fill,
            };
            using var menu = new NeraSpreadSheet.WinForms.NeraBarPresenter(barRuntime);
            form.MainMenuStrip = (System.Windows.Forms.MenuStrip)menu.NativeControl;
            form.Controls.Add(ribbon);
            form.Controls.Add(menu.NativeControl);
            form.Show();
            WinFormsApplication.DoEvents();

            var toggle = FindWinFormsDescendants<WinFormsButtonBase>(ribbon)
                .Single(control => control.Tag is CommandId);
            Assert.AreEqual("ribbon-command-view.gridlines", toggle.Name);
            Assert.AreEqual("Đường lưới", toggle.AccessibleName);
            Assert.IsTrue(toggle.Enabled);
            PerformWinFormsClick(toggle);
            WinFormsApplication.DoEvents();

            var refreshed = FindWinFormsDescendants<WinFormsButtonBase>(ribbon)
                .Single(control => control.Tag is CommandId);
            Assert.AreEqual(1, ribbonHandler.ExecutionCount);
            Assert.IsFalse(refreshed.Enabled);
            Assert.IsTrue(((System.Windows.Forms.CheckBox)refreshed).Checked);

            var save = FindWinFormsItems(menu.NativeControl.Items)
                .Single(item => item.Tag is CommandId);
            save.PerformClick();
            WinFormsApplication.DoEvents();
            Assert.AreEqual(1, menuHandler.ExecutionCount);
            Assert.IsFalse(FindWinFormsItems(menu.NativeControl.Items)
                .Single(item => item.Tag is CommandId)
                .Enabled);

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopShortcutBindingsShouldAttachAndActivateThroughRuntime()
    {
        RunInSta(() =>
        {
            var wpfRegistry = new CommandRegistry();
            var wpfHandler = new OneShotHandler(isChecked: null);
            wpfRegistry.Register(
                new CommandDescriptor(
                    "view.gridlines",
                    "Đường lưới",
                    shortcut: "Ctrl+G"),
                wpfHandler);
            var wpfRuntime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                wpfRegistry);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(wpfRuntime);
            var wpfWindow = new WpfWindow { Content = wpfRibbon, ShowInTaskbar = false };
            using var wpfBinding = wpfRibbon.BindShortcuts(wpfWindow);

            Assert.IsTrue(wpfRibbon.TryActivateShortcutAsync("control+g")
                .AsTask().GetAwaiter().GetResult());
            Assert.AreEqual(1, wpfHandler.ExecutionCount);

            var winFormsRegistry = new CommandRegistry();
            var winFormsHandler = new OneShotHandler(isChecked: null);
            winFormsRegistry.Register(
                new CommandDescriptor("file.save", "Lưu", shortcut: "Ctrl+S"),
                winFormsHandler);
            var winFormsRuntime = new BarRuntimeController(
                CreateMenuDefinition(),
                winFormsRegistry);
            using var form = new WinFormsForm();
            using var menu = new NeraSpreadSheet.WinForms.NeraBarPresenter(winFormsRuntime);
            var save = FindWinFormsItems(menu.NativeControl.Items)
                .OfType<System.Windows.Forms.ToolStripMenuItem>()
                .Single(item => item.Tag is CommandId);
            Assert.AreEqual(
                System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S,
                save.ShortcutKeys);
            using (menu.BindShortcuts(form))
            {
                Assert.IsTrue(form.KeyPreview);
                Assert.IsTrue(menu.TryActivateShortcutAsync("CONTROL+s")
                    .AsTask().GetAwaiter().GetResult());
            }
            Assert.IsFalse(form.KeyPreview);
            Assert.AreEqual(1, winFormsHandler.ExecutionCount);
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfShortcutBindingShouldIgnorePlainTextKeysWithoutThrowing()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            registry.Register(
                new CommandDescriptor("file.save", "Lưu", shortcut: "Ctrl+S"),
                new OneShotHandler(isChecked: null));
            var runtime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                registry);
            using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime);
            var window = new WpfWindow
            {
                Content = ribbon,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -32_000d,
                Top = -32_000d,
            };
            using var binding = ribbon.BindShortcuts(window);
            try
            {
                window.Show();
                FlushWpf(window);
                var source = WpfPresentationSource.FromVisual(window) ??
                    throw new AssertFailedException(
                        "The WPF window did not have a presentation source.");
                var args = new WpfKeyEventArgs(
                    WpfKeyboard.PrimaryDevice,
                    source,
                    Environment.TickCount,
                    WpfKey.S)
                {
                    RoutedEvent = WpfKeyboard.PreviewKeyDownEvent,
                };

                window.RaiseEvent(args);

                Assert.IsFalse(args.Handled);
            }
            finally
            {
                window.Close();
                FlushWpf(window);
            }
        });
    }

    private static RibbonDefinition CreateRibbonDefinition() =>
        new(
        [
            new RibbonTabDefinition(
                "view",
                "Xem",
                [
                    new RibbonGroupDefinition(
                        "display",
                        "Hiển thị",
                        [new RibbonItemDefinition("view.gridlines", IsLarge: true)]),
                ]),
        ]);

    private static BarDefinition CreateMenuDefinition() =>
        new(
            "main",
            BarKind.MainMenu,
            [
                BarItemDefinition.Submenu(
                    "Tệp",
                    [BarItemDefinition.Command("file.save")],
                    id: "file"),
            ]);

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

    private static IEnumerable<WpfMenuItem> FindWpfMenuItems(
        System.Windows.Controls.ItemsControl root)
    {
        foreach (var item in root.Items.OfType<WpfMenuItem>())
        {
            yield return item;
            foreach (var child in FindWpfMenuItems(item))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<T> FindWinFormsDescendants<T>(WinFormsControl root)
        where T : WinFormsControl
    {
        foreach (WinFormsControl child in root.Controls)
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

    private static void PerformWinFormsClick(WinFormsButtonBase button)
    {
        var onClick = button.GetType().GetMethod(
            "OnClick",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"{button.GetType().FullName}.OnClick was not found.");
        onClick.Invoke(button, [EventArgs.Empty]);
    }

    private static IEnumerable<WinFormsToolStripItem> FindWinFormsItems(
        System.Windows.Forms.ToolStripItemCollection items)
    {
        foreach (WinFormsToolStripItem item in items)
        {
            yield return item;
            if (item is System.Windows.Forms.ToolStripMenuItem menuItem)
            {
                foreach (var child in FindWinFormsItems(menuItem.DropDownItems))
                {
                    yield return child;
                }
            }
        }
    }

    private static void FlushWpf(WpfWindow window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(static () => { }));
        window.UpdateLayout();
    }

    private static void RunInSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Nera desktop Ribbon presenter smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop Ribbon presenter smoke timed out.");
        }
        failure?.Throw();
    }

    private sealed class OneShotHandler : IStatefulCommandHandler
    {
        private readonly bool? _initialChecked;

        public OneShotHandler(bool? isChecked)
        {
            _initialChecked = isChecked;
        }

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => ExecutionCount == 0;

        public CommandState GetState(CommandContext context) =>
            new(
                CanExecute(context),
                _initialChecked.HasValue
                    ? ExecutionCount > 0
                    : null);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            return ValueTask.CompletedTask;
        }
    }
}
