using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopRibbonCustomizationDialogSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

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
