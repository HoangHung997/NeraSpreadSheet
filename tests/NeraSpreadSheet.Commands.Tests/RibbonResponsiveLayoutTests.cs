using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonResponsiveLayoutTests
{
    [TestMethod]
    public void LayoutShouldCollapseDeterministicallyByPriorityAndPosition()
    {
        var snapshot = CreatePresentation();
        var engine = new RibbonResponsiveLayoutEngine();

        var layout = engine.Layout(snapshot, new RibbonLayoutRequest(260d));

        CollectionAssert.AreEqual(
            new[]
            {
                RibbonGroupLayoutMode.Expanded,
                RibbonGroupLayoutMode.Compact,
                RibbonGroupLayoutMode.Overflow,
            },
            layout.Tabs[0].Groups.Select(static group => group.Mode).ToArray());
        Assert.IsTrue(layout.Tabs[0].HasOverflow);
        Assert.IsLessThanOrEqualTo(260d, layout.Tabs[0].InlineWidth);
    }

    [TestMethod]
    public void LayoutShouldUseTheSamePhysicalResultAtSupportedDpiScales()
    {
        var snapshot = CreatePresentation();
        var engine = new RibbonResponsiveLayoutEngine();
        double[] scales = [1d, 1.25d, 1.5d, 2d];

        var modes = scales.Select(scale => engine.Layout(
                snapshot,
                new RibbonLayoutRequest(420d * scale, scale)))
            .Select(layout => layout.Tabs[0].Groups
                .Select(static group => group.Mode)
                .ToArray())
            .ToArray();

        foreach (var current in modes.Skip(1))
        {
            CollectionAssert.AreEqual(modes[0], current);
        }
    }

    [TestMethod]
    public void LayoutShouldSelectLargeSmallCompactAndOverflowAtThresholds()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("one", "Một"), new EnabledHandler());
        registry.Register(new CommandDescriptor("two", "Hai"), new EnabledHandler());
        var presentation = new RibbonPresentationProjector(registry).Project(
            new RibbonDefinition(
            [
                new RibbonTabDefinition(
                    "home",
                    "Trang đầu",
                    [
                        new RibbonGroupDefinition(
                            "commands",
                            "Lệnh",
                            [
                                new RibbonItemDefinition("one", IsLarge: true),
                                new RibbonItemDefinition("two"),
                            ]),
                    ]),
            ]));
        var engine = new RibbonResponsiveLayoutEngine();

        var expanded = engine.Layout(presentation, new RibbonLayoutRequest(184d));
        var small = engine.Layout(presentation, new RibbonLayoutRequest(175d));
        var compact = engine.Layout(presentation, new RibbonLayoutRequest(108d));
        var overflow = engine.Layout(presentation, new RibbonLayoutRequest(60d));

        CollectionAssert.AreEqual(
            new[] { RibbonItemSize.Large, RibbonItemSize.Small },
            expanded.Tabs[0].Groups[0].Items.Select(static item => item.Size).ToArray());
        CollectionAssert.AreEqual(
            new[] { RibbonItemSize.Small, RibbonItemSize.Small },
            small.Tabs[0].Groups[0].Items.Select(static item => item.Size).ToArray());
        CollectionAssert.AreEqual(
            new[] { RibbonItemSize.Compact, RibbonItemSize.Compact },
            compact.Tabs[0].Groups[0].Items.Select(static item => item.Size).ToArray());
        Assert.AreEqual(RibbonGroupLayoutMode.Overflow, overflow.Tabs[0].Groups[0].Mode);
    }

    [TestMethod]
    public void LayoutShouldRemainBoundedDuringContinuousResize()
    {
        var snapshot = CreatePresentation();
        var engine = new RibbonResponsiveLayoutEngine();

        for (var width = 600; width >= 60; width--)
        {
            var layout = engine.Layout(snapshot, new RibbonLayoutRequest(width));

            Assert.IsLessThanOrEqualTo(width, layout.Tabs[0].InlineWidth);
        }
    }

    [TestMethod]
    public void LayoutShouldPreserveStableSelectionAndFocusAcrossRebuild()
    {
        var snapshot = CreatePresentation();
        var engine = new RibbonResponsiveLayoutEngine();
        var first = engine.Layout(
            snapshot,
            new RibbonLayoutRequest(
                800d,
                selectedTabId: "insert",
                focusedCommandId: "home.low"));

        var resized = engine.Layout(
            snapshot,
            new RibbonLayoutRequest(
                100d,
                selectedTabId: first.SelectedTabId,
                focusedCommandId: first.FocusedCommandId));

        Assert.AreEqual("insert", resized.SelectedTabId);
        Assert.AreEqual(new CommandId("home.low"), resized.FocusedCommandId);
        Assert.IsTrue(resized.Tabs[0].HasOverflow);
    }

    [TestMethod]
    public void LayoutShouldFallbackWhenRequestedIdentitiesDisappear()
    {
        var layout = new RibbonResponsiveLayoutEngine().Layout(
            CreatePresentation(),
            new RibbonLayoutRequest(
                800d,
                selectedTabId: "missing",
                focusedCommandId: "missing.command"));

        Assert.AreEqual("home", layout.SelectedTabId);
        Assert.IsNull(layout.FocusedCommandId);
    }

    [TestMethod]
    public void LayoutShouldRejectInvalidMeasurementInput()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new RibbonLayoutRequest(100d, scale: 0d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new RibbonLayoutRequest(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new RibbonLayoutRequest(
                100d,
                metrics: new RibbonLayoutMetrics { CompactItemWidth = -1d }));
    }

    private static RibbonPresentationSnapshot CreatePresentation()
    {
        var registry = new CommandRegistry();
        foreach (var id in new[] { "home.high", "home.tie-left", "home.low" })
        {
            registry.Register(new CommandDescriptor(id, id), new EnabledHandler());
        }
        return new RibbonPresentationProjector(registry).Project(new RibbonDefinition(
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
                        "tie-left",
                        "Trái",
                        [new RibbonItemDefinition("home.tie-left", IsLarge: true)]),
                    new RibbonGroupDefinition(
                        "low",
                        "Phải",
                        [new RibbonItemDefinition("home.low", IsLarge: true)]),
                ]),
            new RibbonTabDefinition("insert", "Chèn", []),
        ]));
    }

    private sealed class EnabledHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.CompletedTask;
    }
}
