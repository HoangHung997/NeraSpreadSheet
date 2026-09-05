using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class PresentationLocalizationTests
{
    [TestMethod]
    public void EnglishResourcesShouldCoverEveryNeutralKeyAndPreserveFormatArguments()
    {
        var resources = new System.Resources.ResourceManager(
            "NeraSpreadSheet.Commands.PresentationStrings", typeof(PresentationLocalization).Assembly);
        var neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var english = resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false)!;
        foreach (System.Collections.DictionaryEntry entry in neutral)
        {
            var key = (string)entry.Key;
            var translated = english.GetString(key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(translated), key);
            var sourceFormat = System.Text.CompositeFormat.Parse((string)entry.Value!);
            var targetFormat = System.Text.CompositeFormat.Parse(translated);
            Assert.AreEqual(sourceFormat.MinimumArgumentCount, targetFormat.MinimumArgumentCount, key);
            var sourceArguments = System.Text.RegularExpressions.Regex.Matches((string)entry.Value!, @"\{[0-9]+[^}]*\}")
                .Select(static match => match.Value).Order(StringComparer.Ordinal).ToArray();
            var targetArguments = System.Text.RegularExpressions.Regex.Matches(translated, @"\{[0-9]+[^}]*\}")
                .Select(static match => match.Value).Order(StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(sourceArguments, targetArguments, key);
        }
    }

    [TestMethod]
    public void ThrowingHostResourceShouldLeaveThePreviousRuntimeSnapshotAndCultureIntact()
    {
        var session = new SpreadsheetSession(new Workbook());
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
        var snapshot = runtime.Snapshot;
        var history = session.History.UndoCount;
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.SetLocalization(
            new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"),
                static (_, _) => throw new InvalidOperationException("Host resource failed."))));
        Assert.AreSame(snapshot, runtime.Snapshot);
        Assert.AreSame(PresentationLocalization.Default, runtime.Localization);
        Assert.AreEqual(history, session.History.UndoCount);
    }

    [TestMethod]
    public void ResourcesShouldFallBackAndHonorHostCultureWithoutChangingThreadCulture()
    {
        var original = CultureInfo.CurrentUICulture;
        var french = new PresentationLocalization(CultureInfo.GetCultureInfo("fr-CA"));
        var english = new PresentationLocalization(CultureInfo.GetCultureInfo("en-GB"));
        var host = new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"),
            static (key, culture) => key == "Áp dụng" ? $"Apply ({culture.Name})" : null);

        Assert.AreEqual("Áp dụng", french.Get("Áp dụng"));
        Assert.AreEqual("Apply", english.Get("Áp dụng"));
        Assert.AreEqual("Apply (en-US)", host.Get("Áp dụng"));
        Assert.AreEqual("Cancel", host.Get("Hủy"));
        Assert.AreEqual("host.missing", host.Get("host.missing"));
        Assert.AreEqual("Năm 2026", french.Format("Năm {0}", 2026));
        Assert.IsTrue(host.Culture.IsReadOnly);
        Assert.AreSame(original, CultureInfo.CurrentUICulture);
    }

    [TestMethod]
    public void ProductionCatalogShouldHaveVietnameseResourcesForEveryDefaultCaption()
    {
        var session = new SpreadsheetSession(new Workbook());
        foreach (var id in RibbonProductionCommandCatalog.CommandIds)
        {
            Assert.IsTrue(session.Commands.TryResolve(id, out var descriptor, out _));
            Assert.IsNotNull(descriptor);
            Assert.IsTrue(PresentationLocalization.ContainsKey(descriptor.Caption), id.Value);
        }
        foreach (var tab in RibbonProductionCommandCatalog.CreateDefaultDefinition().Tabs)
        {
            Assert.IsNotNull(tab.CaptionResourceKey);
            Assert.IsTrue(PresentationLocalization.ContainsKey(tab.CaptionResourceKey));
            foreach (var group in tab.Groups)
            {
                Assert.IsNotNull(group.CaptionResourceKey);
                Assert.IsTrue(PresentationLocalization.ContainsKey(group.CaptionResourceKey));
            }
        }
        foreach (var kind in Enum.GetValues<SpreadsheetAutoFilterMenuKind>())
        {
            Assert.IsTrue(PresentationLocalization.ContainsKey(kind.GetDefaultDisplayName()));
        }
    }

    [TestMethod]
    public async Task CultureSwitchShouldPreserveCustomizationIdentityShortcutsAndTableHistory()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Tables.Create(new CellRange(default, new CellAddress(2, 1)), "DữLiệu");
        var profile = new RibbonCustomization([
            new RibbonTabCustomization("home", caption: "Công việc của tôi"),
        ]);
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands, profile);
        runtime.SetSelectionContext(new RibbonSelectionContext(HasSelection: true, IsInTable: true));
        var json = RibbonCustomizationJsonSerializer.Serialize(profile);
        var ids = runtime.Snapshot.Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items).Select(static item => item.Command.CommandId).ToArray();
        var undoDescription = session.History.NextUndoDescription;
        var tips = runtime.KeyTips.TabTips.ToArray();
        runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-US")));

        Assert.AreEqual("Công việc của tôi", runtime.Snapshot.Tabs.Single(static tab => tab.Id == "home").Caption,
            "A customized caption is user text.");
        Assert.AreEqual("Table Design", runtime.Snapshot.Tabs.Single(static tab => tab.Id == "table-design").Caption);
        CollectionAssert.AreEqual(ids, runtime.Snapshot.Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items).Select(static item => item.Command.CommandId).ToArray());
        CollectionAssert.AreEqual(tips, runtime.KeyTips.TabTips.ToArray());
        Assert.IsTrue(runtime.TryResolveShortcut("Ctrl+B", out var bold));
        Assert.AreEqual(new CommandId("Cell.Format.Bold"), bold);
        Assert.AreEqual(json, RibbonCustomizationJsonSerializer.Serialize(runtime.Customization!));
        Assert.AreEqual(undoDescription, session.History.NextUndoDescription);

        Assert.IsTrue(await runtime.TryActivateAsync("Table.TotalsRow"));
        Assert.IsTrue(session.TableDesign.Snapshot.HasTotalsRow);
        runtime.SetLocalization(PresentationLocalization.Default);
        Assert.IsTrue(await runtime.TryActivateAsync("Edit.Undo"));
        Assert.IsFalse(session.TableDesign.Snapshot.HasTotalsRow);
    }

    [TestMethod]
    public void BarAndRibbonShouldKeepHostDescriptorAndDynamicUserTextOverrides()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("Edit.Copy", "Host copy"), new Handler());
        var bar = new BarRuntimeController(new BarDefinition("bar", BarKind.Toolbar,
            [BarItemDefinition.Command("Edit.Copy")]), registry);
        bar.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"),
            static (_, _) => "Do not replace host captions"));
        Assert.AreEqual("Host copy", bar.Snapshot.Items[0].Caption);
    }

    [TestMethod]
    public void LongLocalizedLabelsShouldRetainDenseBoundsAndCommandsAcrossScaleMatrix()
    {
        var session = new SpreadsheetSession(new Workbook());
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
        runtime.SetSelectionContext(new RibbonSelectionContext(HasSelection: true, IsInTable: true));
        runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("vi-VN"),
            static (key, _) => key == "Đổi tên Bảng" ? "Đổi tên bảng dữ liệu để nhận biết nội dung trong sổ tính" : null));
        var engine = new RibbonResponsiveLayoutEngine();
        double[] widths = [820d, 1024d, 1280d, 1920d];
        double[] scales = [1d, 1.25d, 1.5d, 2d];
        foreach (var width in widths)
        {
            foreach (var scale in scales)
            {
                var layout = engine.Layout(runtime.Snapshot,
                    new RibbonLayoutRequest(width * scale, scale, "table-design", "Table.Rename"));
                var tab = layout.Tabs.Single(static tab => tab.Presentation.Id == "table-design");
                Assert.IsLessThanOrEqualTo(width * scale, tab.InlineWidth);
                Assert.AreEqual(19 - 1, tab.Groups.Sum(static group => group.Items.Count));
                Assert.AreEqual(new CommandId("Table.Rename"), layout.FocusedCommandId);
                foreach (var group in tab.Groups.Where(static group => group.Mode != RibbonGroupLayoutMode.Overflow))
                {
                    foreach (var item in group.Items)
                    {
                        Assert.IsLessThanOrEqualTo(group.Width + 0.001d, item.X + item.Width);
                        Assert.IsLessThanOrEqualTo(group.CaptionY + 0.001d, item.Y + item.Height);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void LocalizedCatalogAndEditorShouldKeepStoredSourceCaptionsAndCustomNames()
    {
        var session = new SpreadsheetSession(new Workbook());
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        var runtime = new RibbonRuntimeController(definition, session.Commands);
        var editor = new RibbonCustomizationSession(definition, runtime.CommandCatalog);
        var profile = editor.CreateCustomization();
        var json = RibbonCustomizationJsonSerializer.Serialize(profile);
        runtime.SetCustomization(profile);
        var english = new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"));
        runtime.SetLocalization(english);
        Assert.AreEqual("Home", runtime.Snapshot.Tabs.Single(static tab => tab.Id == "home").Caption);
        Assert.AreEqual("Home", editor.GetLocalizedEntries(english).Single(static entry =>
            entry.Target.Kind == RibbonCustomizationTargetKind.Tab && entry.Target.TabId == "home").Caption);
        Assert.AreEqual("Copy", runtime.CommandCatalog.Entries.Single(static entry => entry.CommandId == new CommandId("Edit.Copy")).Caption);
        Assert.IsTrue(editor.Rename(RibbonCustomizationTarget.Tab("home"), "Công việc"));
        Assert.AreEqual("Công việc", editor.GetLocalizedEntries(english).Single(static entry =>
            entry.Target.Kind == RibbonCustomizationTargetKind.Tab && entry.Target.TabId == "home").Caption);
        Assert.AreEqual(json, RibbonCustomizationJsonSerializer.Serialize(runtime.Customization!));
    }

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
