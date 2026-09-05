using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;
using WpfAutomationProperties = System.Windows.Automation.AutomationProperties;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfImage = System.Windows.Controls.Image;
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
    private static readonly string[] ResponsiveCommandIds =
        ["home.high", "home.left", "home.right"];

    [TestMethod]
    public void WpfTableBindingShouldDiscardQueuedRefreshAfterDisposal()
    {
        RunInSta(() =>
        {
            var session = new NeraSpreadSheet.Editing.SpreadsheetSession(new NeraSpreadSheet.Core.Workbook());
            session.Tables.Create(new NeraSpreadSheet.Core.CellRange(default, new NeraSpreadSheet.Core.CellAddress(2, 0)), "Sales");
            var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
            using var binding = new NeraSpreadSheet.Wpf.NeraWpfTableDesignRibbonBinding(session, runtime,
                System.Windows.Threading.Dispatcher.CurrentDispatcher);
            Assert.IsTrue(runtime.SelectionContext.IsInTable);
            Task.Run(() => session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(5, 5))).GetAwaiter().GetResult();
            binding.Dispose();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });
            Assert.IsTrue(runtime.SelectionContext.IsInTable, "A disposed binding must not publish queued work.");
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopTableDesignBindingShouldFollowSelectionAndExecuteOneTransaction()
    {
        RunInSta(() =>
        {
            var workbook = new NeraSpreadSheet.Core.Workbook();
            var worksheet = workbook.Worksheets[0];
            var table = new NeraSpreadSheet.Core.SpreadsheetTable(
                Guid.NewGuid(),
                "Sales",
                new NeraSpreadSheet.Core.CellRange(
                    default,
                    new NeraSpreadSheet.Core.CellAddress(2, 1)),
                [
                    new NeraSpreadSheet.Core.SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                    new NeraSpreadSheet.Core.SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
                ]);
            worksheet.AddTable(table);
            var session = new NeraSpreadSheet.Editing.SpreadsheetSession(workbook);
            session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(4, 4));
            var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
            var wpfRuntime = new RibbonRuntimeController(definition, session.Commands);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(wpfRuntime);
            var window = new WpfWindow
            {
                Content = wpfRibbon,
                ShowInTaskbar = false,
                Width = 1_400d,
                Height = 300d,
            };
            using var wpfBinding = wpfRibbon.BindTableDesign(session);
            try
            {
                window.Show();
                FlushWpf(window);
                Assert.AreEqual(6, wpfRibbon.LayoutSnapshot.Tabs.Count);
                session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(1, 0));
                FlushWpf(window);
                Assert.AreEqual(7, wpfRibbon.LayoutSnapshot.Tabs.Count);
                var before = session.History.UndoCount;
                Assert.IsTrue(wpfRuntime.TryActivateAsync(
                    NeraSpreadSheet.Editing.SpreadsheetTableCommandIds.FirstColumn)
                    .AsTask().GetAwaiter().GetResult());
                FlushWpf(window);
                Assert.AreEqual(before + 1, session.History.UndoCount);
                Assert.IsTrue(worksheet.Tables.Single().ShowFirstColumn);
                session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(4, 4));
                FlushWpf(window);
                Assert.AreEqual(6, wpfRibbon.LayoutSnapshot.Tabs.Count);
            }
            finally
            {
                window.Close();
                FlushWpf(window);
            }

            var winRuntime = new RibbonRuntimeController(definition, session.Commands);
            using var form = new WinFormsForm
            {
                ClientSize = new System.Drawing.Size(1_400, 300),
            };
            using var winRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(winRuntime)
            {
                Dock = WinFormsDockStyle.Fill,
            };
            form.Controls.Add(winRibbon);
            using var winBinding = winRibbon.BindTableDesign(session);
            form.Show();
            WinFormsApplication.DoEvents();
            Assert.AreEqual(6, winRibbon.LayoutSnapshot.Tabs.Count);
            session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(1, 1));
            WinFormsApplication.DoEvents();
            Assert.AreEqual(7, winRibbon.LayoutSnapshot.Tabs.Count);
            session.Selection.SetActiveCell(new NeraSpreadSheet.Core.CellAddress(4, 4));
            WinFormsApplication.DoEvents();
            Assert.AreEqual(6, winRibbon.LayoutSnapshot.Tabs.Count);
            form.Close();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopContextualQatBackstageAndKeyTipsShouldLoadAndRestoreState()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            var home = new OneShotHandler(null);
            var table = new OneShotHandler(null);
            var file = new OneShotHandler(null);
            registry.Register(new CommandDescriptor("home.copy", "Sao chép"), home);
            registry.Register(new CommandDescriptor("table.rename", "Đổi tên bảng"), table);
            registry.Register(new CommandDescriptor("file.save", "Lưu"), file);
            var definition = CreateContextualRibbonDefinition();
            var runtime = new RibbonRuntimeController(definition, registry);

            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime);
            var wpfEditor = new System.Windows.Controls.TextBox { Text = "worksheet" };
            var wpfRoot = new System.Windows.Controls.DockPanel();
            System.Windows.Controls.DockPanel.SetDock(
                wpfRibbon,
                System.Windows.Controls.Dock.Top);
            wpfRoot.Children.Add(wpfRibbon);
            wpfRoot.Children.Add(wpfEditor);
            var window = new WpfWindow
            {
                Content = wpfRoot,
                ShowInTaskbar = false,
                Width = 700d,
                Height = 260d,
            };
            using var wpfBinding = wpfRibbon.BindShortcuts(window);
            try
            {
                window.Show();
                FlushWpf(window);
                Assert.AreEqual(1, wpfRibbon.LayoutSnapshot.Tabs.Count);
                runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
                FlushWpf(window);
                Assert.AreEqual(2, wpfRibbon.LayoutSnapshot.Tabs.Count);
                Assert.IsNotNull(FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "ribbon-qat-home.copy"));
                var wpfFile = FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "ribbon-file");
                wpfFile.RaiseEvent(new System.Windows.RoutedEventArgs(WpfButtonBase.ClickEvent));
                FlushWpf(window);
                Assert.IsTrue(wpfRibbon.IsBackstageOpen);
                Assert.AreEqual(
                    System.Windows.Visibility.Visible,
                    FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                        .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                            "ribbon-backstage-file.save").Visibility);
                wpfFile = FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "ribbon-file");
                wpfFile.RaiseEvent(new System.Windows.RoutedEventArgs(WpfButtonBase.ClickEvent));
                FlushWpf(window);
                Assert.IsFalse(wpfRibbon.IsBackstageOpen);
                var wpfQat = FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "ribbon-qat-home.copy");
                wpfQat.Focus();
                wpfRibbon.EnterKeyTipMode();
                Assert.IsTrue(wpfRibbon.ProcessKeyTipAsync(runtime.KeyTips.TabTips["home"])
                    .AsTask().GetAwaiter().GetResult());
                wpfRibbon.EscapeKeyTipMode();
                wpfRibbon.EscapeKeyTipMode();
                FlushWpf(window);
                Assert.IsTrue(FindWpfDescendants<System.Windows.Controls.Button>(wpfRibbon)
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "ribbon-qat-home.copy").IsKeyboardFocused);
                wpfRibbon.IsMinimized = true;
                FlushWpf(window);
                Assert.IsTrue(wpfRibbon.IsMinimized);
                Assert.IsTrue(FindWpfDescendants<System.Windows.Controls.GroupBox>(wpfRibbon)
                    .All(static group => !group.IsVisible));
                wpfEditor.Focus();
                FlushWpf(window);
                Assert.IsTrue(wpfEditor.IsKeyboardFocused);
                Assert.IsTrue(RaiseWpfKey(window, WpfKey.LeftAlt).Handled);
                Assert.AreEqual(RibbonKeyTipScope.Tabs, wpfRibbon.KeyTipScope);
                Assert.IsTrue(RaiseWpfKey(window, WpfKey.F).Handled);
                FlushWpf(window);
                Assert.IsTrue(wpfRibbon.IsBackstageOpen);
                Assert.IsTrue(RaiseWpfKey(window, WpfKey.Escape).Handled);
                Assert.IsFalse(wpfRibbon.IsBackstageOpen);
                Assert.IsTrue(RaiseWpfKey(window, WpfKey.Escape).Handled);
                FlushWpf(window);
                Assert.AreEqual(RibbonKeyTipScope.Inactive, wpfRibbon.KeyTipScope);
                Assert.IsTrue(wpfEditor.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
                FlushWpf(window);
            }

            var winRuntime = new RibbonRuntimeController(definition, registry);
            using var form = new WinFormsForm { ClientSize = new System.Drawing.Size(700, 260) };
            using var winRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(winRuntime)
            {
                Dock = WinFormsDockStyle.Top,
                Height = 220,
            };
            var winEditor = new System.Windows.Forms.TextBox { Dock = WinFormsDockStyle.Bottom };
            form.Controls.Add(winRibbon);
            form.Controls.Add(winEditor);
            using var winBinding = winRibbon.BindShortcuts(form);
            form.Show();
            WinFormsApplication.DoEvents();
            winRuntime.SetSelectionContext(new RibbonSelectionContext(true, true));
            WinFormsApplication.DoEvents();
            Assert.AreEqual(2, winRibbon.LayoutSnapshot.Tabs.Count);
            Assert.IsNotNull(FindWinFormsDescendants<System.Windows.Forms.Button>(winRibbon)
                .Single(button => button.Name == "ribbon-backstage-file.save"));
            PerformWinFormsClick(FindWinFormsDescendants<System.Windows.Forms.Button>(winRibbon)
                .Single(button => button.Name == "ribbon-file"));
            WinFormsApplication.DoEvents();
            Assert.IsTrue(winRibbon.IsBackstageOpen);
            PerformWinFormsClick(FindWinFormsDescendants<System.Windows.Forms.Button>(winRibbon)
                .Single(button => button.Name == "ribbon-file"));
            WinFormsApplication.DoEvents();
            Assert.IsFalse(winRibbon.IsBackstageOpen);
            var winQat = FindWinFormsDescendants<System.Windows.Forms.Button>(winRibbon)
                .Single(button => button.Name == "ribbon-qat-home.copy");
            winQat.Focus();
            winRibbon.EnterKeyTipMode();
            Assert.IsTrue(winRibbon.ProcessKeyTipAsync(winRuntime.KeyTips.TabTips["home"])
                .AsTask().GetAwaiter().GetResult());
            winRibbon.EscapeKeyTipMode();
            winRibbon.EscapeKeyTipMode();
            WinFormsApplication.DoEvents();
            Assert.IsTrue(FindWinFormsDescendants<System.Windows.Forms.Button>(winRibbon)
                .Single(button => button.Name == "ribbon-qat-home.copy").Focused);
            winRibbon.IsMinimized = true;
            WinFormsApplication.DoEvents();
            Assert.IsTrue(winRibbon.IsMinimized);
            Assert.IsTrue(FindWinFormsDescendants<System.Windows.Forms.FlowLayoutPanel>(winRibbon)
                .Where(static panel => panel.Parent is System.Windows.Forms.TabPage)
                .All(static panel => !panel.Visible));
            winEditor.Focus();
            WinFormsApplication.DoEvents();
            Assert.IsTrue(winEditor.Focused);
            RaiseWinFormsKey(form, System.Windows.Forms.Keys.Menu);
            Assert.AreEqual(RibbonKeyTipScope.Tabs, winRibbon.KeyTipScope);
            RaiseWinFormsKey(form, System.Windows.Forms.Keys.F);
            WinFormsApplication.DoEvents();
            Assert.IsTrue(winRibbon.IsBackstageOpen);
            RaiseWinFormsKey(form, System.Windows.Forms.Keys.Escape);
            Assert.IsFalse(winRibbon.IsBackstageOpen);
            RaiseWinFormsKey(form, System.Windows.Forms.Keys.Escape);
            WinFormsApplication.DoEvents();
            Assert.AreEqual(RibbonKeyTipScope.Inactive, winRibbon.KeyTipScope);
            Assert.IsTrue(winEditor.Focused);
            form.Close();
        });
    }

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
                    iconKey: "view.gridlines",
                    shortcut: "Ctrl+G"),
                ribbonHandler);
            registry.Register(
                new CommandDescriptor(
                    "file.save",
                    "Lưu",
                    iconKey: "file.save",
                    shortcut: "Ctrl+S"),
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
                var ribbonIcon = FindWpfDescendants<WpfImage>(toggle).Single();
                Assert.AreEqual(32d, ribbonIcon.Width);
                Assert.IsNotNull(ribbonIcon.Source);

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
                Assert.IsNotNull(save.Icon);
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
                new CommandDescriptor(
                    "view.gridlines",
                    "Đường lưới",
                    iconKey: "view.gridlines"),
                ribbonHandler);
            registry.Register(
                new CommandDescriptor(
                    "file.save",
                    "Lưu",
                    iconKey: "file.save",
                    shortcut: "Ctrl+S"),
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
            Assert.IsNotNull(toggle.Image);
            Assert.AreEqual(new System.Drawing.Size(32, 32), toggle.Image.Size);
            PerformWinFormsClick(toggle);
            WinFormsApplication.DoEvents();

            var refreshed = FindWinFormsDescendants<WinFormsButtonBase>(ribbon)
                .Single(control => control.Tag is CommandId);
            Assert.AreEqual(1, ribbonHandler.ExecutionCount);
            Assert.IsFalse(refreshed.Enabled);
            Assert.IsTrue(((System.Windows.Forms.CheckBox)refreshed).Checked);

            var save = FindWinFormsItems(menu.NativeControl.Items)
                .Single(item => item.Tag is CommandId);
            Assert.IsNotNull(save.Image);
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

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopRibbonsShouldConsumeTheSameResponsiveSnapshotWhenLoaded()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            foreach (var id in ResponsiveCommandIds)
            {
                registry.Register(
                    new CommandDescriptor(id, id, iconKey: "view.gridlines"),
                    new OneShotHandler(isChecked: null));
            }
            var definition = CreateResponsiveRibbonDefinition();
            var wpfRuntime = new RibbonRuntimeController(definition, registry);
            var winFormsRuntime = new RibbonRuntimeController(definition, registry);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(
                wpfRuntime)
            {
                Width = 140d,
            };
            var window = new WpfWindow
            {
                Content = wpfRibbon,
                Width = 160d,
                Height = 180d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -32_000d,
                Top = -32_000d,
            };
            using var form = new WinFormsForm
            {
                ClientSize = new System.Drawing.Size(160, 180),
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
            };
            using var winFormsRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(
                winFormsRuntime)
            {
                Dock = WinFormsDockStyle.None,
                ClientSize = new System.Drawing.Size(140, 120),
            };
            form.Controls.Add(winFormsRibbon);

            try
            {
                window.Show();
                form.Show();
                FlushWpf(window);
                WinFormsApplication.DoEvents();
                wpfRibbon.Rebuild();
                winFormsRibbon.Rebuild();

                var engine = new RibbonResponsiveLayoutEngine();
                var expectedWpf = engine.Layout(
                    wpfRuntime.Snapshot,
                    new RibbonLayoutRequest(
                        wpfRibbon.LayoutSnapshot.AvailableWidth,
                        wpfRibbon.LayoutSnapshot.Scale));
                var expectedWinForms = engine.Layout(
                    winFormsRuntime.Snapshot,
                    new RibbonLayoutRequest(
                        winFormsRibbon.LayoutSnapshot.AvailableWidth,
                        winFormsRibbon.LayoutSnapshot.Scale));
                CollectionAssert.AreEqual(
                    expectedWpf.Tabs[0].Groups.Select(static group => group.Mode).ToArray(),
                    wpfRibbon.LayoutSnapshot.Tabs[0].Groups
                        .Select(static group => group.Mode).ToArray());
                CollectionAssert.AreEqual(
                    expectedWinForms.Tabs[0].Groups.Select(static group => group.Mode).ToArray(),
                    winFormsRibbon.LayoutSnapshot.Tabs[0].Groups
                        .Select(static group => group.Mode).ToArray());
                Assert.IsTrue(wpfRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
                Assert.IsTrue(winFormsRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
                Assert.IsNotInstanceOfType<System.Windows.Controls.ScrollViewer>(
                    FindWpfDescendants<System.Windows.Controls.TabItem>(wpfRibbon)
                        .Single().Content);
            }
            finally
            {
                form.Close();
                window.Close();
                WinFormsApplication.DoEvents();
                FlushWpf(window);
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void NarrowDesktopCommandsShouldKeepUnavailableIconCaptionInOverflowAndInline()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            registry.Register(
                new CommandDescriptor(
                    "home.missing-icon",
                    "Không có biểu tượng",
                    iconKey: "missing.icon.key"),
                new OneShotHandler(isChecked: null));
            var definition = new RibbonDefinition(
            [
                new RibbonTabDefinition(
                    "home",
                    "Trang đầu",
                    [
                        new RibbonGroupDefinition(
                            "commands",
                            "Lệnh",
                            [new RibbonItemDefinition("home.missing-icon")]),
                    ]),
            ]);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(
                new RibbonRuntimeController(definition, registry))
            {
                Width = 70d,
            };
            using var winFormsRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(
                new RibbonRuntimeController(definition, registry));
            winFormsRibbon.ClientSize = new System.Drawing.Size(
                (int)Math.Ceiling(70d * winFormsRibbon.DeviceDpi / 96d),
                120);

            wpfRibbon.Measure(new System.Windows.Size(70d, 120d));
            wpfRibbon.Arrange(new System.Windows.Rect(0d, 0d, 70d, 120d));
            wpfRibbon.UpdateLayout();
            wpfRibbon.Rebuild();
            winFormsRibbon.Rebuild();

            Assert.AreEqual(
                RibbonItemSize.Compact,
                wpfRibbon.LayoutSnapshot.Tabs[0].Groups[0].Items[0].Size);
            Assert.IsTrue(wpfRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
            Assert.IsTrue(winFormsRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
            Assert.IsTrue(wpfRibbon.LayoutSnapshot.Tabs[0].Groups[0].Items[0].CaptionVisible);
            var overflowTab = (System.Windows.Controls.TabItem)wpfRibbon.NativeTabControl.Items[0];
            var overflowMenu = ((System.Windows.Controls.StackPanel)overflowTab.Content).Children.OfType<System.Windows.Controls.Menu>().Single();
            var overflowRoot = (WpfMenuItem)overflowMenu.Items[0];
            var overflowGroup = (WpfMenuItem)overflowRoot.Items[0];
            Assert.AreEqual("Không có biểu tượng", ((WpfMenuItem)overflowGroup.Items[0]).Header);
            wpfRibbon.Width = 240d;
            wpfRibbon.Measure(new System.Windows.Size(240d, 180d));
            wpfRibbon.Arrange(new System.Windows.Rect(0d, 0d, 240d, 180d));
            wpfRibbon.UpdateLayout();
            wpfRibbon.Rebuild();
            winFormsRibbon.ClientSize = new System.Drawing.Size((int)Math.Ceiling(240d * winFormsRibbon.DeviceDpi / 96d), 180);
            winFormsRibbon.Rebuild();
            Assert.IsFalse(wpfRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
            Assert.IsFalse(winFormsRibbon.LayoutSnapshot.Tabs[0].HasOverflow);
            var wpfTab = (System.Windows.Controls.TabItem)wpfRibbon.NativeTabControl.Items[0];
            var wpfGroups = (System.Windows.Controls.StackPanel)wpfTab.Content;
            var wpfGroup = (System.Windows.Controls.GroupBox)wpfGroups.Children[0];
            var wpfItems = (System.Windows.Controls.Canvas)wpfGroup.Content;
            var wpfButton = (WpfButtonBase)wpfItems.Children[0];
            var wpfContent = ((System.Windows.Controls.Grid)wpfButton.Content).Children.OfType<System.Windows.Controls.StackPanel>().Single();
            Assert.AreEqual(
                "Không có biểu tượng",
                wpfContent.Children.OfType<System.Windows.Controls.TextBlock>()
                    .Single().Text);
            var winFormsButton = FindWinFormsDescendants<WinFormsButtonBase>(
                    winFormsRibbon)
                .Single(control => control.Tag is CommandId);
            Assert.AreEqual("Không có biểu tượng", winFormsButton.Text);
            Assert.IsNull(winFormsButton.Image);
            Assert.IsGreaterThanOrEqualTo(wpfContent.Children.OfType<System.Windows.Controls.TextBlock>().Single().MaxWidth + 6d, wpfButton.Width);
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void CompactDesktopCommandsShouldKeepTextualBadgeWhenIconIsAvailable()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            registry.Register(
                new CommandDescriptor(
                    "view.gridlines",
                    "Đường lưới",
                    iconKey: "view.gridlines"),
                new OneShotHandler(isChecked: null));
            var definition = new RibbonDefinition([
                new RibbonTabDefinition("view", "Xem", [
                    new RibbonGroupDefinition("display", "Hiển thị", [
                        new RibbonItemDefinition("view.gridlines"),
                    ]),
                ]),
            ]);
            var wpfRuntime = new RibbonRuntimeController(definition, registry);
            var winRuntime = new RibbonRuntimeController(definition, registry);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(wpfRuntime)
            {
                Width = 90d,
            };
            using var winRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(winRuntime);
            winRibbon.ClientSize = new System.Drawing.Size(
                (int)Math.Ceiling(90d * winRibbon.DeviceDpi / 96d),
                120);
            wpfRibbon.Measure(new System.Windows.Size(90d, 120d));
            wpfRibbon.Arrange(new System.Windows.Rect(0d, 0d, 90d, 120d));
            wpfRibbon.UpdateLayout();
            wpfRibbon.Rebuild();
            winRibbon.Rebuild();

            wpfRibbon.EnterKeyTipMode();
            Assert.IsTrue(wpfRibbon.ProcessKeyTipAsync(wpfRuntime.KeyTips.TabTips["view"])
                .AsTask().GetAwaiter().GetResult());
            winRibbon.EnterKeyTipMode();
            Assert.IsTrue(winRibbon.ProcessKeyTipAsync(winRuntime.KeyTips.TabTips["view"])
                .AsTask().GetAwaiter().GetResult());

            Assert.AreEqual(
                RibbonItemSize.Compact,
                wpfRibbon.LayoutSnapshot.Tabs[0].Groups[0].Items[0].Size);
            Assert.AreEqual(
                RibbonItemSize.Compact,
                winRibbon.LayoutSnapshot.Tabs[0].Groups[0].Items[0].Size);
            var wpfTab = (System.Windows.Controls.TabItem)wpfRibbon.NativeTabControl.Items[0];
            var wpfGroups = (System.Windows.Controls.StackPanel)wpfTab.Content;
            var wpfGroup = (System.Windows.Controls.GroupBox)wpfGroups.Children[0];
            var wpfItems = (System.Windows.Controls.Canvas)wpfGroup.Content;
            var wpfButton = (WpfButtonBase)wpfItems.Children[0];
            var wpfContent = (System.Windows.Controls.Grid)wpfButton.Content;
            var badge = wpfContent.Children.OfType<System.Windows.Controls.Border>().Single();
            var wpfText = (System.Windows.Controls.TextBlock)badge.Child;
            var winButton = FindWinFormsDescendants<WinFormsButtonBase>(winRibbon)
                .Single(control => control.Tag is CommandId);
            Assert.IsTrue(wpfRuntime.KeyTips.TryGetCommandTip("view.gridlines", out var expectedTip));
            Assert.AreEqual(expectedTip, wpfText.Text);
            Assert.IsNotNull(badge.BorderBrush);
            StringAssert.Contains(winButton.Text, "[");
            Assert.IsNotNull(winButton.Image);
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopRibbonRebuildShouldPreserveSelectedTabIdentity()
    {
        RunInSta(() =>
        {
            var registry = new CommandRegistry();
            registry.Register(
                new CommandDescriptor("home.command", "Trang đầu"),
                new OneShotHandler(isChecked: null));
            registry.Register(
                new CommandDescriptor("insert.command", "Chèn"),
                new OneShotHandler(isChecked: null));
            var definition = new RibbonDefinition(
            [
                new RibbonTabDefinition("home", "Trang đầu", [
                    new RibbonGroupDefinition("home.group", "Trang đầu", [
                        new RibbonItemDefinition("home.command"),
                    ]),
                ]),
                new RibbonTabDefinition("insert", "Chèn", [
                    new RibbonGroupDefinition("insert.group", "Chèn", [
                        new RibbonItemDefinition("insert.command"),
                    ]),
                ]),
            ]);
            using var wpfRibbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(
                new RibbonRuntimeController(definition, registry));
            using var winFormsRibbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(
                new RibbonRuntimeController(definition, registry));
            wpfRibbon.Measure(new System.Windows.Size(400d, 120d));
            wpfRibbon.Arrange(new System.Windows.Rect(0d, 0d, 400d, 120d));
            wpfRibbon.UpdateLayout();
            wpfRibbon.Rebuild();
            winFormsRibbon.ClientSize = new System.Drawing.Size(400, 120);
            winFormsRibbon.Rebuild();
            var wpfTabs = FindWpfDescendants<System.Windows.Controls.TabControl>(
                    wpfRibbon)
                .Single();
            var winFormsTabs = FindWinFormsDescendants<System.Windows.Forms.TabControl>(
                    winFormsRibbon)
                .Single();
            wpfTabs.SelectedIndex = 1;
            winFormsTabs.SelectedIndex = 1;

            wpfRibbon.Rebuild();
            winFormsRibbon.Rebuild();

            Assert.AreEqual(
                "insert",
                ((System.Windows.Controls.TabItem)wpfTabs.SelectedItem).Tag);
            Assert.AreEqual("insert", winFormsTabs.SelectedTab!.Tag);
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

    private static RibbonDefinition CreateResponsiveRibbonDefinition() =>
        new(
        [
            new RibbonTabDefinition(
                "home",
                "Trang đầu",
                [
                    new RibbonGroupDefinition(
                        "high",
                        "Quan trọng",
                        [new RibbonItemDefinition("home.high", IsLarge: true)],
                        order: 0,
                        collapsePriority: 10),
                    new RibbonGroupDefinition(
                        "left",
                        "Trái",
                        [new RibbonItemDefinition("home.left", IsLarge: true)]),
                    new RibbonGroupDefinition(
                        "right",
                        "Phải",
                        [new RibbonItemDefinition("home.right", IsLarge: true)]),
                ]),
        ]);

    private static RibbonDefinition CreateContextualRibbonDefinition() => new(
        [
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("clipboard", "Bảng tạm", [new("home.copy")])]),
            new RibbonTabDefinition("table-design", "Thiết kế Bảng", [
                new RibbonGroupDefinition("table", "Bảng", [new("table.rename")])]),
        ],
        [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
        [new RibbonCommandSurfaceItem("home.copy", "1")],
        [new RibbonCommandSurfaceItem("file.save", "S")]);

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

    private static void RaiseWinFormsKey(WinFormsForm form, System.Windows.Forms.Keys key)
    {
        var onKeyDown = typeof(WinFormsControl).GetMethod(
            "OnKeyDown",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic) ??
            throw new AssertFailedException("Control.OnKeyDown was not found.");
        onKeyDown.Invoke(form, [new System.Windows.Forms.KeyEventArgs(key)]);
    }

    private static WpfKeyEventArgs RaiseWpfKey(WpfWindow window, WpfKey key)
    {
        var source = WpfPresentationSource.FromVisual(window) ??
            throw new AssertFailedException(
                "The WPF window did not have a presentation source.");
        var args = new WpfKeyEventArgs(
            WpfKeyboard.PrimaryDevice,
            source,
            Environment.TickCount,
            key)
        {
            RoutedEvent = WpfKeyboard.PreviewKeyDownEvent,
        };
        window.RaiseEvent(args);
        return args;
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
