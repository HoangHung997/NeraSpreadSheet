using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class RibbonCustomizationTests
{
    [TestMethod]
    public Task NoOpApplyShouldPreserveInheritedProfile() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        Assert.IsNull(editor.Apply()); Assert.IsNull(runtime.Customization);
        Assert.IsFalse(RibbonCustomizationJsonSerializer.Deserialize(editor.ExportJson()).HasQuickAccessToolbarOverride);
    });
    [TestMethod]
    public Task StructureChangeShouldNotFreezeInheritedQat() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        editor.Rename(RibbonCustomizationTarget.Tab("home"), "Tên mới"); var profile = editor.Apply();
        Assert.IsNotNull(profile); Assert.IsFalse(profile.HasQuickAccessToolbarOverride);
        Assert.AreEqual("Tên mới", runtime.EffectiveDefinition.Tabs[0].Caption);
    });
    [TestMethod]
    public Task ExplicitEmptyQatShouldRemainEmptyAcrossJsonRoundTrip() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        Assert.IsTrue(editor.RemoveFromQuickAccessToolbar("Test.One"));
        var json = editor.ExportJson(); editor.Apply();
        Assert.HasCount(0, runtime.EffectiveDefinition.QuickAccessToolbar);
        var other = new NeraRibbonCustomizationControl(Runtime()); other.ImportJson(json);
        Assert.IsTrue(other.Apply()!.HasQuickAccessToolbarOverride); Assert.HasCount(0, other.QuickAccessToolbar);
    });
    [TestMethod]
    public Task CancelShouldDiscardStructureAndQatChanges() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        editor.AddTab("user-tab", "Tùy chỉnh"); editor.AddToQuickAccessToolbar("Test.Two"); editor.Cancel();
        Assert.IsFalse(editor.HasChanges); Assert.IsNull(runtime.Customization); Assert.HasCount(1, editor.QuickAccessToolbar);
        Assert.IsFalse(editor.Entries.Any(entry => entry.Target.TabId == "user-tab"));
    });
    [TestMethod]
    public Task CustomTabGroupAndCommandShouldApplyThroughSharedProfile() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        editor.AddTab("custom", "Của tôi"); editor.AddGroup("custom", "tools", "Công cụ");
        editor.AddCommand("Test.Two", "custom", "tools", true); editor.Apply();
        var command = runtime.EffectiveDefinition.Tabs.Single(tab => tab.Id == "custom").Groups[0].Items[0];
        Assert.AreEqual(new CommandId("Test.Two"), command.CommandId); Assert.IsTrue(command.IsLarge);
    });
    [TestMethod]
    public Task InvalidImportShouldLeaveWorkingProfileUnchanged() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var editor = new NeraRibbonCustomizationControl(Runtime()); editor.AddToQuickAccessToolbar("Test.Two"); var before = editor.ExportJson();
        try { editor.ImportJson("{\"version\":999}"); Assert.Fail("Invalid schema must fail."); }
        catch (System.IO.InvalidDataException) { }
        Assert.AreEqual(before, editor.ExportJson());
    });
    [TestMethod]
    public Task ApplicationPolicyShouldRejectLockedMutations() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var editor = new NeraRibbonCustomizationControl(Runtime(), new RibbonCustomizationPolicy(lockedTabIds: ["home"]));
        try { editor.SetVisible(RibbonCustomizationTarget.Tab("home"), false); Assert.Fail("Locked tab must not change."); }
        catch (InvalidOperationException) { }
        Assert.IsFalse(editor.HasChanges);
    });
    [TestMethod]
    public Task ExternalProfileChangeShouldRejectStaleApply() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var runtime = Runtime(); var editor = new NeraRibbonCustomizationControl(runtime);
        editor.AddToQuickAccessToolbar("Test.Two"); var replacement = new RibbonCustomization([]); runtime.SetCustomization(replacement);
        try { editor.Apply(); Assert.Fail("Stale editor must not overwrite another editor."); }
        catch (InvalidOperationException) { }
        Assert.AreSame(replacement, runtime.Customization);
    });
    private static RibbonRuntimeController Runtime()
    {
        var registry = new CommandRegistry(); registry.Register(new CommandDescriptor("Test.One", "Một"), new Handler()); registry.Register(new CommandDescriptor("Test.Two", "Hai"), new Handler());
        return new RibbonRuntimeController(new RibbonDefinition(
            [new RibbonTabDefinition("home", "Trang đầu", [new RibbonGroupDefinition("tools", "Công cụ", [new RibbonItemDefinition("Test.One"), new RibbonItemDefinition("Test.Two")])])],
            [], [new RibbonCommandSurfaceItem("Test.One", "1")], []), registry);
    }
    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
