using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Wpf;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

public sealed partial class Release009SplitAutoFilterSmokeTests
{
    [TestMethod]
    [DataRow(false, "vi-VN")]
    [DataRow(true, "vi-VN")]
    [DataRow(false, "en-US")]
    [DataRow(true, "en-US")]
    [Timeout(100_000)]
    public Task NativePeersShouldExposeCurrentPagePatternsNamesAndFocus(bool worksheetFilter, string culture) =>
        RunLoaded(worksheetFilter, true, async host =>
        {
            var threadCulture = CultureInfo.CurrentCulture;
            var threadUiCulture = CultureInfo.CurrentUICulture;
            host.Presenter.Localization = new PresentationLocalization(CultureInfo.GetCultureInfo(culture));
            foreach (var pane in Enum.GetValues<SpreadsheetPaneId>())
            {
                await OpenAccessiblePopup(host, pane);
                var binding = Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding");
                var undo = host.Session.History.UndoCount;
                Assert.AreEqual(250, binding.TotalItemCount);
                AssertAccessiblePage(host, culture, 0, 100);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250, "1–100/250");

                var search = NativePeer(Field<TextBox>(host.Presenter, "_searchBox"), AutomationControlType.Edit,
                    Text(culture, "Tìm giá trị trong cột Values", "Search values in column Values"));
                Assert.IsTrue(search.HasKeyboardFocus());
                Assert.IsFalse(NativePattern<IValueProvider>(search, PatternInterface.Value).IsReadOnly);
                var menu = NativePeer(Field<ComboBox>(host.Presenter, "_menuKindBox"), AutomationControlType.ComboBox,
                    Text(culture, "Nhóm điều kiện lọc", "Filter condition category"));
                Assert.AreEqual(ExpandCollapseState.Collapsed,
                    NativePattern<IExpandCollapseProvider>(menu, PatternInterface.ExpandCollapse).ExpandCollapseState);
                _ = NativePattern<ISelectionProvider>(menu, PatternInterface.Selection);
                AssertPagingPeers(host, culture, previous: false, next: true);

                var oldValue = Values(host).First();
                var oldPeer = NativePeer(oldValue, AutomationControlType.CheckBox);
                oldPeer.SetFocus();
                Assert.IsTrue(oldPeer.HasKeyboardFocus());
                await InvokeAccessibleButton(host, Field<Button>(host.Presenter, "_nextButton"));
                Assert.AreEqual(100, binding.PageOffset);
                AssertAccessiblePage(host, culture, 100, 100);
                AssertDetachedValuePeer(host, oldValue, oldPeer);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250, "101–200/250");
                AssertPagingPeers(host, culture, previous: true, next: true);

                await InvokeAccessibleButton(host, Field<Button>(host.Presenter, "_nextButton"));
                Assert.AreEqual(200, binding.PageOffset);
                AssertAccessiblePage(host, culture, 200, 50);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250, "201–250/250");
                AssertPagingPeers(host, culture, previous: true, next: false);

                await SearchAccessiblePopup(host, "Value 249", 1);
                Assert.AreEqual(0, binding.PageOffset);
                AssertAccessiblePage(host, culture, 249, 1);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250, "1–1/1");
                AssertPagingPeers(host, culture, previous: false, next: false);
                var choice = NativePeer(Values(host).Single(), AutomationControlType.CheckBox);
                var toggle = NativePattern<IToggleProvider>(choice, PatternInterface.Toggle);
                Assert.AreEqual(ToggleState.On, toggle.ToggleState);
                toggle.Toggle();
                await Drain(host);
                Assert.AreEqual(ToggleState.Off,
                    NativePattern<IToggleProvider>(NativePeer(Values(host).Single(), AutomationControlType.CheckBox), PatternInterface.Toggle).ToggleState);
                Assert.AreEqual(undo, host.Session.History.UndoCount, "Changing a pending choice must not apply a filter.");
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterPagedCancel"));
                Assert.IsFalse(host.Presenter.IsOpen);
                Assert.IsTrue(Surface(host).IsKeyboardFocused, "Cancel must restore the actual split surface's focus.");
                Assert.AreEqual(undo, host.Session.History.UndoCount);
                Assert.AreEqual(250, VisibleFixtureRows(host));
            }
            Assert.AreSame(threadCulture, CultureInfo.CurrentCulture);
            Assert.AreSame(threadUiCulture, CultureInfo.CurrentUICulture);
        });

    [TestMethod]
    [DataRow(false, "vi-VN")]
    [DataRow(true, "vi-VN")]
    [DataRow(false, "en-US")]
    [DataRow(true, "en-US")]
    [Timeout(100_000)]
    public Task NativeAnnouncementsShouldFollowAppliedFilterAndSortInEveryPane(bool worksheetFilter, string culture) =>
        RunLoaded(worksheetFilter, true, async host =>
        {
            host.Presenter.Localization = new PresentationLocalization(CultureInfo.GetCultureInfo(culture));
            foreach (var pane in Enum.GetValues<SpreadsheetPaneId>())
            {
                await OpenAccessiblePopup(host, pane);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250);
                var undo = host.Session.History.UndoCount;
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterSortDescending"));
                Assert.IsFalse(host.Presenter.IsOpen);
                Assert.AreEqual(undo + 1, host.Session.History.UndoCount);
                await OpenAccessiblePopup(host, pane);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.Sorted, 250);

                await SearchAccessiblePopup(host, "Value 249", 1);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.Sorted, 250, "1–1/1");
                NativePattern<IToggleProvider>(NativePeer(Values(host).Single(), AutomationControlType.CheckBox), PatternInterface.Toggle).Toggle();
                await Drain(host);
                var apply = NativePeer(Field<Button>(host.Presenter, "_applyButton"), AutomationControlType.Button,
                    Text(culture, "Áp dụng", "Apply"));
                Assert.IsTrue(apply.IsEnabled());
                await InvokeAccessibleButton(host, Field<Button>(host.Presenter, "_applyButton"));
                Assert.IsFalse(host.Presenter.IsOpen);
                Assert.AreEqual(undo + 2, host.Session.History.UndoCount);
                Assert.AreEqual(249, VisibleFixtureRows(host));
                await OpenAccessiblePopup(host, pane);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.FilteredAndSorted, 249);

                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterClearSort"));
                Assert.AreEqual(undo + 3, host.Session.History.UndoCount);
                await OpenAccessiblePopup(host, pane);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.Filtered, 249);
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterPagedClear"));
                Assert.AreEqual(undo + 4, host.Session.History.UndoCount);
                Assert.AreEqual(250, VisibleFixtureRows(host));
                await OpenAccessiblePopup(host, pane);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250);
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterPagedCancel"));
            }
        });

    [TestMethod]
    [DataRow(false, "vi-VN")]
    [DataRow(true, "vi-VN")]
    [DataRow(false, "en-US")]
    [DataRow(true, "en-US")]
    [Timeout(100_000)]
    public Task DetachedPopupPeersShouldNotChangeTheReopenedOrSwitchedWorksheet(bool worksheetFilter, string culture) =>
        RunLoaded(worksheetFilter, true, async host =>
        {
            host.Presenter.Localization = new PresentationLocalization(CultureInfo.GetCultureInfo(culture));
            var original = host.Session.ActiveWorksheet;
            var other = host.Session.Workbook.AddWorksheet("AccessibilityOther");
            foreach (var pane in Enum.GetValues<SpreadsheetPaneId>())
            {
                await OpenAccessiblePopup(host, pane);
                var oldValue = Values(host).First();
                var oldPeer = NativePeer(oldValue, AutomationControlType.CheckBox);
                var oldSearch = NativePeer(Field<TextBox>(host.Presenter, "_searchBox"), AutomationControlType.Edit);
                var oldApply = NativePeer(Field<Button>(host.Presenter, "_applyButton"), AutomationControlType.Button);
                var oldRoot = Field<Popup>(host.Presenter, "_popup").Child;
                var undo = host.Session.History.UndoCount;
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterPagedCancel"));
                Assert.IsFalse(oldPeer.HasKeyboardFocus());
                Assert.IsFalse(oldSearch.HasKeyboardFocus());
                Assert.IsTrue(oldPeer.IsOffscreen());
                await OpenAccessiblePopup(host, pane);
                Assert.AreNotSame(oldRoot, Field<Popup>(host.Presenter, "_popup").Child);
                AssertDetachedValuePeer(host, oldValue, oldPeer);
                NativePattern<IValueProvider>(oldSearch, PatternInterface.Value).SetValue("obsolete search");
                NativePattern<IToggleProvider>(oldPeer, PatternInterface.Toggle).Toggle();
                NativePattern<IInvokeProvider>(oldApply, PatternInterface.Invoke).Invoke();
                await Drain(host);
                Assert.IsTrue(host.Presenter.IsOpen);
                Assert.AreEqual(string.Empty, Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding").SearchText);
                AssertAccessiblePage(host, culture, 0, 100);
                var currentSearch = NativePeer(Field<TextBox>(host.Presenter, "_searchBox"), AutomationControlType.Edit);
                Assert.IsTrue(currentSearch.HasKeyboardFocus());
                Assert.IsFalse(oldSearch.HasKeyboardFocus());

                var currentValue = Values(host).First();
                var currentPeer = NativePeer(currentValue, AutomationControlType.CheckBox);
                host.Session.ActivateWorksheet(other);
                Assert.IsFalse(host.Presenter.IsOpen);
                await Flush(host);
                Assert.IsTrue(Surface(host).Focus());
                Assert.IsFalse(currentPeer.HasKeyboardFocus());
                Assert.IsFalse(currentSearch.HasKeyboardFocus());
                Assert.IsTrue(currentPeer.IsOffscreen());
                Assert.AreEqual(undo, host.Session.History.UndoCount);
                Assert.AreSame(other, host.Session.ActiveWorksheet);
                Assert.HasCount(0, Buttons(host));

                host.Session.ActivateWorksheet(original);
                host.Session.Selection.SetActiveCell(Header);
                await Flush(host);
                await OpenAccessiblePopup(host, pane);
                AssertDetachedValuePeer(host, currentValue, currentPeer);
                AssertAccessibleStatus(host, culture, SpreadsheetFilterHeaderState.None, 250);
                Assert.IsTrue(NativePeer(Field<TextBox>(host.Presenter, "_searchBox"), AutomationControlType.Edit).HasKeyboardFocus());
                await InvokeAccessibleButton(host, AccessibleButton(host, "NeraAutoFilterPagedCancel"));
                Assert.AreEqual(undo, host.Session.History.UndoCount);
            }
        });

    private static async Task OpenAccessiblePopup(Host host, SpreadsheetPaneId pane)
    {
        Assert.IsTrue(Surface(host).Focus());
        await ClickHeader(host, ExpectedHit(host, pane).Bounds);
        await Ready(host);
        AssertAnchor(host, pane);
        Assert.AreEqual(100, Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding").PageSize);
    }

    private static AutomationPeer NativePeer(UIElement element, AutomationControlType role, string? name = null)
    {
        Assert.IsTrue(element.Dispatcher.CheckAccess(), "Read native peers only on their owning WPF UI thread.");
        var peer = UIElementAutomationPeer.CreatePeerForElement(element) ??
            throw new AssertFailedException($"The actual {element.GetType().Name} did not expose an automation peer.");
        Assert.AreEqual(role, peer.GetAutomationControlType());
        if (name is not null) Assert.AreEqual(name, peer.GetName());
        return peer;
    }

    private static T NativePattern<T>(AutomationPeer peer, PatternInterface pattern) where T : class =>
        peer.GetPattern(pattern) as T ?? throw new AssertFailedException($"Native peer does not expose {pattern}.");

    private static Button AccessibleButton(Host host, string id) =>
        Descendants(Field<Popup>(host.Presenter, "_popup").Child).OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == id);

    private static async Task InvokeAccessibleButton(Host host, Button button)
    {
        var peer = NativePeer(button, AutomationControlType.Button);
        Assert.IsTrue(peer.IsEnabled());
        NativePattern<IInvokeProvider>(peer, PatternInterface.Invoke).Invoke();
        // WPF InvokePattern queues the actual button action at input priority.
        await Flush(host);
        await Drain(host);
    }

    private static async Task SearchAccessiblePopup(Host host, string text, int count)
    {
        var binding = Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding");
        var peer = NativePeer(Field<TextBox>(host.Presenter, "_searchBox"), AutomationControlType.Edit);
        peer.SetFocus();
        NativePattern<IValueProvider>(peer, PatternInterface.Value).SetValue(text);
        await Until(() => binding.SearchText == text && Values(host).Count == count && !binding.IsBusy);
        await Drain(host);
    }

    private static void AssertPagingPeers(Host host, string culture, bool previous, bool next)
    {
        var back = NativePeer(Field<Button>(host.Presenter, "_previousButton"), AutomationControlType.Button,
            Text(culture, "◀ Trang trước", "◀ Previous page"));
        var forward = NativePeer(Field<Button>(host.Presenter, "_nextButton"), AutomationControlType.Button,
            Text(culture, "Trang sau ▶", "Next page ▶"));
        Assert.AreEqual(previous, back.IsEnabled());
        Assert.AreEqual(next, forward.IsEnabled());
        foreach (var peer in new[] { back, forward })
        {
            var invoke = NativePattern<IInvokeProvider>(peer, PatternInterface.Invoke);
            if (!peer.IsEnabled()) Assert.Throws<ElementNotEnabledException>(() => invoke.Invoke());
        }
    }

    private static void AssertAccessiblePage(Host host, string culture, int first, int count)
    {
        Assert.HasCount(count, Values(host));
        var peers = CurrentValuePeers(host);
        Assert.HasCount(count, peers);
        for (var index = 0; index < count; index++)
        {
            var expected = $"Value {first + index:000}; 1 " + Text(culture, "dòng", "rows");
            var peer = NativePeer(Values(host)[index], AutomationControlType.CheckBox, expected);
            Assert.IsTrue(peers.Contains(peer), "The native values subtree must expose the actual current checkbox peer.");
            Assert.IsTrue(peer.IsEnabled());
            Assert.IsTrue(peer.IsKeyboardFocusable());
            Assert.AreEqual(ToggleState.On, NativePattern<IToggleProvider>(peer, PatternInterface.Toggle).ToggleState);
        }
    }

    private static List<AutomationPeer> CurrentValuePeers(Host host)
    {
        var scroller = Field<ScrollViewer>(host.Presenter, "_itemsScroller");
        var root = NativePeer(scroller, AutomationControlType.Pane);
        var queue = new Queue<AutomationPeer>();
        var visited = new HashSet<AutomationPeer>();
        var values = new List<AutomationPeer>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var peer))
        {
            Assert.IsTrue(visited.Add(peer), "The current native automation subtree must not contain a cycle.");
            Assert.IsLessThan(1000, visited.Count, "Native automation traversal must remain bounded to the loaded page.");
            if (peer.GetAutomationControlType() == AutomationControlType.CheckBox) values.Add(peer);
            foreach (var child in peer.GetChildren() ?? []) queue.Enqueue(child);
        }
        return values;
    }

    private static void AssertDetachedValuePeer(Host host, CheckBox element, AutomationPeer peer)
    {
        Assert.IsFalse(Values(host).Contains(element));
        Assert.IsFalse(CurrentValuePeers(host).Contains(peer), "Detached page controls must leave the native automation subtree.");
        Assert.IsTrue(peer.IsOffscreen());
        Assert.IsFalse(peer.HasKeyboardFocus());
    }

    private static void AssertAccessibleStatus(Host host, string culture, SpreadsheetFilterHeaderState state, int rows, string? page = null)
    {
        Assert.IsTrue(Buttons(host).All(button => button.HeaderState == state));
        var binding = Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding");
        Assert.AreEqual(state, binding.Target.HeaderState);
        var description = state switch
        {
            SpreadsheetFilterHeaderState.Filtered => Text(culture, "đang lọc", "filtered"),
            SpreadsheetFilterHeaderState.Sorted => Text(culture, "đang sắp xếp giảm dần", "sorted descending"),
            SpreadsheetFilterHeaderState.FilteredAndSorted => Text(culture, "đang lọc và sắp xếp giảm dần", "filtered and sorted descending"),
            _ => Text(culture, "chưa lọc hoặc sắp xếp", "not filtered or sorted"),
        };
        var owner = binding.Target.OwnerKind == SpreadsheetAutoFilterOwnerKind.Table ? "FilterValues" : host.Session.ActiveWorksheet.Name;
        var announcement = Text(culture, $"Cột Values trong {owner}, {description}, {rows} kết quả.",
            $"Column Values in {owner}, {description}, {rows} results.");
        var status = NativePeer(Field<TextBlock>(host.Presenter, "_status"), AutomationControlType.Text);
        Assert.IsTrue(status.GetName().StartsWith(announcement, StringComparison.Ordinal), status.GetName());
        if (page is not null) Assert.AreEqual(announcement + " " + page + Text(culture, " giá trị.", " values."), status.GetName());
        var root = Field<Popup>(host.Presenter, "_popup").Child;
        Assert.AreEqual(Text(culture, $"Lọc Values trong {owner}, {description}", $"Filter Values in {owner}, {description}"),
            AutomationProperties.GetName(root));
        Assert.AreEqual(rows, VisibleFixtureRows(host));
    }

    private static int VisibleFixtureRows(Host host)
    {
        var snapshot = WorksheetSnapshot.Capture(host.Session.ActiveWorksheet);
        return Enumerable.Range(3, 250).Count(snapshot.IsRowVisible);
    }

    private static string Text(string culture, string vietnamese, string english) =>
        culture == "en-US" ? english : vietnamese;
}
