using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonDenseLayoutTests
{
    private static readonly int[] ExpectedRows = [0, 0, 1, 2, 0, 1, 2, 0, 0];
    private static readonly int[] ExpectedRowSpans = [3, 1, 1, 1, 1, 1, 1, 3, 3];
    private static readonly int[] ExpectedColumns = [0, 1, 1, 1, 2, 2, 2, 3, 4];

    [TestMethod]
    public void LayoutShouldPackSmallCommandsInThreeRowsBesidePrimaryAndGallery()
    {
        var snapshot = CreatePresentation(1);
        var group = new RibbonResponsiveLayoutEngine().Layout(
            snapshot, new RibbonLayoutRequest(1536d)).Tabs[0].Groups[0];

        Assert.AreEqual(RibbonGroupLayoutMode.Expanded, group.Mode);
        CollectionAssert.AreEqual(ExpectedRows,
            group.Items.Select(static item => item.Row).ToArray());
        CollectionAssert.AreEqual(ExpectedRowSpans,
            group.Items.Select(static item => item.RowSpan).ToArray());
        CollectionAssert.AreEqual(ExpectedColumns,
            group.Items.Select(static item => item.Column).ToArray());
        Assert.AreEqual(4d, group.Items[0].X);
        Assert.AreEqual(4d, group.Items[0].Y);
        Assert.AreEqual(76d, group.Items[0].Height);
        Assert.AreEqual(24d, group.Items[1].Height);
        Assert.AreEqual(80d, group.CaptionY);
        Assert.AreEqual(18d, group.CaptionHeight);
        Assert.AreEqual(102d, group.Height);
        Assert.AreEqual(group.Items[1].Width, group.Items[2].Width);
        Assert.AreEqual(group.Items[2].Width, group.Items[3].Width);
        Assert.AreEqual(2, group.Items[0].CaptionMaxLines);
        Assert.AreEqual(1, group.Items[1].CaptionMaxLines);
        Assert.IsFalse(group.Items[7].CaptionVisible);
        AssertGeometry(group);
    }

    [TestMethod]
    public void LayoutShouldKeepBoundsCaptionsAndIdentityAcrossWidthAndDpiMatrix()
    {
        var presentation = CreatePresentation(6);
        var engine = new RibbonResponsiveLayoutEngine();
        double[] widths = [1536d, 1280d, 1024d, 820d];
        double[] scales = [1d, 1.25d, 1.5d, 2d];
        foreach (var width in widths)
        {
            var baseline = engine.Layout(presentation,
                new RibbonLayoutRequest(width, selectedTabId: "home", focusedCommandId: "g5.gallery"));
            foreach (var scale in scales)
            {
                var layout = engine.Layout(presentation,
                    new RibbonLayoutRequest(width * scale, scale, "home", "g5.gallery"));
                Assert.AreEqual("home", layout.SelectedTabId);
                Assert.AreEqual(new CommandId("g5.gallery"), layout.FocusedCommandId);
                Assert.AreEqual(54, layout.Tabs[0].Groups.Sum(static group => group.Items.Count));
                Assert.IsLessThanOrEqualTo(width * scale, layout.Tabs[0].InlineWidth);
                for (var groupIndex = 0; groupIndex < layout.Tabs[0].Groups.Count; groupIndex++)
                {
                    var group = layout.Tabs[0].Groups[groupIndex];
                    var reference = baseline.Tabs[0].Groups[groupIndex];
                    Assert.AreEqual(reference.Mode, group.Mode);
                    Assert.AreEqual(reference.Width, group.Width / scale, 0.00001d);
                    Assert.AreEqual(reference.CaptionY, group.CaptionY / scale, 0.00001d);
                    Assert.AreEqual(reference.Height, group.Height / scale, 0.00001d);
                    for (var index = 0; index < group.Items.Count; index++)
                    {
                        var item = group.Items[index];
                        var referenceItem = reference.Items[index];
                        Assert.AreEqual(referenceItem.Size, item.Size);
                        Assert.AreEqual(referenceItem.X, item.X / scale, 0.00001d);
                        Assert.AreEqual(referenceItem.Y, item.Y / scale, 0.00001d);
                        Assert.AreEqual(referenceItem.Width, item.Width / scale, 0.00001d);
                        Assert.AreEqual(referenceItem.Height, item.Height / scale, 0.00001d);
                        Assert.AreEqual(referenceItem.CaptionVisible, item.CaptionVisible);
                    }
                    if (group.Mode != RibbonGroupLayoutMode.Overflow)
                    {
                        AssertGeometry(group);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void LayoutShouldKeepCommandsInlineBeforeOverflowAndCollapseMonotonically()
    {
        var presentation = CreatePresentation(6);
        var engine = new RibbonResponsiveLayoutEngine();
        RibbonLayoutSnapshot? previous = null;
        for (var width = 2400; width >= 60; width--)
        {
            var layout = engine.Layout(presentation, new RibbonLayoutRequest(width));
            Assert.IsLessThanOrEqualTo(width, layout.Tabs[0].InlineWidth);
            if (previous is not null)
            {
                for (var groupIndex = 0; groupIndex < layout.Tabs[0].Groups.Count; groupIndex++)
                {
                    var group = layout.Tabs[0].Groups[groupIndex];
                    var earlier = previous.Tabs[0].Groups[groupIndex];
                    if (earlier.Mode == RibbonGroupLayoutMode.Overflow)
                    {
                        Assert.AreEqual(RibbonGroupLayoutMode.Overflow, group.Mode);
                    }
                    for (var itemIndex = 0; itemIndex < group.Items.Count; itemIndex++)
                    {
                        Assert.IsGreaterThanOrEqualTo((int)earlier.Items[itemIndex].Size,
                            (int)group.Items[itemIndex].Size);
                    }
                }
            }
            previous = layout;
        }
    }

    [TestMethod]
    public void LayoutShouldAllocateVisibleFallbackCaptionsWhenNativeIconsAreMissing()
    {
        var presentation = CreatePresentation(1);
        var fallback = new RibbonResponsiveLayoutEngine().Layout(presentation,
            new RibbonLayoutRequest(300d) { IsIconAvailable = static _ => false });
        foreach (var item in fallback.Tabs[0].Groups[0].Items)
        {
            if (item.Presentation.Kind != RibbonItemKind.Separator)
            {
                Assert.IsTrue(item.CaptionVisible);
                Assert.IsGreaterThan(28d, item.Width);
            }
        }
    }

    [TestMethod]
    public void LayoutShouldReserveTwoLineLargeCaptionsAndFullGroupCaptions()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("long", "Định dạng có điều kiện", iconKey: "format.conditional"),
            new EnabledHandler());
        var presentation = new RibbonPresentationProjector(registry).Project(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("format", "Định dạng trang tính", [new RibbonItemDefinition("long", true)]),
            ]),
        ]));
        var group = new RibbonResponsiveLayoutEngine().Layout(presentation,
            new RibbonLayoutRequest(1536d)).Tabs[0].Groups[0];

        Assert.IsTrue(group.Items[0].CaptionVisible);
        Assert.AreEqual(2, group.Items[0].CaptionMaxLines);
        Assert.IsGreaterThan(64d, group.Items[0].Width);
        Assert.IsGreaterThan(group.Items[0].Width, group.Width);
        AssertGeometry(group);
    }

    [TestMethod]
    public void LayoutShouldRetainImmutableGeometryCollectionsAndLegacyConstructors()
    {
        var layout = new RibbonResponsiveLayoutEngine().Layout(CreatePresentation(1), new RibbonLayoutRequest(1536d));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<RibbonTabLayout>)layout.Tabs).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<RibbonGroupLayout>)layout.Tabs[0].Groups).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<RibbonItemLayout>)layout.Tabs[0].Groups[0].Items).Clear());
        Assert.IsNotNull(typeof(RibbonItemLayout).GetConstructor(
            [typeof(RibbonItemPresentation), typeof(RibbonItemSize), typeof(double)]));
        Assert.IsNotNull(typeof(RibbonGroupLayout).GetConstructor(
            [typeof(RibbonGroupPresentation), typeof(RibbonGroupLayoutMode), typeof(IReadOnlyList<RibbonItemLayout>), typeof(double)]));
    }

    [TestMethod]
    public void LayoutShouldRejectInvalidDenseMetrics()
    {
        foreach (var metrics in new[]
        {
            new RibbonLayoutMetrics { RowCount = 0 },
            new RibbonLayoutMetrics { RowCount = 4 },
            new RibbonLayoutMetrics { RowHeight = 0d },
            new RibbonLayoutMetrics { RowSpacing = double.NaN },
            new RibbonLayoutMetrics { GroupPadding = -1d },
            new RibbonLayoutMetrics { GroupCaptionHeight = double.PositiveInfinity },
            new RibbonLayoutMetrics { RowHeight = double.MaxValue },
        })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RibbonLayoutRequest(820d, metrics: metrics));
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RibbonLayoutRequest(820d, scale: double.MaxValue));
    }

    [TestMethod]
    public void LayoutShouldSupportEmptyGroupsAndTwoRowPacking()
    {
        var empty = new RibbonPresentationProjector(new CommandRegistry()).Project(new RibbonDefinition([
            new RibbonTabDefinition("empty", "Trống", [new RibbonGroupDefinition("empty", "Nhóm trống", [])]),
        ]));
        var engine = new RibbonResponsiveLayoutEngine();
        var emptyGroup = engine.Layout(empty, new RibbonLayoutRequest(820d)).Tabs[0].Groups[0];
        Assert.AreEqual(0, emptyGroup.Items.Count);
        Assert.IsGreaterThan(8d, emptyGroup.Width);
        Assert.AreEqual(80d, emptyGroup.CaptionY);

        var twoRows = engine.Layout(CreatePresentation(1), new RibbonLayoutRequest(1536d,
            metrics: new RibbonLayoutMetrics { RowCount = 2 }));
        var group = twoRows.Tabs[0].Groups[0];
        Assert.IsTrue(group.Items.All(static item => item.Row is 0 or 1));
        Assert.IsTrue(group.Items.Where(static item => item.Size == RibbonItemSize.Large)
            .All(static item => item.RowSpan == 2));
        Assert.AreEqual(76d, group.Height);
        AssertGeometry(group);
    }

    [TestMethod]
    public void LayoutShouldRejectItemMeasurementThatOverflowsPhysicalCoordinates()
    {
        var presentation = new RibbonPresentationProjector(new CommandRegistry()).Project(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("group", "Nhóm", [
                    new RibbonItemDefinition("wide", RibbonItemKind.Button, measurement: static _ => double.MaxValue),
                ]),
            ]),
        ]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new RibbonResponsiveLayoutEngine().Layout(
            presentation, new RibbonLayoutRequest(820d, scale: 2d)));
    }

    [TestMethod]
    public void LayoutShouldReserveIndependentArrowSpaceForLargeAndCompactCommands()
    {
        var registry = new CommandRegistry();
        RibbonItemKind[] kinds = [RibbonItemKind.Button, RibbonItemKind.SplitButton, RibbonItemKind.DropDown, RibbonItemKind.Menu];
        var groups = new List<RibbonGroupDefinition>();
        foreach (var kind in kinds)
        {
            var id = $"item.{kind}";
            registry.Register(new CommandDescriptor(id, "Định dạng có điều kiện", iconKey: "format.conditional"), new EnabledHandler());
            groups.Add(new RibbonGroupDefinition(kind.ToString(), "A", [new RibbonItemDefinition(id, kind, isLarge: true)]));
        }
        var presentation = new RibbonPresentationProjector(registry).Project(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", groups),
        ]));
        var engine = new RibbonResponsiveLayoutEngine();
        var expanded = engine.Layout(presentation, new RibbonLayoutRequest(1536d, 1.5d));
        var largeButtonWidth = expanded.Tabs[0].Groups[0].Items[0].Width;
        foreach (var group in expanded.Tabs[0].Groups.Skip(1))
        {
            Assert.AreEqual(largeButtonWidth + 18d * 1.5d, group.Items[0].Width);
            Assert.AreEqual(2, group.Items[0].CaptionMaxLines);
        }
        var compact = engine.Layout(presentation, new RibbonLayoutRequest(60d));
        foreach (var group in compact.Tabs[0].Groups.Skip(1))
        {
            Assert.AreEqual(RibbonItemSize.Compact, group.Items[0].Size);
            Assert.IsFalse(group.Items[0].CaptionVisible);
            Assert.IsGreaterThanOrEqualTo(28d, group.Items[0].Width - 18d);
        }
    }

    [TestMethod]
    public void ProductionDefinitionShouldReservePrimaryChromeAndRetainExactSessionCatalog()
    {
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        var items = definition.Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items).ToArray();
        CommandId[] expectedLarge = ["Edit.Paste", "Insert.Chart.Column", "Insert.Pivot.Sum", "Formula.RecalculateWorkbook", "View.FreezePanes", "Table.Rename", "Table.Style"];
        CollectionAssert.AreEquivalent(expectedLarge, items.Where(static item => item.IsLarge)
            .Select(static item => item.CommandId).ToArray());
        CollectionAssert.AreEquivalent(RibbonProductionCommandCatalog.CommandIds.ToArray(),
            items.Select(static item => item.CommandId).ToArray());
        var session = new SpreadsheetSession(new Workbook());
        RibbonCommandCatalogAudit.ValidateExact(session.Commands, definition, RibbonProductionCommandCatalog.CommandIds);
    }

    private static void AssertGeometry(RibbonGroupLayout group)
    {
        Assert.IsLessThanOrEqualTo(group.Height, group.CaptionY + group.CaptionHeight);
        foreach (var item in group.Items)
        {
            Assert.IsGreaterThanOrEqualTo(0d, item.X);
            Assert.IsGreaterThanOrEqualTo(0d, item.Y);
            Assert.IsLessThanOrEqualTo(group.Width + 0.00001d, item.X + item.Width);
            Assert.IsLessThanOrEqualTo(group.CaptionY + 0.00001d, item.Y + item.Height);
        }
        for (var first = 0; first < group.Items.Count; first++)
        {
            for (var second = first + 1; second < group.Items.Count; second++)
            {
                var a = group.Items[first];
                var b = group.Items[second];
                Assert.IsFalse(a.X < b.X + b.Width && b.X < a.X + a.Width &&
                    a.Y < b.Y + b.Height && b.Y < a.Y + a.Height,
                    $"Overlapping commands {a.Presentation.Command.CommandId} and {b.Presentation.Command.CommandId}.");
            }
        }
    }

    private static RibbonPresentationSnapshot CreatePresentation(int groupCount)
    {
        var registry = new CommandRegistry();
        var groups = new List<RibbonGroupDefinition>();
        for (var group = 0; group < groupCount; group++)
        {
            var items = new List<RibbonItemDefinition>();
            for (var item = 0; item < 7; item++)
            {
                var id = $"g{group}.command{item}";
                registry.Register(new CommandDescriptor(id, item == 0 ? "Dán dữ liệu" : "Định dạng", iconKey: "edit.copy"),
                    new EnabledHandler());
                items.Add(new RibbonItemDefinition(id, item == 0));
            }
            items.Add(RibbonItemDefinition.Separator($"g{group}.separator"));
            registry.Register(new CommandDescriptor($"g{group}.gallery", "Kiểu bảng", iconKey: "table.style"),
                new EnabledHandler());
            items.Add(new RibbonItemDefinition($"g{group}.gallery", RibbonItemKind.Gallery));
            groups.Add(new RibbonGroupDefinition($"g{group}", $"Nhóm {group + 1}", items, order: group, collapsePriority: group == 0 ? 10 : 0));
        }
        return new RibbonPresentationProjector(registry).Project(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", groups),
        ]));
    }

    private sealed class EnabledHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
