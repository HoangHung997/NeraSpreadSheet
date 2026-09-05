using System.Globalization;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopPresentationLocalizationSmokeTests
{
    [TestMethod]
    [Timeout(120_000)]
    public void LoadedWpfChromeShouldSwitchCultureAndExposeFocusCheckedAndDisabledStates()
    {
        RunInSta(() =>
        {
            var runtime = CreateRuntime();
            using var ribbon = new NeraSpreadSheet.Wpf.NeraRibbonControl(runtime) { Width = 1024d };
            var window = new Window { Content = ribbon, Width = 1100d, Height = 250d, ShowInTaskbar = false };
            try
            {
                window.Show();
                Flush(window);
                foreach (var theme in Enum.GetValues<NeraIconTheme>())
                {
                    ribbon.IconTheme = theme;
                    Flush(window);
                    var toggle = Descendants<ToggleButton>(ribbon).Single(button =>
                        AutomationProperties.GetAutomationId(button) == "ribbon-command-Cell.Format.Bold");
                    Assert.IsTrue(toggle.IsChecked);
                    Assert.AreEqual("Đậm", AutomationProperties.GetName(toggle));
                    var disabled = Descendants<Button>(ribbon).Single(button =>
                        AutomationProperties.GetAutomationId(button) == "ribbon-command-Edit.Copy");
                    Assert.IsFalse(disabled.IsEnabled);
                    Assert.AreEqual("Sao chép", AutomationProperties.GetName(disabled));
                    toggle.ApplyTemplate();
                    var focusRing = toggle.Template.FindName("FocusRing", toggle) as System.Windows.Shapes.Rectangle;
                    Assert.IsNotNull(focusRing);
                    Assert.IsGreaterThan(0, focusRing.StrokeDashArray.Count);
                    var expected = theme == NeraIconTheme.HighContrastDark ? Colors.Black :
                        theme == NeraIconTheme.HighContrastLight ? Colors.White :
                        theme == NeraIconTheme.Dark ? Color.FromRgb(37, 37, 37) : Colors.White;
                    Assert.AreEqual(expected, ((SolidColorBrush)ribbon.Resources["RibbonSurface"]).Color);
                }
                runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-GB")));
                Flush(window);
                Assert.AreEqual("File", ribbon.FileCaption);
                Assert.AreEqual("Copy", AutomationProperties.GetName(Descendants<Button>(ribbon).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "ribbon-command-Edit.Copy")));
                ribbon.FileCaption = "Tệp của ứng dụng";
                runtime.SetLocalization(PresentationLocalization.Default);
                Flush(window);
                Assert.AreEqual("Tệp của ứng dụng", ribbon.FileCaption);
                Assert.IsNull(Application.Current?.Resources["RibbonSurface"], "SDK resources must stay local.");
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void LoadedWinFormsChromeShouldKeepNativeIdentityAndLocalizedFilterLabels()
    {
        RunInSta(() =>
        {
            var runtime = CreateRuntime();
            using var ribbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(runtime)
            { Dock = System.Windows.Forms.DockStyle.Top };
            using var form = new System.Windows.Forms.Form
            { Width = 1100, Height = 300, ShowInTaskbar = false };
            form.Controls.Add(ribbon);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                ribbon.IconTheme = theme;
                System.Windows.Forms.Application.DoEvents();
                Assert.AreEqual(theme, ribbon.IconTheme);
            }
            runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-US")));
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual("File", ribbon.FileCaption);
            var copy = WinFormsDescendants(ribbon).Single(control => control.AccessibleName == "Copy");
            Assert.IsFalse(copy.Enabled);
            Assert.IsTrue(copy.Name.Contains("Edit.Copy", StringComparison.Ordinal));
            using var filter = new NeraSpreadSheet.WinForms.NeraTableFilterDropDown(
                new SpreadsheetSession(new Workbook()),
                new PresentationLocalization(CultureInfo.GetCultureInfo("en-US")),
                NeraIconTheme.HighContrastDark);
            Assert.AreEqual("Apply", filter.Localization.Get("Áp dụng"));
            form.Close();
        });
    }

    private static RibbonRuntimeController CreateRuntime()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("Cell.Format.Bold", "Bold", iconKey: "font.bold"), new Handler(true, true));
        registry.Register(new CommandDescriptor("Edit.Copy", "Copy", iconKey: "edit.copy"), new Handler(false, null));
        return new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("commands", "Lệnh", [
                    new RibbonItemDefinition("Cell.Format.Bold", RibbonItemKind.Toggle),
                    new RibbonItemDefinition("Edit.Copy", RibbonItemKind.Button),
                ]),
            ]),
        ]), registry);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T target) yield return target;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static IEnumerable<System.Windows.Forms.Control> WinFormsDescendants(System.Windows.Forms.Control parent)
    {
        foreach (System.Windows.Forms.Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in WinFormsDescendants(child)) yield return nested;
        }
    }

    private static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(100d)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Handler(bool enabled, bool? isChecked) : IStatefulCommandHandler
    {
        public bool CanExecute(CommandContext context) => enabled;
        public CommandState GetState(CommandContext context) => new(enabled, isChecked);
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
