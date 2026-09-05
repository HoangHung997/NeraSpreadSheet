using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopRibbonCustomizationDialogSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void ClosingDesktopDialogsShouldDiscardPreviewAfterTheLastApply()
    {
        RunInSta(() =>
        {
            var target = RibbonCustomizationTarget.Tab("home");
            var wpfRuntime = CreateSharedCommandRuntime();
            var wpf = new NeraSpreadSheet.Wpf.NeraRibbonCustomizationDialog(wpfRuntime)
            { ShowInTaskbar = false, Left = -32_000d, Top = -32_000d };
            wpf.Show();
            wpf.UpdateLayout();
            var catalog = FindWpfElements(wpf).OfType<System.Windows.Controls.ListBox>()
                .Single(list => System.Windows.Automation.AutomationProperties.GetAutomationId(list) == "RibbonCustomizationCatalog");
            var catalogItem = (System.Windows.Controls.ListBoxItem)catalog.ItemContainerGenerator.ContainerFromIndex(0);
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(catalogItem);
            Assert.AreEqual(((RibbonCommandCatalogEntry)catalog.Items[0]).Caption, peer.GetName(),
                "Screen readers need the command caption, without the record's technical ToString output.");
            Assert.IsTrue(FindWpfElements(wpf).OfType<System.Windows.Controls.Button>().Single(button =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "RibbonCustomizationCancel").IsCancel);
            wpf.Session.Rename(target, "Đã áp dụng");
            wpf.ApplyCustomization();
            wpf.Session.Rename(target, "Chưa áp dụng");
            wpf.PreviewCustomization();
            Assert.AreEqual("Chưa áp dụng", wpfRuntime.Snapshot.Tabs[0].Caption);
            wpf.Close();
            Assert.AreEqual("Đã áp dụng", wpfRuntime.Snapshot.Tabs[0].Caption);

            var winRuntime = CreateSharedCommandRuntime();
            using var win = new NeraSpreadSheet.WinForms.NeraRibbonCustomizationDialog(winRuntime)
            { ShowInTaskbar = false, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Location = new System.Drawing.Point(-32_000, -32_000) };
            win.Show();
            Assert.IsNotNull(win.CancelButton, "Escape must use the native cancel button.");
            win.Session.Rename(target, "Đã áp dụng");
            win.ApplyCustomization();
            win.Session.Rename(target, "Chưa áp dụng");
            win.PreviewCustomization();
            Assert.AreEqual("Chưa áp dụng", winRuntime.Snapshot.Tabs[0].Caption);
            win.Close();
            Assert.AreEqual("Đã áp dụng", winRuntime.Snapshot.Tabs[0].Caption);
        });
    }

    private static IEnumerable<System.Windows.DependencyObject> FindWpfElements(System.Windows.DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in FindWpfElements(System.Windows.Media.VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    [TestMethod]
    [Timeout(120_000)]
    public void ThemedDesktopDialogsShouldApplyAndCancelCommandsSharedAcrossTabs()
    {
        RunInSta(() =>
        {
            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                var wpfRuntime = CreateSharedCommandRuntime();
                var wpf = new NeraSpreadSheet.Wpf.NeraRibbonCustomizationDialog(wpfRuntime)
                {
                    IconTheme = theme,
                    ShowInTaskbar = false,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Left = -32_000d,
                    Top = -32_000d,
                };
                try
                {
                    wpf.Show();
                    wpf.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });
                    if (theme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark)
                    {
                        var expectedForeground = ((System.Windows.Media.SolidColorBrush)wpf.FindResource("RibbonForeground")).Color;
                        var options = FindWpfCheckBoxes(wpf).ToArray();
                        Assert.HasCount(2, options);
                        foreach (var option in options)
                        {
                            Assert.AreEqual(expectedForeground, ((System.Windows.Media.SolidColorBrush)option.Foreground).Color,
                                $"The effective checkbox label foreground must follow {theme} instead of the native default black.");
                        }
                    }
                    Assert.AreEqual(2, wpf.Session.Entries.Count(entry => entry.Target.Kind == RibbonCustomizationTargetKind.Command && entry.Caption == "Sao chép"));
                    wpf.SelectedTarget = RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy");
                    Assert.IsTrue(wpf.SetSelectedVisible(false));
                    wpf.ApplyCustomization();
                    Assert.IsTrue(wpf.SetSelectedVisible(true));
                    wpf.CancelCustomization();
                    Assert.AreEqual(1, wpfRuntime.Snapshot.Tabs.SelectMany(static tab => tab.Groups).SelectMany(static group => group.Items).Count());
                    Assert.AreEqual("Sao chép", wpfRuntime.Snapshot.Tabs.SelectMany(static tab => tab.Groups).SelectMany(static group => group.Items).Single().Command.Caption);
                }
                finally { wpf.Close(); }

                var winRuntime = CreateSharedCommandRuntime();
                using var win = new NeraSpreadSheet.WinForms.NeraRibbonCustomizationDialog(winRuntime)
                {
                    IconTheme = theme,
                    ShowInTaskbar = false,
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-32_000, -32_000),
                };
                win.Show();
                System.Windows.Forms.Application.DoEvents();
                Assert.AreEqual(2, win.Session.Entries.Count(entry => entry.Target.Kind == RibbonCustomizationTargetKind.Command && entry.Caption == "Sao chép"));
                win.SelectedTarget = RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy");
                Assert.IsTrue(win.SetSelectedVisible(false));
                win.ApplyCustomization();
                Assert.IsTrue(win.SetSelectedVisible(true));
                win.CancelCustomization();
                Assert.AreEqual(1, winRuntime.Snapshot.Tabs.SelectMany(static tab => tab.Groups).SelectMany(static group => group.Items).Count());
                Assert.AreNotEqual(win.BackColor, win.ForeColor);
                win.Close();
            }
        });
    }

    private static RibbonRuntimeController CreateSharedCommandRuntime()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("edit.copy", "Sao chép"), new EnabledHandler());
        return new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [new RibbonGroupDefinition("clipboard", "Bảng tạm", [new RibbonItemDefinition("edit.copy")])]),
            new RibbonTabDefinition("insert", "Chèn", [new RibbonGroupDefinition("clipboard", "Bảng tạm", [new RibbonItemDefinition("edit.copy")])]),
        ]), registry);
    }

    private static IEnumerable<System.Windows.Controls.CheckBox> FindWpfCheckBoxes(System.Windows.DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is System.Windows.Controls.CheckBox checkBox) yield return checkBox;
            foreach (var descendant in FindWpfCheckBoxes(child)) yield return descendant;
        }
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfDialogShouldLoadApplyPersistAndResetCustomization()
    {
        RunInSta(() =>
        {
            var runtime = CreateRuntime();
            var dialog = new NeraSpreadSheet.Wpf.NeraRibbonCustomizationDialog(runtime)
            {
                ShowInTaskbar = false,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = -32_000d,
                Top = -32_000d,
            };
            try
            {
                dialog.Show();
                dialog.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(static () => { }));
                var customTab = dialog.AddCustomTab("custom", "Cá nhân");
                var customGroup = dialog.AddCustomGroup(customTab.TabId, "quick", "Lệnh nhanh");
                dialog.MoveCommand(
                    RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy"),
                    customTab.TabId,
                    customGroup.GroupId!);
                dialog.AddToQuickAccessToolbar("edit.copy");
                dialog.PreviewCustomization();
                Assert.AreEqual("custom", runtime.Snapshot.Tabs[1].Id);
                Assert.AreEqual("edit.copy", runtime.Snapshot.QuickAccessToolbar[0].CommandId.Value);
                dialog.CancelCustomization();
                Assert.AreEqual(1, runtime.Snapshot.Tabs.Count);
                dialog.SelectedTarget = RibbonCustomizationTarget.Command(
                    "home",
                    "clipboard",
                    "edit.copy");

                Assert.IsTrue(dialog.SetSelectedVisible(false));
                Assert.AreEqual(1, runtime.Snapshot.Tabs[0].Groups[0].Items.Count);
                StringAssert.Contains(
                    dialog.SaveCustomizationJson(),
                    RibbonCustomizationJsonSerializer.SchemaName);

                dialog.ResetCustomization();
                Assert.AreEqual(2, runtime.Snapshot.Tabs[0].Groups[0].Items.Count);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsDialogShouldLoadApplyPersistAndResetCustomization()
    {
        RunInSta(() =>
        {
            var runtime = CreateRuntime();
            using var dialog = new NeraSpreadSheet.WinForms.NeraRibbonCustomizationDialog(runtime)
            {
                ShowInTaskbar = false,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
            };
            dialog.Show();
            System.Windows.Forms.Application.DoEvents();
            var customTab = dialog.AddCustomTab("custom", "Cá nhân");
            var customGroup = dialog.AddCustomGroup(customTab.TabId, "quick", "Lệnh nhanh");
            dialog.MoveCommand(
                RibbonCustomizationTarget.Command("home", "clipboard", "edit.paste"),
                customTab.TabId,
                customGroup.GroupId!);
            dialog.PreviewCustomization();
            Assert.AreEqual("custom", runtime.Snapshot.Tabs[1].Id);
            dialog.ApplyCustomization();
            dialog.Session.Rename(customTab, "Tạm thời");
            dialog.CancelCustomization();
            Assert.AreEqual("Cá nhân", runtime.Snapshot.Tabs[1].Caption);
            dialog.SelectedTarget = RibbonCustomizationTarget.Command(
                "home",
                "clipboard",
                "edit.copy");

            Assert.IsTrue(dialog.SetSelectedLarge(true));
            Assert.IsTrue(runtime.Snapshot.Tabs[0].Groups[0].Items[0].IsLarge);
            var json = dialog.SaveCustomizationJson();
            dialog.ResetCustomization();
            Assert.IsFalse(runtime.Snapshot.Tabs[0].Groups[0].Items[0].IsLarge);

            dialog.LoadCustomizationJson(json);
            Assert.IsTrue(runtime.Snapshot.Tabs[0].Groups[0].Items[0].IsLarge);
            dialog.Close();
            System.Windows.Forms.Application.DoEvents();
        });
    }

    private static RibbonRuntimeController CreateRuntime()
    {
        var registry = new CommandRegistry();
        registry.Register(
            new CommandDescriptor("edit.copy", "Sao chép"),
            new EnabledHandler());
        registry.Register(
            new CommandDescriptor("edit.paste", "Dán"),
            new EnabledHandler());
        return new RibbonRuntimeController(
            new RibbonDefinition(
            [
                new RibbonTabDefinition(
                    "home",
                    "Trang đầu",
                    [
                        new RibbonGroupDefinition(
                            "clipboard",
                            "Bảng tạm",
                            [
                                new RibbonItemDefinition("edit.copy"),
                                new RibbonItemDefinition("edit.paste", Order: 1),
                            ]),
                    ]),
            ]),
            registry);
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
            Name = "Nera Ribbon customization dialog smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The Ribbon customization dialog smoke timed out.");
        }
        failure?.Throw();
    }

    private sealed class EnabledHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
