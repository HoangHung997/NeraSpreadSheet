using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonItemModelTests
{
    [TestMethod]
    public void ProjectionShouldPreserveEveryItemKindAndImmutableCommandState()
    {
        var source = new List<CommandItem>
        {
            new("one", "Một"),
            new("more", "Thêm", children: [new CommandItem("two", "Hai")]),
        };
        var registry = new CommandRegistry();
        var kinds = Enum.GetValues<RibbonItemKind>()
            .Where(static kind => kind != RibbonItemKind.Separator)
            .ToArray();
        foreach (var kind in kinds)
        {
            registry.Register(
                new CommandDescriptor($"item.{kind}", kind.ToString()),
                new StatefulHandler(new CommandState(
                    true,
                    IsChecked: kind == RibbonItemKind.Toggle,
                    SelectedValue: "one",
                    ItemsSource: source)));
        }
        var items = kinds.Select((kind, order) => new RibbonItemDefinition(
                $"item.{kind}",
                kind,
                order: order,
                automationName: $"Automation {kind}"))
            .Append(RibbonItemDefinition.Separator("primary", kinds.Length))
            .ToArray();
        var definition = CreateDefinition(items);

        var snapshot = new RibbonPresentationProjector(registry).Project(definition);
        source.Clear();

        CollectionAssert.AreEqual(
            Enum.GetValues<RibbonItemKind>(),
            snapshot.Tabs[0].Groups[0].Items.Select(static item => item.Kind).ToArray());
        var combo = snapshot.Tabs[0].Groups[0].Items.Single(static item =>
            item.Kind == RibbonItemKind.ComboBox);
        Assert.AreEqual("one", combo.Command.SelectedValue);
        Assert.AreEqual(2, combo.Command.SelectableItems.Count);
        Assert.AreEqual("two", combo.Command.SelectableItems[1].Children[0].Value);
        Assert.AreEqual("Automation ComboBox", combo.AutomationName);
        Assert.IsFalse(snapshot.Tabs[0].Groups[0].Items[^1].Command.IsRegistered);
    }

    [TestMethod]
    public void CommandStateShouldRejectAmbiguousSelectableValues()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new CommandState(
            true,
            ItemsSource:
            [
                new CommandItem("same", "Một"),
                new CommandItem("same", "Hai"),
            ]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new CommandItem(
            "parent",
            "Cha",
            children:
            [
                new CommandItem("same", "Một"),
                new CommandItem("same", "Hai"),
            ]));
    }

    [TestMethod]
    public void LayoutShouldUseItemMeasurementCallbackAtEachResponsiveSize()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("item.combo", "Phông chữ"),
            new StatefulHandler(CommandState.Enabled));
        var measuredSizes = new List<RibbonItemSize>();
        var item = new RibbonItemDefinition(
            "item.combo",
            RibbonItemKind.ComboBox,
            isLarge: true,
            measurement: context =>
            {
                measuredSizes.Add(context.Size);
                return context.Size == RibbonItemSize.Large ? 150d : 90d;
            });
        var presentation = new RibbonPresentationProjector(registry)
            .Project(CreateDefinition([item]));

        var layout = new RibbonResponsiveLayoutEngine().Layout(
            presentation,
            new RibbonLayoutRequest(169d));

        Assert.AreEqual(RibbonItemSize.Small, layout.Tabs[0].Groups[0].Items[0].Size);
        Assert.AreEqual(90d, layout.Tabs[0].Groups[0].Items[0].Width);
        CollectionAssert.Contains(measuredSizes, RibbonItemSize.Large);
        CollectionAssert.Contains(measuredSizes, RibbonItemSize.Small);
    }

    [TestMethod]
    public void LayoutShouldRejectInvalidItemMeasurement()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("item.gallery", "Kiểu"),
            new StatefulHandler(CommandState.Enabled));
        var presentation = new RibbonPresentationProjector(registry).Project(
            CreateDefinition([
                new RibbonItemDefinition(
                    "item.gallery",
                    RibbonItemKind.Gallery,
                    measurement: static _ => double.NaN),
            ]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RibbonResponsiveLayoutEngine().Layout(
                presentation,
                new RibbonLayoutRequest(500d)));
    }

    [TestMethod]
    public void CustomizationShouldPreserveComplexItemSemantics()
    {
        RibbonItemMeasurementCallback measurement = static _ => 123d;
        var definition = CreateDefinition([
            new RibbonItemDefinition(
                "item.gallery",
                RibbonItemKind.Gallery,
                automationName: "Bộ sưu tập kiểu",
                measurement: measurement),
        ]);
        var customization = new RibbonCustomization([
            new RibbonTabCustomization("home", groups: [
                new RibbonGroupCustomization("items", items: [
                    new RibbonItemCustomization("item.gallery", IsLarge: true),
                ]),
            ]),
        ]);

        var item = customization.ApplyTo(definition).Tabs[0].Groups[0].Items[0];

        Assert.AreEqual(RibbonItemKind.Gallery, item.Kind);
        Assert.IsTrue(item.IsLarge);
        Assert.AreEqual("Bộ sưu tập kiểu", item.AutomationName);
        Assert.AreSame(measurement, item.Measurement);
    }

    [TestMethod]
    public async Task SelectableActivationShouldRetainHostParameterAndRefreshState()
    {
        var registry = new CommandRegistry();
        var handler = new SelectionHandler();
        registry.Register(new CommandDescriptor("item.color", "Màu"), handler);
        var runtime = new RibbonRuntimeController(
            CreateDefinition([
                new RibbonItemDefinition("item.color", RibbonItemKind.ColorPicker),
            ]),
            registry);

        var activated = await runtime.TryActivateItemAsync(
            "item.color",
            "#ff0000",
            new CommandContext(Parameter: "host-state"));

        Assert.IsTrue(activated);
        Assert.AreEqual("#ff0000", runtime.Snapshot.Tabs[0].Groups[0].Items[0]
            .Command.SelectedValue);
        Assert.IsNotNull(handler.LastActivation);
        Assert.AreEqual("#ff0000", handler.LastActivation.SelectedValue);
        Assert.AreEqual("host-state", handler.LastActivation.OriginalParameter);
    }

    [TestMethod]
    public async Task SelectableActivationShouldPropagateHandlerFailureWithoutRefresh()
    {
        var registry = new CommandRegistry();
        registry.Register(
            new CommandDescriptor("item.menu", "Trình đơn"),
            new ThrowingHandler());
        var runtime = new RibbonRuntimeController(
            CreateDefinition([
                new RibbonItemDefinition("item.menu", RibbonItemKind.Menu),
            ]),
            registry);
        var changeCount = 0;
        runtime.SnapshotChanged += (_, _) => changeCount++;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await runtime.TryActivateItemAsync("item.menu", "one"));

        Assert.AreEqual(0, changeCount);
    }

    private static RibbonDefinition CreateDefinition(
        IEnumerable<RibbonItemDefinition> items) =>
        new([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("items", "Mục", items),
            ]),
        ]);

    private sealed class StatefulHandler : IStatefulCommandHandler
    {
        public StatefulHandler(CommandState state) => State = state;

        public CommandState State { get; }

        public bool CanExecute(CommandContext context) => State.IsEnabled;

        public CommandState GetState(CommandContext context) => State;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class SelectionHandler : IStatefulCommandHandler
    {
        private string? _selectedValue;

        public RibbonItemActivation? LastActivation { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(
            true,
            SelectedValue: _selectedValue,
            ItemsSource:
            [
                new CommandItem("#ff0000", "Đỏ"),
                new CommandItem("#0000ff", "Xanh"),
            ]);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            LastActivation = (RibbonItemActivation?)context.Parameter;
            _selectedValue = LastActivation?.SelectedValue;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.FromException(new InvalidOperationException("Expected failure."));
    }
}
