using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonDeepCustomizationTests
{
    [TestMethod]
    public void SessionShouldCreateRenameMoveResizeAndRoundTripAcrossProfiles()
    {
        var definition = CreateDefinition();
        var session = new RibbonCustomizationSession(definition);
        var customTab = session.AddTab("custom-tools", "Công cụ của tôi");
        var customGroup = session.AddGroup(customTab.TabId, "custom-edit", "Chỉnh sửa nhanh");
        var moved = session.MoveCommand(
            RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy"),
            customTab.TabId,
            customGroup.GroupId!);

        Assert.IsTrue(session.Rename(customTab, "Tab cá nhân"));
        Assert.IsTrue(session.SetLarge(moved, true));
        Assert.IsTrue(session.AddToQuickAccessToolbar("edit.copy"));
        var preview = session.Preview();

        Assert.AreEqual("Tab cá nhân", preview.Tabs.Single(tab => tab.Id == "custom-tools").Caption);
        Assert.AreEqual(0, preview.Tabs.Single(tab => tab.Id == "home").Groups[0].Items.Count);
        Assert.IsTrue(preview.Tabs.Single(tab => tab.Id == "custom-tools").Groups[0].Items[0].IsLarge);
        Assert.AreEqual("edit.copy", preview.QuickAccessToolbar.Single().CommandId.Value);

        var json = RibbonCustomizationJsonSerializer.Serialize(session.Commit());
        var restored = new RibbonCustomizationSession(
            definition,
            RibbonCustomizationJsonSerializer.Deserialize(json));
        var restoredPreview = restored.Preview();

        Assert.AreEqual("custom-tools", restoredPreview.Tabs[1].Id);
        Assert.AreEqual("edit.copy", restoredPreview.Tabs[1].Groups[0].Items[0].CommandId.Value);
    }

    [TestMethod]
    public void CancelShouldRollbackUncommittedPreviewAndCommitShouldAdvanceRollbackPoint()
    {
        var session = new RibbonCustomizationSession(CreateDefinition());
        session.AddTab("discarded", "Bỏ đi");
        session.Cancel();
        Assert.IsFalse(session.Entries.Any(entry => entry.Target.TabId == "discarded"));

        session.AddTab("kept", "Giữ lại");
        session.Commit();
        session.Rename(RibbonCustomizationTarget.Tab("kept"), "Tạm thời");
        session.Cancel();

        Assert.AreEqual("Giữ lại", session.Entries.Single(entry => entry.Target.TabId == "kept").Caption);
    }

    [TestMethod]
    public void QuickAccessToolbarShouldRejectDuplicatesReorderAndPreserveUnknownModuleIds()
    {
        var profile = new RibbonCustomization(
            [new RibbonTabCustomization(
                "optional-tab",
                groups: [new RibbonGroupCustomization(
                    "optional-group",
                    items: [new RibbonItemCustomization("module.missing", IsPlacement: true)],
                    caption: "Tùy chọn",
                    isCustom: true)],
                caption: "Mô-đun",
                isCustom: true)],
            [
                new RibbonQuickAccessItemCustomization("module.optional", 0, "M"),
                new RibbonQuickAccessItemCustomization("edit.copy", 1, "C"),
            ]);
        var session = new RibbonCustomizationSession(CreateDefinition(), profile);

        Assert.IsFalse(session.AddToQuickAccessToolbar("edit.copy"));
        Assert.IsTrue(session.MoveQuickAccessToolbar("edit.copy", -1));
        var roundTrip = RibbonCustomizationJsonSerializer.Deserialize(
            RibbonCustomizationJsonSerializer.Serialize(session.CreateCustomization()));

        Assert.AreEqual("edit.copy,module.optional", string.Join(',', roundTrip.QuickAccessToolbar.OrderBy(item => item.Order).Select(item => item.CommandId.Value)));
        Assert.AreEqual("module.optional", roundTrip.QuickAccessToolbar.Single(item => item.CommandId == "module.optional").CommandId.Value);
        Assert.AreEqual(
            "module.missing",
            roundTrip.Tabs.Single(tab => tab.TabId == "optional-tab").Groups[0].Items[0].CommandId.Value);
    }

    [TestMethod]
    public void PolicyShouldRejectLockedMutationsWithoutChangingPreview()
    {
        var session = new RibbonCustomizationSession(
            CreateDefinition(),
            policy: new RibbonCustomizationPolicy(
                lockedCommandIds: ["edit.copy"],
                allowCustomTabs: false,
                allowQuickAccessToolbar: false));
        var before = RibbonCustomizationJsonSerializer.Serialize(session.CreateCustomization());

        Assert.ThrowsExactly<InvalidOperationException>(() => session.SetLarge(
            RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy"), true));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.AddTab("custom", "Tùy biến"));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.AddToQuickAccessToolbar("edit.copy"));
        var imported = new RibbonCustomization(
        [
            new RibbonTabCustomization("home", groups: [new RibbonGroupCustomization(
                "clipboard",
                items: [new RibbonItemCustomization("edit.copy", IsLarge: true)])]),
        ]);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.ReplaceCustomization(imported));

        Assert.AreEqual(before, RibbonCustomizationJsonSerializer.Serialize(session.CreateCustomization()));
        Assert.IsTrue(session.Entries.Single(entry => entry.Target.CommandId == "edit.copy").IsLocked);
    }

    [TestMethod]
    public void CatalogShouldGroupCommandsAndRejectDuplicateStableIds()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("edit.copy", "Sao chép", iconKey: "copy"), new EnabledHandler());
        registry.Register(new CommandDescriptor("app.unplaced", "Lệnh ứng dụng"), new EnabledHandler());
        var catalog = RibbonCommandCatalog.FromDefinition(CreateDefinition(), registry);

        Assert.AreEqual("home", catalog.Categories[0].Id);
        Assert.AreEqual("Sao chép", catalog.Entries[0].Caption);
        Assert.AreEqual("other", catalog.Categories[1].Id);
        Assert.AreEqual("app.unplaced", catalog.Categories[1].Commands[0].CommandId.Value);
        var session = new RibbonCustomizationSession(CreateDefinition(), catalog);
        var tab = session.AddTab("application", "Ứng dụng");
        var group = session.AddGroup(tab.TabId, "commands", "Lệnh");
        session.AddCommand("app.unplaced", tab.TabId, group.GroupId!);
        Assert.AreEqual("app.unplaced", session.Preview().Tabs[1].Groups[0].Items[0].CommandId.Value);
        var runtime = new RibbonRuntimeController(CreateDefinition(), registry);
        runtime.SetCustomization(session.CreateCustomization());
        Assert.AreEqual("app.unplaced", runtime.Snapshot.Tabs[1].Groups[0].Items[0].Command.CommandId.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => new RibbonCommandCatalog(
        [
            new RibbonCommandCatalogEntry("a", "A", "same", "Một", null),
            new RibbonCommandCatalogEntry("b", "B", "SAME", "Hai", null),
        ]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new RibbonCustomization(
        [
            new RibbonTabCustomization("one", groups: [new RibbonGroupCustomization("a", items: [new RibbonItemCustomization("same", IsPlacement: true)])]),
            new RibbonTabCustomization("two", groups: [new RibbonGroupCustomization("b", items: [new RibbonItemCustomization("SAME", IsPlacement: true)])]),
        ]));
    }

    [TestMethod]
    public void VersionOneProfileShouldMigrateWithoutInventingStructuralOverrides()
    {
        const string VersionOne =
            "{\"schema\":\"neraspreadsheet.ribbon-customization\",\"version\":1,\"tabs\":[{\"tabId\":\"home\",\"groups\":[{\"groupId\":\"clipboard\",\"items\":[{\"commandId\":\"edit.copy\",\"isLarge\":true}]}]}]}";

        var migrated = RibbonCustomizationJsonSerializer.MigrateToCurrent(VersionOne);
        var profile = RibbonCustomizationJsonSerializer.Deserialize(migrated);

        StringAssert.Contains(migrated, "\"version\":2");
        Assert.IsFalse(profile.Tabs[0].IsCustom);
        Assert.IsFalse(profile.Tabs[0].Groups[0].Items[0].IsPlacement);
        Assert.IsTrue(profile.ApplyTo(CreateDefinition()).Tabs[0].Groups[0].Items[0].IsLarge);
    }

    private static RibbonDefinition CreateDefinition() => new(
        [
            new RibbonTabDefinition(
                "home",
                "Trang đầu",
                [new RibbonGroupDefinition("clipboard", "Bảng tạm", [new RibbonItemDefinition("edit.copy")])]),
        ]);

    private sealed class EnabledHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
