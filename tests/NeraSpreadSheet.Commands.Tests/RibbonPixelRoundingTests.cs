using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonPixelRoundingTests
{
    // Actual group widths reported by the native 820-DIP regression on 1d0154e.
    private static readonly double[] ObservedGroupWidths = [96.5, 349.5, 92, 134, 66, 72];

    [TestMethod]
    public void PixelBudgetShouldReserveEachGroupBeforeSelectingOverflow()
    {
        var presentation = CreatePresentation();
        var engine = new RibbonResponsiveLayoutEngine();
        var legacy = engine.Layout(presentation, new RibbonLayoutRequest(820));
        Assert.AreEqual(820d, legacy.Tabs[0].InlineWidth);
        Assert.IsFalse(legacy.Tabs[0].HasOverflow);
        var expanded = engine.Layout(presentation, new RibbonLayoutRequest(double.PositiveInfinity) { RoundGroupWidthsToPixels = true });
        Assert.AreEqual(821d, expanded.Tabs[0].InlineWidth);
        var fixedLayout = engine.Layout(presentation, new RibbonLayoutRequest(820) { RoundGroupWidthsToPixels = true });
        Assert.IsTrue(fixedLayout.Tabs[0].HasOverflow);
        Assert.IsTrue(fixedLayout.Tabs[0].InlineWidth <= 820);
        CollectionAssert.AreEqual(presentation.Tabs[0].Groups.SelectMany(group => group.Items).Select(item => item.Command.CommandId).ToArray(),
            fixedLayout.Tabs[0].Groups.SelectMany(group => group.Items).Select(item => item.Presentation.Command.CommandId).ToArray());
    }

    [TestMethod]
    [DataRow(1d)]
    [DataRow(1.25d)]
    [DataRow(1.5d)]
    [DataRow(2d)]
    public void OptInShouldReservePhysicalWidthsAndGapsWithoutChangingItems(double scale)
    {
        var engine = new RibbonResponsiveLayoutEngine(); var presentation = CreatePresentation();
        var ordinary = engine.Layout(presentation, new RibbonLayoutRequest(double.PositiveInfinity, scale));
        var aligned = engine.Layout(presentation, new RibbonLayoutRequest(double.PositiveInfinity, scale) { RoundGroupWidthsToPixels = true });
        for (var index = 0; index < ObservedGroupWidths.Length; index++)
        {
            var group = aligned.Tabs[0].Groups[index];
            Assert.AreEqual(Math.Ceiling(ObservedGroupWidths[index] * scale), group.Width, 1e-8);
            Assert.AreEqual(ordinary.Tabs[0].Groups[index].Items[0].Width, group.Items[0].Width, 1e-8);
            var launcher = group.Items.Single(item => item.Presentation.Definition.IsDialogLauncher);
            Assert.AreEqual(group.CaptionY, launcher.Y);
            Assert.IsTrue(launcher.X + launcher.Width <= group.Width + 1e-8);
        }
        var expected = ObservedGroupWidths.Sum(width => Math.Ceiling(width * scale)) +
            (ObservedGroupWidths.Length - 1) * Math.Ceiling(2 * scale);
        Assert.AreEqual(expected, aligned.Tabs[0].InlineWidth, 1e-8);
    }

    private static RibbonPresentationSnapshot CreatePresentation()
    {
        var registry = new CommandRegistry(); var groups = new List<RibbonGroupDefinition>();
        for (var index = 0; index < ObservedGroupWidths.Length; index++)
        {
            var id = index.ToString(CultureInfo.InvariantCulture); var width = ObservedGroupWidths[index] - 8;
            registry.Register(new CommandDescriptor("command." + id, "Action"), new Handler());
            registry.Register(new CommandDescriptor("dialog." + id, "Settings"), new Handler());
            groups.Add(new RibbonGroupDefinition(id, "G",
                [new RibbonItemDefinition("command." + id, RibbonItemKind.Button, measurement: _ => width), RibbonItemDefinition.DialogLauncher("dialog." + id)]));
        }
        return new RibbonPresentationProjector(registry).Project(new RibbonDefinition([new RibbonTabDefinition("home", "Home", groups)]));
    }

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
