using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed partial class Release009SplitAutoFilterSmokeTests
{
    private static readonly CellAddress Header = new(2, 1);

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [Timeout(100_000)]
    public Task FirstHeaderClickKeyboardAndCommandShouldUseTheActualPane(bool worksheetFilter, bool split) =>
        RunLoaded(worksheetFilter, split, async host =>
        {
            foreach (var pane in split ? Enum.GetValues<SpreadsheetPaneId>() : [SpreadsheetPaneId.TopLeft])
            {
                var expected = ExpectedHit(host, pane);
                var selection = host.Session.Selection.Capture();
                var undo = host.Session.History.UndoCount;
                await ClickHeader(host, expected.Bounds, cancelCapture: true);
                Assert.IsFalse(host.Presenter.IsOpen, "Losing native capture must cancel the pending header click.");
                Assert.AreEqual(selection.Version, host.Session.Selection.Capture().Version);
                Assert.AreEqual(undo, host.Session.History.UndoCount);
                await ClickHeader(host, expected.Bounds);
                await Ready(host);
                Assert.AreSame(host.Presenter, Field<NeraAutoFilterPagedPopupPresenter>(host.Window, "_filterPopup"));
                AssertAnchor(host, pane);
                Assert.AreEqual(selection.Version, host.Session.Selection.Capture().Version);
                Assert.AreEqual(undo, host.Session.History.UndoCount);
                Assert.AreEqual(worksheetFilter ? SpreadsheetAutoFilterOwnerKind.Worksheet : SpreadsheetAutoFilterOwnerKind.Table,
                    Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding").Target.OwnerKind);
                host.Presenter.Close();
                await Flush(host);
                Assert.IsTrue(Surface(host).Focus());
                PressAltDown(Surface(host));
                await Ready(host);
                AssertAnchor(host, pane);
                host.Presenter.Close();
                await Flush(host);
                Assert.IsTrue(await host.Runtime.TryActivateAsync("Sample.Filter"));
                await Ready(host);
                AssertAnchor(host, pane);
                host.Presenter.Close();
                await Flush(host);
                Assert.IsFalse(host.Presenter.IsOpen);
            }
        });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [Timeout(100_000)]
    public Task PagedSelectionApplyClearAndHistoryShouldStayCanonical(bool worksheetFilter, bool split) =>
        RunLoaded(worksheetFilter, split, async host =>
        {
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            Assert.HasCount(100, Values(host));
            var binding = Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding");
            Assert.AreEqual(250, binding.TotalItemCount);
            Click(Field<Button>(host.Presenter, "_nextButton"));
            await Drain(host);
            Assert.AreEqual(100, binding.PageOffset);
            Assert.HasCount(100, Values(host));
            Click(Field<Button>(host.Presenter, "_previousButton"));
            await Drain(host);
            Assert.AreEqual(0, binding.PageOffset);
            Field<TextBox>(host.Presenter, "_searchBox").Text = "Value 249";
            await Until(() => binding.SearchText == "Value 249" && Values(host).Count == 1);
            var undo = host.Session.History.UndoCount;
            Values(host).Single().IsChecked = false;
            Click(Field<Button>(host.Presenter, "_applyButton"));
            await Drain(host);
            Assert.IsFalse(host.Presenter.IsOpen);
            Assert.AreEqual(undo + 1, host.Session.History.UndoCount);
            Assert.IsFalse(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
            Assert.IsTrue(host.Session.Undo());
            Assert.IsTrue(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
            Assert.IsTrue(host.Session.Redo());
            Assert.IsFalse(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
            await Flush(host);
            Assert.IsTrue(Buttons(host).All(button => button.IsFiltered));
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            var root = Field<Popup>(host.Presenter, "_popup").Child;
            Click(Descendants(root).OfType<Button>().Single(button =>
                AutomationProperties.GetAutomationId(button) == "NeraAutoFilterPagedClear"));
            await Drain(host);
            Assert.AreEqual(undo + 2, host.Session.History.UndoCount);
            Assert.IsTrue(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
            Assert.IsTrue(host.Session.Undo());
            Assert.IsFalse(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
            Assert.IsTrue(host.Session.Redo());
            Assert.IsTrue(WorksheetSnapshot.Capture(host.Session.ActiveWorksheet).IsRowVisible(252));
        });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [Timeout(100_000)]
    public Task HeaderGeometryShouldTrackFractionalScrollHiddenAxesResizeAndIdle(bool worksheetFilter, bool split) =>
        RunLoaded(worksheetFilter, split, async host =>
        {
            var pane = split ? SpreadsheetPaneId.BottomRight : SpreadsheetPaneId.TopLeft;
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            AssertAnchor(host, pane);
            host.Window.Width = 1020;
            if (host.Split is { } controller) controller.SetSplit(280.75, 180.25);
            host.Grid.Zoom = 1.1;
            await Flush(host);
            Assert.IsTrue(host.Presenter.IsOpen, "A visible header must relocate across resize/zoom.");
            AssertAnchor(host, pane);
            if (host.Split is { } partial)
            {
                var frame = partial.LastFrame!;
                var paneFrame = frame.Panes.Single(item => item.Pane.PaneId == pane);
                Assert.IsTrue(frame.ScrollBars.TryGetBar(pane, SpreadsheetScrollBarOrientation.Vertical, out var bar));
                // Put the right edge of the header button four pixels under the vertical bar.
                host.Session.ActiveWorksheet.Dimensions.SetColumnWidth(1,
                    bar.Bounds.Left - paneFrame.Pane.Bounds.Left + paneFrame.ScrollX + host.Grid.RenderTheme.TableFilterButtonMargin + 4);
                await Flush(host);
                var clippedHeader = ExpectedHit(host, pane);
                Assert.IsTrue(clippedHeader.Bounds.Width > 0 && clippedHeader.Bounds.Width < host.Grid.RenderTheme.TableFilterButtonExtent);
                AssertAnchor(host, pane);
                var chrome = SpreadsheetChromeGeometry.Calculate(host.Grid.ActualWidth, host.Grid.ActualHeight, host.Grid.RenderTheme);
                Assert.IsFalse(host.Presenter.TryOpenAt(chrome.RowHeaderWidth + bar.Bounds.Left + 1,
                    clippedHeader.Bounds.Y + clippedHeader.Bounds.Height / 2), "A partially clipped filter must not steal the scrollbar hit.");
                host.Session.ActiveWorksheet.Dimensions.SetColumnWidth(1, 160);
                await Flush(host);
            }
            host.Session.ActiveWorksheet.Dimensions.HideColumns(1);
            await Flush(host);
            Assert.HasCount(0, Buttons(host));
            Assert.IsFalse(host.Presenter.IsOpen);
            host.Session.ActiveWorksheet.Dimensions.UnhideColumns(1);
            host.Session.ActiveWorksheet.Dimensions.HideRows(2);
            await Flush(host);
            Assert.HasCount(0, Buttons(host));
            host.Session.ActiveWorksheet.Dimensions.UnhideRows(2);
            await Flush(host);
            Assert.HasCount(split ? 4 : 1, Buttons(host));
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            if (host.Split is { } scrolling) scrolling.ScrollPaneTo(pane, 0.75, 400.25);
            else host.Grid.ScrollTo(0.75, 400.25);
            await Flush(host);
            await Until(() => !host.Presenter.IsOpen);
            if (host.Split is { } clipped)
            {
                var frame = clipped.LastFrame!;
                var chrome = SpreadsheetChromeGeometry.Calculate(host.Grid.ActualWidth, host.Grid.ActualHeight, host.Grid.RenderTheme);
                foreach (var bounds in frame.ScrollBars.Bars.Select(bar => bar.Bounds)
                    .Concat([frame.Layout.VerticalSeparator, frame.Layout.HorizontalSeparator]))
                {
                    if (bounds.IsEmpty) continue;
                    Assert.IsFalse(host.Presenter.TryOpenAt(chrome.RowHeaderWidth + bounds.X + bounds.Width / 2,
                        chrome.ColumnHeaderHeight + bounds.Y + bounds.Height / 2));
                }
            }
            Assert.IsFalse(host.Presenter.TryOpenAt(2, 2));
            var cached = Field<Array>(host.Presenter, "_nativeButtons");
            var nativeFrame = host.Split?.LastFrame;
            for (var i = 0; i < 100; i++) Assert.IsFalse(host.Presenter.TryOpenAt(-1, -1));
            Assert.AreSame(cached, Field<Array>(host.Presenter, "_nativeButtons"), "Raw hit tests must reuse presented geometry.");
            Assert.AreSame(nativeFrame, host.Split?.LastFrame, "Raw input must not compose another split frame.");
            var layoutEvents = 0;
            host.Window.LayoutUpdated += (_, _) => layoutEvents++;
            await Task.Delay(60);
            await Flush(host);
            Assert.IsLessThan(20, layoutEvents, "Unchanged filter chrome must reach dispatcher idle.");
        });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [Timeout(100_000)]
    public Task DraftRefusalAndStaleCallbacksShouldPreserveTheCurrentHost(bool worksheetFilter, bool split) =>
        RunLoaded(worksheetFilter, split, async host =>
        {
            host.Grid.BeginEdit("=SUM(1,2)");
            host.Grid.UpdateEditorDraft("=SUM(1,2)", 2, 3);
            var state = host.Session.Editor.State;
            var draft = host.Grid.CurrentEditorDraft;
            var focus = Keyboard.FocusedElement;
            var selection = host.Session.Selection.Capture();
            Assert.IsFalse(host.Presenter.TryOpenForActiveCell());
            var pane = split ? SpreadsheetPaneId.BottomRight : SpreadsheetPaneId.TopLeft;
            await ClickHeader(host, ExpectedHit(host, pane).Bounds);
            PressAltDown((UIElement)focus!);
            Assert.IsTrue(await host.Runtime.TryActivateAsync("Sample.Filter"));
            await Flush(host);
            Assert.IsFalse(host.Presenter.IsOpen);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreEqual(draft, host.Grid.CurrentEditorDraft);
            Assert.AreSame(focus, Keyboard.FocusedElement);
            Assert.AreEqual(selection.Version, host.Session.Selection.Capture().Version);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            var barEditor = Field<TextBox>(host.Window, "_formula");
            Assert.IsTrue(barEditor.Focus());
            Assert.IsFalse(host.Presenter.TryOpenForActiveCell());
            Assert.IsTrue(await host.Runtime.TryActivateAsync("Sample.Filter"));
            Assert.AreSame(barEditor, Keyboard.FocusedElement);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreEqual(draft, host.Grid.CurrentEditorDraft);
            host.Grid.CancelEditor();
            await Flush(host);
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            var oldPopup = Field<Popup>(host.Presenter, "_popup");
            var oldSearch = Field<TextBox>(host.Presenter, "_searchBox");
            var oldApply = Field<Button>(host.Presenter, "_applyButton");
            var oldChoice = Values(host).First();
            Field<TextBox>(host.Presenter, "_searchBox").Text = "old request";
            host.Presenter.Close();
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            var newBinding = Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding");
            Invoke(host.Presenter, "OnPopupClosed", oldPopup, EventArgs.Empty);
            await Ready(host);
            oldSearch.Text = "obsolete control";
            oldChoice.IsChecked = false;
            Click(oldApply);
            await Task.Delay(180);
            Assert.AreEqual(string.Empty, newBinding.SearchText);
            Assert.IsTrue(host.Presenter.IsOpen);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            Assert.AreSame(newBinding, Field<NeraWpfAutoFilterPagedBinding>(host.Presenter, "_binding"));
            Assert.IsTrue(Field<TextBox>(host.Presenter, "_searchBox").IsKeyboardFocusWithin);
            var original = host.Session.ActiveWorksheet;
            var other = host.Session.Workbook.AddWorksheet("Other");
            Field<TextBox>(host.Presenter, "_searchBox").Text = "stale";
            host.Session.ActivateWorksheet(other);
            Assert.IsFalse(host.Presenter.IsOpen, "Sheet changes close the native popup synchronously.");
            await Flush(host);
            Assert.HasCount(0, Buttons(host));
            host.Session.ActivateWorksheet(original);
            host.Session.Selection.SetActiveCell(Header);
            await Flush(host);
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            if (split)
            {
                var previous = host.Split!;
                host.Grid.DisableSplitPanes();
                Assert.IsFalse(host.Presenter.IsOpen);
                Assert.IsTrue(previous.IsDisposed);
                host.Grid.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
                await Flush(host);
            }
            if (!host.Presenter.IsOpen) Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            var decorator = (System.Windows.Documents.AdornerDecorator)host.Grid.Parent;
            decorator.Child = null;
            host.Window.UpdateLayout();
            await Task.Delay(30);
            Assert.IsFalse(host.Presenter.IsOpen);
            decorator.Child = host.Grid;
            await Flush(host);
            Assert.IsTrue(host.Presenter.TryOpenForActiveCell());
            await Ready(host);
            host.Grid.Session = new SpreadsheetSession(new Workbook());
            await Flush(host);
            Assert.IsFalse(host.Presenter.IsOpen);
            Assert.HasCount(0, Buttons(host));
            host.Window.Dispose();
            await Flush(host);
            Invoke(host.Presenter, "OnPopupClosed", oldPopup, EventArgs.Empty);
            Assert.IsFalse(host.Presenter.IsOpen);
            Assert.AreEqual(0, host.Session.History.UndoCount);
        });

    private sealed record Host(RibbonPreviewWindow Window, NeraSpreadsheetControl Grid, SpreadsheetSession Session,
        NeraAutoFilterPagedPopupPresenter Presenter, RibbonRuntimeController Runtime)
    {
        internal NeraSpreadsheetSplitController? Split => Grid.TryGetSplitPaneController(out var split) ? split : null;
    }

    private static Task RunLoaded(bool worksheetFilter, bool split, Func<Host, Task> verify)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var workbook = new Workbook();
                    var worksheet = workbook.Worksheets[0];
                    worksheet.SetValue(Header, "Values");
                    for (var i = 0; i < 250; i++) worksheet.SetValue(new CellAddress(3 + i, 1), $"Value {i:000}");
                    var range = new CellRange(Header, new CellAddress(252, 1));
                    if (worksheetFilter) worksheet.SetAutoFilter(new WorksheetAutoFilter(range));
                    else worksheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "FilterValues", range,
                        [new SpreadsheetTableColumn(Guid.NewGuid(), "Values")]));
                    worksheet.Dimensions.SetColumnWidth(1, 160);
                    worksheet.Dimensions.HideRows(0);
                    worksheet.Dimensions.HideColumns(0);
                    var session = new SpreadsheetSession(workbook);
                    session.Selection.SetActiveCell(Header);
                    if (split) session.View.SetSplitState(new SpreadsheetSplitViewState(SpreadsheetSplitViewMode.Both,
                        300.5, 180.25, SpreadsheetSplitViewPane.BottomRight, new(0.25, 0.5), new(0.5, 0.75), new(0.75, 1.25), new(1.25, 1.5)));
                    using var window = new RibbonPreviewWindow(session)
                    {
                        Width = 1000, Height = 760, Left = 0, Top = 0, ShowInTaskbar = false, Topmost = true,
                    };
                    try
                    {
                        window.Show();
                        Assert.IsTrue(window.Activate());
                        var grid = Field<NeraSpreadsheetControl>(window, "_sheet");
                        var host = new Host(window, grid, session, Field<NeraAutoFilterPagedPopupPresenter>(window, "_filterPopup"),
                            Field<RibbonRuntimeController>(window, "_runtime"));
                        await Flush(host);
                        Assert.HasCount(split ? 4 : 1, Buttons(host));
                        if (host.Split is { } fractional)
                        {
                            Assert.AreEqual(new PointD(0.25, 0.5), fractional.GetPaneScroll(SpreadsheetPaneId.TopLeft));
                            Assert.AreEqual(new PointD(1.25, 1.5), fractional.GetPaneScroll(SpreadsheetPaneId.BottomRight));
                        }
                        await verify(host);
                    }
                    finally
                    {
                        window.Close();
                        await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                    }
                    completion.SetResult();
                }
                catch (Exception exception) { completion.SetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(90));
    }

    private static UIElement Surface(Host host) => host.Split is { } split ? Field<UIElement>(split, "_adorner") : host.Grid;
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static object? Invoke(object value, string name, params object?[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, args);
    private static SpreadsheetAutoFilterButtonHit[] Buttons(Host host) => (SpreadsheetAutoFilterButtonHit[])Invoke(host.Presenter, "GetVisibleButtons")!;
    private static List<CheckBox> Values(Host host) => Field<List<CheckBox>>(host.Presenter, "_valueCheckBoxes");
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static async Task Flush(Host host)
    {
        // The sample switches its native surface at ContextIdle after a sheet change.
        // Resolve the surface only after that queued host transition has completed.
        await host.Window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle).Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.Window.UpdateLayout();
        if (host.Split is { IsDisposed: false, IsAttached: true } split) split.RenderNow();
        else if (host.Grid.IsLoaded && !Field<bool>(host.Grid, "_disposed") &&
            Field<ViewportLayout?>(host.Grid, "_lastLayout") is null)
        {
            host.Grid.InvalidateVisual();
            host.Window.UpdateLayout();
        }
        await host.Window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle).Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (host.Split is null && host.Grid.IsLoaded && !Field<bool>(host.Grid, "_disposed"))
            await Until(() => Field<ViewportLayout?>(host.Grid, "_lastLayout") is not null);
    }
    private static async Task Drain(Host host)
    {
        await Field<Task>(host.Presenter, "_operationTail").WaitAsync(TimeSpan.FromSeconds(5));
        await Flush(host);
    }
    private static async Task Ready(Host host)
    {
        Assert.IsTrue(host.Presenter.IsOpen);
        await Drain(host);
        Assert.IsTrue(host.Presenter.IsOpen);
        Assert.IsTrue(Values(host).Count > 0);
        var root = (FrameworkElement)Field<Popup>(host.Presenter, "_popup").Child;
        foreach (var id in new[] { "Clear", "Cancel", "Apply", "Previous", "Next" })
        {
            var button = Descendants(root).OfType<Button>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "NeraAutoFilterPaged" + id);
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
            Assert.IsTrue(button.IsVisible && bounds.Width > 0 && bounds.Height > 0 &&
                new Rect(0, 0, root.ActualWidth, root.ActualHeight).Contains(bounds), $"{id} must be fully visible inside the popup.");
            if (!button.IsEnabled) continue;
            var hit = root.InputHitTest(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)) as DependencyObject;
            while (hit is not null && !ReferenceEquals(hit, button)) hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            Assert.AreSame(button, hit, $"{id} must expose a native pointer target.");
        }
    }
    private static async Task Until(Func<bool> condition)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (!condition()) { Assert.IsTrue(DateTime.UtcNow < end, "Native condition timed out."); await Task.Delay(10); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static SpreadsheetAutoFilterButtonHit ExpectedHit(Host host, SpreadsheetPaneId pane)
    {
        var layout = host.Split is { } split ? split.LastFrame!.Panes.Single(item => item.Pane.PaneId == pane).ViewportFrame.Layout
            : Field<ViewportLayout>(host.Grid, "_lastLayout");
        var hit = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(host.Session.ActiveWorksheet.Tables,
            host.Session.ActiveWorksheet.AutoFilter, layout, host.Grid.RenderTheme).Single();
        var origin = host.Split?.LastFrame!.Panes.Single(item => item.Pane.PaneId == pane).Pane.Bounds ?? default;
        var chrome = SpreadsheetChromeGeometry.Calculate(host.Grid.ActualWidth, host.Grid.ActualHeight, host.Grid.RenderTheme);
        var bounds = hit.Bounds.Translate(origin.X, origin.Y);
        if (host.Split is { } clipping)
        {
            var frame = clipping.LastFrame!;
            var right = frame.ScrollBars.TryGetBar(pane, SpreadsheetScrollBarOrientation.Vertical, out var vertical) ? vertical.Bounds.Left : origin.Right;
            var bottom = frame.ScrollBars.TryGetBar(pane, SpreadsheetScrollBarOrientation.Horizontal, out var horizontal) ? horizontal.Bounds.Top : origin.Bottom;
            bounds = bounds.Intersect(new RectD(origin.X, origin.Y, right - origin.X, bottom - origin.Y));
        }
        return hit with { Bounds = bounds.Translate(chrome.RowHeaderWidth, chrome.ColumnHeaderHeight) };
    }
    private static void AssertAnchor(Host host, SpreadsheetPaneId pane)
    {
        var expected = ExpectedHit(host, pane);
        var popup = Field<Popup>(host.Presenter, "_popup");
        Assert.AreSame(Surface(host), popup.PlacementTarget);
        Assert.AreEqual(expected.Bounds.Left, popup.HorizontalOffset, 0.01);
        Assert.AreEqual(expected.Bounds.Bottom, popup.VerticalOffset, 0.01);
        if (host.Split is { } split) Assert.AreEqual(pane, split.ActivePane);
    }
    private static async Task ClickHeader(Host host, RectD bounds, bool cancelCapture = false)
    {
        Assert.IsTrue(GetCursorPos(out var original));
        var point = host.Grid.PointToScreen(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        var surface = Surface(host);
        var downSeen = false;
        var pressed = false;
        MouseButtonEventHandler observeDown = (_, args) =>
        {
            downSeen = true;
            Console.WriteLine($"Native filter down: point={args.GetPosition(surface)}, expected={bounds}, handled={args.Handled}, source={args.OriginalSource.GetType().Name}.");
        };
        surface.AddHandler(Mouse.PreviewMouseDownEvent, observeDown, handledEventsToo: true);
        try
        {
            Assert.IsTrue(SetCursorPos((int)Math.Round(point.X), (int)Math.Round(point.Y)));
            await Task.Delay(20);
            MouseEvent(0x0002, 0, 0, 0, UIntPtr.Zero);
            pressed = true;
            await Until(() => downSeen);
            await host.Window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background);
            Console.WriteLine($"Native filter before up: open={host.Presenter.IsOpen}, generation={Field<long>(host.Presenter, "_openGeneration")}, draft={host.Grid.CurrentEditorDraft is not null}.");
            if (cancelCapture) surface.ReleaseMouseCapture();
            MouseEvent(0x0004, 0, 0, 0, UIntPtr.Zero);
            pressed = false;
            await Task.Delay(30);
            await Flush(host);
            Console.WriteLine($"Native filter after up: open={host.Presenter.IsOpen}, active={host.Session.Selection.ActiveCell}, focus={Keyboard.FocusedElement?.GetType().Name}.");
        }
        finally
        {
            if (pressed) MouseEvent(0x0004, 0, 0, 0, UIntPtr.Zero);
            surface.RemoveHandler(Mouse.PreviewMouseDownEvent, observeDown);
            Assert.IsTrue(SetCursorPos(original.X, original.Y));
        }
    }
    private static void PressAltDown(UIElement target)
    {
        var original = new byte[256];
        Assert.IsTrue(GetKeyboardState(original));
        var keys = new byte[256];
        keys[0x12] = keys[0xA4] = 0x80;
        try
        {
            Assert.IsTrue(SetKeyboardState(keys));
            Assert.AreEqual(ModifierKeys.Alt, Keyboard.Modifiers);
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, Key.Down)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(args, null);
            target.RaiseEvent(args);
            Assert.IsTrue(args.Handled);
        }
        finally { Assert.IsTrue(SetKeyboardState(original)); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(uint flags, uint deltaX, uint deltaY, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] state);
}
