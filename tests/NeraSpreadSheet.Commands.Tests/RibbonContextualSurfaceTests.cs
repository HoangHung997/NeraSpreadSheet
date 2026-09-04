using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonContextualSurfaceTests
{
    [TestMethod]
    public void ContextualTableTabShouldTrackSelectionContextWithoutChangingDefinition()
    {
        var registry = CreateRegistry("home.copy", "table.rename");
        var definition = CreateDefinition();
        var runtime = new RibbonRuntimeController(definition, registry);

        Assert.AreEqual(1, runtime.Snapshot.Tabs.Count);
        runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        Assert.AreEqual(2, runtime.Snapshot.Tabs.Count);
        Assert.AreEqual("table-design", runtime.Snapshot.Tabs[1].Id);
        runtime.SetSelectionContext(new RibbonSelectionContext(true, false));
        Assert.AreEqual(1, runtime.Snapshot.Tabs.Count);
        Assert.AreEqual(2, definition.Tabs.Count);
    }

    [TestMethod]
    public async Task QuickAccessAndBackstageShouldUseStableRuntimeCommandIdentity()
    {
        var registry = CreateRegistry("home.copy", "table.rename", "file.save");
        var runtime = new RibbonRuntimeController(CreateDefinition(), registry);

        Assert.AreEqual("home.copy", runtime.Snapshot.QuickAccessToolbar[0].CommandId.Value);
        Assert.AreEqual("file.save", runtime.Snapshot.Backstage[0].CommandId.Value);
        Assert.IsTrue(await runtime.TryActivateAsync("home.copy"));
        Assert.IsTrue(await runtime.TryActivateAsync("file.save"));
    }

    [TestMethod]
    public void MinimizedStateShouldRoundTripAndPublishOnlyOnChange()
    {
        var registry = CreateRegistry("home.copy", "table.rename");
        var runtime = new RibbonRuntimeController(CreateDefinition(), registry);
        var changes = 0;
        runtime.SnapshotChanged += (_, _) => changes++;

        runtime.SetMinimized(true);
        runtime.SetMinimized(true);
        var json = RibbonViewStateJsonSerializer.Serialize(new RibbonViewState(runtime.IsMinimized));
        runtime.SetMinimized(false);
        runtime.RestoreViewState(RibbonViewStateJsonSerializer.Deserialize(json));

        Assert.IsTrue(runtime.IsMinimized);
        Assert.AreEqual(3, changes);
    }

    [TestMethod]
    public void KeyTipsShouldScopeNavigateBackAndRejectSurfaceCollision()
    {
        var runtime = new RibbonRuntimeController(
            CreateDefinition(),
            CreateRegistry("home.copy", "table.rename", "file.save"));
        runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        var tips = runtime.KeyTips;

        tips.Enter();
        var tabResult = tips.Process(tips.TabTips["home"]);
        Assert.AreEqual(RibbonKeyTipScope.Tab, tips.Scope);
        Assert.AreEqual("home", tabResult.TabId);
        var commandTip = tips.GetCommandTips().Single().Key;
        var commandResult = tips.Process(commandTip);
        Assert.AreEqual(RibbonKeyTipAction.ActivateCommand, commandResult.Action);
        Assert.AreEqual(new CommandId("home.copy"), commandResult.CommandId);
        tips.Enter();
        tips.Process(tips.TabTips["home"]);
        Assert.AreEqual(RibbonKeyTipAction.ScopeChanged, tips.Escape().Action);
        Assert.AreEqual(RibbonKeyTipScope.Tabs, tips.Scope);
        Assert.AreEqual(RibbonKeyTipAction.Exit, tips.Escape().Action);

        tips.Enter();
        RibbonKeyTipResult? contextualResult = null;
        foreach (var character in tips.TabTips["table-design"])
        {
            contextualResult = tips.ProcessCharacter(character);
        }
        Assert.AreEqual("table-design", contextualResult?.TabId);

        tips.Enter();
        Assert.AreEqual(RibbonKeyTipScope.Backstage, tips.Process("F").Action ==
            RibbonKeyTipAction.ScopeChanged ? tips.Scope : RibbonKeyTipScope.Inactive);
        Assert.AreEqual(new CommandId("file.save"), tips.Process("S").CommandId);
        tips.Enter();
        tips.Process("Q");
        Assert.AreEqual(new CommandId("home.copy"), tips.Process("1").CommandId);

        Assert.ThrowsExactly<InvalidOperationException>(() => new RibbonDefinition(
            CreateDefinition().Tabs,
            [],
            [new("home.copy", "A"), new("file.save", "a")],
            []));
    }

    [TestMethod]
    public void CatalogAuditShouldRequireRegistrationAndReachablePlacement()
    {
        var registry = CreateRegistry("home.copy", "table.rename", "file.save");
        var definition = CreateDefinition();
        RibbonCommandCatalogAudit.Validate(
            registry,
            definition,
            ["home.copy", "table.rename", "file.save"]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            RibbonCommandCatalogAudit.Validate(registry, definition, ["missing.command"]));
    }

    [TestMethod]
    public void CustomizationShouldPreserveContextualQatAndBackstageMetadata()
    {
        var definition = CreateDefinition();
        var customized = new RibbonCustomization([
            new RibbonTabCustomization("home", order: 5),
        ]).ApplyTo(definition);

        Assert.AreEqual(1, customized.ContextualTabs.Count);
        Assert.AreEqual("home.copy", customized.QuickAccessToolbar[0].CommandId.Value);
        Assert.AreEqual("file.save", customized.Backstage[0].CommandId.Value);
    }

    [TestMethod]
    public void ProductionCatalogShouldContainEveryRegisteredSessionCapabilityOnce()
    {
        Assert.AreEqual(30, RibbonProductionCommandCatalog.CommandIds.Count);
        Assert.AreEqual(30, RibbonProductionCommandCatalog.CommandIds.Distinct().Count());
        var registry = new CommandRegistry();
        foreach (var commandId in RibbonProductionCommandCatalog.CommandIds)
        {
            registry.Register(new CommandDescriptor(commandId, commandId.Value), new Handler());
        }

        RibbonCommandCatalogAudit.Validate(
            registry,
            RibbonProductionCommandCatalog.CreateDefaultDefinition(),
            RibbonProductionCommandCatalog.CommandIds);
    }

    private static RibbonDefinition CreateDefinition() => new(
        [
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("clipboard", "Bảng tạm", [new("home.copy")])]),
            new RibbonTabDefinition("table-design", "Thiết kế Bảng", [
                new RibbonGroupDefinition("table", "Bảng", [new("table.rename")])]),
        ],
        [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
        [new RibbonCommandSurfaceItem("home.copy", "1")],
        [new RibbonCommandSurfaceItem("file.save", "S")]);

    private static CommandRegistry CreateRegistry(params string[] commandIds)
    {
        var registry = new CommandRegistry();
        foreach (var commandId in commandIds)
        {
            registry.Register(new CommandDescriptor(commandId, commandId), new Handler());
        }
        return registry;
    }

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
