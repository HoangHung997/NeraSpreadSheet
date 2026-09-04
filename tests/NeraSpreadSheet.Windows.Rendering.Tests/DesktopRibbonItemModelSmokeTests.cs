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
            Assert.AreEqual(1, FindWpfDescendants<System.Windows.Controls.Separator>(ribbon).Count());
            Assert.IsTrue(FindWpfDescendants<System.Windows.Controls.WrapPanel>(ribbon).Any());
            Assert.IsTrue(FindWpfDescendants<System.Windows.Controls.DockPanel>(ribbon).Any());
            var combo = FindWpfDescendants<System.Windows.Controls.ComboBox>(ribbon)
                .Single(control =>
                    System.Windows.Automation.AutomationProperties.GetName(control) ==
                    "Automation ComboBox");
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
        Assert.IsTrue(FindWinFormsDescendants<System.Windows.Forms.Panel>(ribbon)
            .Any(static panel => panel.Width == 1));
        var combo = FindWinFormsDescendants<System.Windows.Forms.ComboBox>(ribbon)
            .Single(static control => control.AccessibleName == "Automation ComboBox");
        combo.SelectedValue = "two";
        System.Windows.Forms.Application.DoEvents();

        Assert.AreEqual("two", handlers[RibbonItemKind.ComboBox].SelectedValue);
        Assert.AreEqual(1, handlers[RibbonItemKind.ComboBox].ExecutionCount);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
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
            var handler = new SelectionHandler();
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
        public string? SelectedValue { get; private set; } = "one";

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(
            true,
            SelectedValue: SelectedValue,
            ItemsSource:
            [
                new CommandItem("one", "Một"),
                new CommandItem("two", "Hai"),
            ]);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            SelectedValue = ((RibbonItemActivation)context.Parameter!).SelectedValue;
            return ValueTask.CompletedTask;
        }
    }
}
