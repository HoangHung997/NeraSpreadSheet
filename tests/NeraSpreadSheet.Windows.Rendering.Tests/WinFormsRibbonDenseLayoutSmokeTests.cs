using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;
using System.Drawing;
using System.Windows.Forms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsRibbonDenseLayoutSmokeTests
{
    [TestMethod]
    [Timeout(120_000)]
    public void LoadedRibbonShouldConsumePackedBoundsAndRetainWorksheetFocusAtEveryWidth()
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                VerifyDenseRibbon();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90d)));
        failure?.Throw();
    }

    private static void VerifyDenseRibbon()
    {
        var registry = new CommandRegistry();
        var items = new List<RibbonItemDefinition>();
        var handlers = new List<EnabledHandler>();
        for (var index = 0; index < 8; index++)
        {
            var commandId = new CommandId($"dense.command-{index}");
            var handler = new EnabledHandler();
            handlers.Add(handler);
            registry.Register(new CommandDescriptor(commandId, $"Lệnh mẫu {index}", iconKey: "edit.copy"), handler);
            items.Add(new RibbonItemDefinition(commandId, RibbonItemKind.Button, isLarge: index == 0, order: index));
        }
        var runtime = new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [new RibbonGroupDefinition("commands", "Lệnh chỉnh sửa", items)]),
        ], contextualTabs: [], quickAccessToolbar: [new RibbonCommandSurfaceItem("dense.command-0", "1")], backstage: [new RibbonCommandSurfaceItem("dense.command-0", "A")]), registry);
        using var form = new Form { ClientSize = new Size(1540, 400), ShowInTaskbar = false };
        using var ribbon = new NeraSpreadSheet.WinForms.NeraRibbonControl(runtime) { Dock = DockStyle.Top, Height = 180 };
        var worksheet = new TextBox { Dock = DockStyle.Bottom, Text = "Ô trang tính" };
        form.Controls.Add(ribbon);
        form.Controls.Add(worksheet);
        form.Show();
        Application.DoEvents();
        Assert.IsTrue(worksheet.Focus());
        Application.DoEvents();
        Assert.IsTrue(worksheet.Focused, "The worksheet sibling did not receive native focus before resize.");
        foreach (var theme in Enum.GetValues<NeraIconTheme>())
        {
            ribbon.IconTheme = theme;
            foreach (var width in new[] { 1536, 1280, 1024, 820 })
            {
                form.ClientSize = new Size((int)Math.Round(width * ribbon.LayoutSnapshot.Scale), 400);
                Application.DoEvents();
                ribbon.Rebuild();
                Application.DoEvents();
                Assert.IsTrue(worksheet.Focused, $"Resize or theme refresh moved focus away from the worksheet at {width} logical px ({theme}); active={form.ActiveControl?.Name}.");
                var group = ribbon.LayoutSnapshot.Tabs[0].Groups[0];
                Assert.AreEqual(RibbonGroupLayoutMode.Expanded, group.Mode);
                var nativeGroup = Descendants<Panel>(ribbon).Single(static panel => panel.Name == "ribbon-group-commands");
                var caption = nativeGroup.Controls.OfType<Label>().Single();
                Assert.IsTrue(caption.Top >= group.Items.Max(static item => item.Y + item.Height) - 1d);
                var bounds = new List<Rectangle>();
                foreach (var item in group.Items)
                {
                    var native = nativeGroup.Controls.Cast<Control>().Single(control => control.Name == $"ribbon-command-{item.Presentation.Command.CommandId.Value}");
                    Assert.AreEqual((int)Math.Round(item.X), native.Left);
                    Assert.AreEqual((int)Math.Round(item.Y), native.Top);
                    Assert.AreEqual((int)Math.Round(item.Width), native.Width);
                    Assert.AreEqual((int)Math.Round(item.Height), native.Height);
                    Assert.IsTrue(bounds.All(rectangle => !rectangle.IntersectsWith(native.Bounds)));
                    bounds.Add(native.Bounds);
                }
                Assert.IsTrue(group.Items.Select(static item => item.Row).Distinct().Count() >= 3);
                var qat = Descendants<Button>(ribbon).Single(static button => button.Name == "ribbon-qat-dense.command-0");
                Assert.IsNotNull(qat.Image);
                Assert.AreEqual(string.Empty, qat.Text);
                Assert.IsTrue(qat.Width <= Math.Ceiling(28d * ribbon.LayoutSnapshot.Scale));
            }
        }
        Descendants<Button>(ribbon).Single(static button => button.Name == "ribbon-file").PerformClick();
        Application.DoEvents();
        var navigation = Descendants<FlowLayoutPanel>(ribbon).Single(static panel => panel.Name == "ribbon-backstage-navigation");
        var content = Descendants<TableLayoutPanel>(ribbon).Single(static panel => panel.Name == "ribbon-backstage-content");
        Assert.IsTrue(navigation.Visible);
        Assert.IsTrue(content.Visible);
        Assert.IsTrue(content.Left >= navigation.Right);
        Descendants<Button>(ribbon).Single(static button => button.Name == "ribbon-backstage-dense.command-0").PerformClick();
        Application.DoEvents();
        Assert.AreEqual(0, handlers[0].ExecutionCount, "Selecting a backstage navigation entry executed its document action.");
        Descendants<Button>(ribbon).Single(static button => button.Name == "ribbon-backstage-dense.command-0-execute").PerformClick();
        Application.DoEvents();
        Assert.AreEqual(1, handlers[0].ExecutionCount, "The backstage content action did not execute exactly once.");
        form.Close();
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class EnabledHandler : ICommandHandler
    {
        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            return ValueTask.CompletedTask;
        }
    }
}
