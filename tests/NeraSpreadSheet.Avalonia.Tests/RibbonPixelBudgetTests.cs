using System.Globalization;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class RibbonPixelBudgetTests
{
    private static readonly double[] ObservedGroupWidths = [96.5, 349.5, 92, 134, 66, 72];

    [TestMethod]
    [DataRow(819d)]
    [DataRow(820d)]
    [DataRow(821d)]
    [DataRow(1024d)]
    public Task RoundedNativeGroupsShouldFitTheBudgetWithoutLosingLaunchers(double width) => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var registry = new CommandRegistry(); var groups = new List<RibbonGroupDefinition>();
        for (var index = 0; index < ObservedGroupWidths.Length; index++)
        {
            var id = index.ToString(CultureInfo.InvariantCulture); var itemWidth = ObservedGroupWidths[index] - 8;
            registry.Register(new CommandDescriptor("command." + id, "Action"), new Handler());
            registry.Register(new CommandDescriptor("dialog." + id, "Settings"), new Handler());
            groups.Add(new RibbonGroupDefinition(id, "G", [
                new RibbonItemDefinition("command." + id, RibbonItemKind.Button, measurement: _ => itemWidth),
                RibbonItemDefinition.DialogLauncher("dialog." + id),
            ]));
        }
        var runtime = new RibbonRuntimeController(new RibbonDefinition([new RibbonTabDefinition("home", "Home", groups)]), registry);
        using var ribbon = new NeraRibbonControl(runtime) { Width = width };
        var window = new Window { Width = 1100, Height = 300, Content = ribbon };
        try
        {
            window.Show(); window.UpdateLayout(); ribbon.Rebuild(); window.UpdateLayout();
            var layout = ribbon.LayoutSnapshot.Tabs[0];
            Assert.IsTrue(layout.InlineWidth <= width);
            Assert.AreEqual(12, layout.Groups.Sum(group => group.Items.Count));
            Assert.AreEqual(6, layout.Groups.SelectMany(group => group.Items).Count(item => item.Presentation.Definition.IsDialogLauncher));
            foreach (var group in layout.Groups.Where(group => group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                var native = ribbon.GetVisualDescendants().OfType<Border>().Single(control =>
                    AutomationProperties.GetAutomationId(control) == "ribbon-group-" + group.Presentation.Id);
                var origin = native.TranslatePoint(default, ribbon) ?? throw new InvalidOperationException("Detached group.");
                Assert.IsTrue(origin.X >= -0.6);
                Assert.IsTrue(origin.X + native.Bounds.Width <= ribbon.Bounds.Width + 0.6, "Native rounding must not overflow the available client.");
                var launcher = group.Items.Single(item => item.Presentation.Definition.IsDialogLauncher);
                var caption = native.GetVisualDescendants().OfType<TextBlock>().Single(control =>
                    AutomationProperties.GetAutomationId(control) == "ribbon-group-caption-" + group.Presentation.Id);
                Assert.IsTrue(caption.Bounds.Width <= launcher.X / ribbon.LayoutSnapshot.Scale);
            }
            if (width == 820) Assert.IsTrue(layout.HasOverflow);
            if (width >= 821) Assert.IsFalse(layout.HasOverflow);
        }
        finally { window.Content = null; window.Close(); }
    });

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
