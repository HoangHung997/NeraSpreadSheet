using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Viewport;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Release009FormulaBarSmokeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void FormulaBarShouldMirrorOneCanonicalDraftAndCommitExactlyOnce(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            Assert.IsNull(host.Grid.CurrentEditorDraft);
            Assert.AreEqual("original", host.Bar.Text);
            Assert.IsTrue(host.Bar.Focus());
            var state = host.Session.Editor.State;
            Assert.IsNotNull(state);
            Assert.AreSame(host.Bar, Keyboard.FocusedElement);
            host.Bar.SelectAll();
            host.Bar.SelectedText = "=20+22";
            host.Bar.Select(2, 2);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreEqual(host.Bar.Text, host.Grid.CurrentEditorDraft!.Text);
            Assert.AreEqual(2, host.Editor.SelectionStart);
            Assert.AreEqual(2, host.Editor.SelectionLength);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            Assert.AreEqual("original", host.Session.ActiveWorksheet.GetValue(default));
            Pump(host.Window);
            Assert.AreEqual(2, host.Bar.SelectionStart);
            Assert.AreEqual(2, host.Bar.SelectionLength);
            host.Bar.Select(4, 0);
            EditingCommands.SelectLeftByCharacter.Execute(null, host.Bar);
            EditingCommands.SelectLeftByCharacter.Execute(null, host.Bar);
            host.Runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-GB")));
            Pump(host.Window);
            Assert.AreEqual("Formula bar", AutomationProperties.GetName(host.Bar));
            EditingCommands.SelectLeftByCharacter.Execute(null, host.Bar);
            Assert.AreEqual(1, host.Bar.SelectionStart);
            Assert.AreEqual(3, host.Bar.SelectionLength, "A same-range shell refresh must preserve the bar's native backward selection.");
            Press(host.Bar, Key.F2);
            Assert.AreSame(host.Editor, Keyboard.FocusedElement);
            Assert.AreSame(state, host.Session.Editor.State);
            host.Editor.Select(host.Editor.Text.Length, 0);
            host.Editor.SelectedText = "+1";
            Assert.AreEqual("=20+22+1", host.Bar.Text);
            Assert.IsTrue(host.Bar.Focus());
            Press(host.Bar, Key.Enter);
            Pump(host.Window);
            Assert.AreEqual(43d, host.Session.ActiveWorksheet.GetValue(default));
            Assert.AreEqual(new CellAddress(1, 0), host.Session.Selection.ActiveCell);
            Assert.IsNull(host.Grid.CurrentEditorDraft);
            Assert.AreEqual(1, host.Session.History.UndoCount);
            Assert.IsFalse(host.Grid.CommitEditor());
            Assert.IsTrue(host.Session.Undo());
            Assert.AreEqual("original", host.Session.ActiveWorksheet.GetValue(default));
            Assert.IsTrue(host.Session.Redo());
            Assert.AreEqual(43d, host.Session.ActiveWorksheet.GetValue(default));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void ValidationFailureShouldRetainBarFocusDraftRangeAndHistory(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            host.Session.ActiveWorksheet.AddDataValidationRule(new DataValidationRule(Guid.NewGuid(),
                [new CellRange(default, default)], DataValidationType.Whole, DataValidationOperator.Between,
                "1", "10", allowBlank: false, showErrorMessage: true));
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "20";
            host.Bar.Select(0, 1);
            var draft = host.Grid.CurrentEditorDraft;
            var state = host.Session.Editor.State;
            Press(host.Bar, Key.Enter);
            Pump(host.Window);
            Assert.AreEqual(draft, host.Grid.CurrentEditorDraft);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreSame(host.Bar, Keyboard.FocusedElement);
            Assert.AreEqual(0, host.Bar.SelectionStart);
            Assert.AreEqual(1, host.Bar.SelectionLength);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            host.Bar.SelectAll();
            host.Bar.SelectedText = "5";
            Press(host.Bar, Key.Enter);
            Assert.AreEqual(5d, host.Session.ActiveWorksheet.GetValue(default));
            Assert.AreEqual(1, host.Session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void AltEnterShouldInsertOneLineAndLeaveKeyTipsUsableWhileEscapeCancels(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "firstsecond";
            host.Bar.Select(5, 0);
            var state = host.Session.Editor.State;
            Press(host.Bar, Key.LeftAlt, ModifierKeys.Alt);
            Assert.AreEqual(RibbonKeyTipScope.Tabs, host.Ribbon.KeyTipScope);
            Press(host.Bar, Key.Enter, ModifierKeys.Alt, systemKey: true);
            Assert.AreEqual(RibbonKeyTipScope.Inactive, host.Ribbon.KeyTipScope);
            Assert.AreEqual("first" + Environment.NewLine + "second", host.Bar.Text);
            Assert.AreEqual(host.Bar.Text, host.Grid.CurrentEditorDraft!.Text);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreSame(host.Bar, Keyboard.FocusedElement);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            Press(host.Bar, Key.Escape);
            Pump(host.Window);
            Assert.IsNull(host.Grid.CurrentEditorDraft);
            Assert.AreEqual("original", host.Bar.Text);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "first" + Environment.NewLine + "second";
            Press(host.Bar, Key.Enter);
            Assert.AreEqual("first" + Environment.NewLine + "second", host.Session.ActiveWorksheet.GetValue(default));
            Assert.AreEqual(1, host.Session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void BarTextShortcutsShouldUseNativeTextHistoryAndClipboardBeforeWorkbookBindings(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            var marker = new CellAddress(12, 5);
            host.Session.SetValue(marker, CellValue.FromNumber(99));
            Assert.AreEqual(1, host.Session.History.UndoCount);
            Assert.IsTrue(host.Bar.Focus());
            Press(host.Bar, Key.Z, ModifierKeys.Control);
            Assert.AreEqual(1, host.Session.History.UndoCount, "Empty text Undo must not fall through to workbook Undo.");
            host.Bar.SelectAll();
            host.Bar.SelectedText = "draft";
            Assert.IsTrue(host.Bar.CanUndo);
            Press(host.Bar, Key.Z, ModifierKeys.Control);
            Assert.AreEqual("original", host.Bar.Text);
            Assert.AreEqual("original", host.Grid.CurrentEditorDraft!.Text);
            Press(host.Bar, Key.Y, ModifierKeys.Control);
            Assert.AreEqual("draft", host.Bar.Text);
            host.Bar.Select(1, 2);
            Press(host.Bar, Key.C, ModifierKeys.Control);
            Assert.AreEqual("ra", Clipboard.GetText());
            Press(host.Bar, Key.X, ModifierKeys.Control);
            Assert.AreEqual("dft", host.Bar.Text);
            Press(host.Bar, Key.V, ModifierKeys.Control);
            Assert.AreEqual("draft", host.Bar.Text);
            Assert.AreEqual(host.Bar.Text, host.Grid.CurrentEditorDraft!.Text);
            Assert.AreEqual(99d, host.Session.ActiveWorksheet.GetValue(marker));
            Assert.AreEqual(1, host.Session.History.UndoCount);
            Press(host.Bar, Key.Escape);
            Assert.IsFalse(host.Bar.IsKeyboardFocusWithin);
            Press((UIElement)Keyboard.FocusedElement!, Key.Z, ModifierKeys.Control);
            Pump(host.Window);
            Assert.AreEqual(0, host.Session.History.UndoCount, "The Ribbon workbook shortcut must still work outside the bar.");
            Assert.IsNull(host.Session.ActiveWorksheet.GetValue(marker));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void FormulaCommandsAndPointModeShouldKeepTheDraftAnchorAndExposeCurrentHelp(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            host.Grid.BeginEdit("=SUM");
            var state = host.Session.Editor.State;
            Assert.IsTrue(host.Bar.Focus());
            Press(host.Bar, Key.Tab);
            Assert.AreSame(host.Editor, Keyboard.FocusedElement);
            Assert.AreEqual("=SUM", host.Grid.CurrentEditorDraft!.Text, "Bar Tab only transfers focus to native completion.");
            Press(host.Editor, Key.Tab);
            Assert.AreEqual("=SUM(", host.Grid.CurrentEditorDraft!.Text);
            Assert.AreSame(state, host.Session.Editor.State);
            var reference = new CellAddress(2, 1);
            PointD point;
            var chrome = SpreadsheetChromeGeometry.Calculate(host.Grid.ActualWidth, host.Grid.ActualHeight, host.Grid.RenderTheme);
            if (host.Split is { } split)
            {
                split.RenderNow();
                var engine = Field<SpreadsheetSplitViewportEngine>(host.Surface, "_engine");
                Assert.IsTrue(engine.TryGetCellBounds(split.ActivePane, reference, out var bounds));
                point = new PointD(chrome.RowHeaderWidth + bounds.X + bounds.Width / 2,
                    chrome.ColumnHeaderHeight + bounds.Y + bounds.Height / 2);
            }
            else
            {
                var engine = Field<SpreadsheetViewportEngine>(host.Grid, "_viewport");
                Assert.IsTrue(engine.TryGetCellBounds(reference, host.Grid.ScrollSnapshot.OffsetX, host.Grid.ScrollSnapshot.OffsetY, out var bounds));
                point = new PointD(chrome.RowHeaderWidth + bounds.X + bounds.Width / 2,
                    chrome.ColumnHeaderHeight + bounds.Y + bounds.Height / 2);
            }
            var method = useSplit ? "TryInsertFormulaReference" : "TryBeginFormulaReferencePointer";
            Assert.IsTrue((bool)host.Surface.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(host.Surface, [new System.Windows.Point(point.X, point.Y)])!);
            ((UIElement)host.Surface).ReleaseMouseCapture();
            host.Session.Selection.SetActiveCell(reference);
            Pump(host.Window);
            Assert.AreSame(state, host.Session.Editor.State);
            Assert.AreEqual(default(CellAddress), host.Grid.CurrentEditorDraft!.Address);
            Assert.AreEqual("A1", Field<TextBlock>(host.Window, "_address").Text);
            Assert.AreEqual(host.Grid.CurrentEditorDraft.Text, host.Bar.Text);
            foreach (var (id, text) in new[] { ("Sample.FormulaSum", "=SUM("), ("Sample.FormulaAverage", "=AVERAGE("),
                ("Sample.FormulaIf", "=IF("), ("Sample.FormulaLookup", "=XLOOKUP(") })
            {
                Assert.IsTrue(host.Runtime.TryActivateAsync(id).AsTask().GetAwaiter().GetResult());
                Pump(host.Window);
                Assert.AreSame(state, host.Session.Editor.State);
                Assert.AreEqual(default(CellAddress), host.Grid.CurrentEditorDraft!.Address);
                Assert.AreEqual(text, host.Bar.Text);
                Assert.AreEqual(0, host.Session.History.UndoCount);
            }
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "=SUM(1,IF(2,3,4))";
            host.Bar.Select(host.Bar.Text.IndexOf('4'), 0);
            Assert.IsTrue(host.Runtime.TryActivateAsync("Sample.FormulaHelp").AsTask().GetAwaiter().GetResult());
            Pump(host.Window);
            var popup = Field<Popup>(host.Window, "_formulaBarHelpPopup");
            Assert.IsTrue(popup.IsOpen);
            StringAssert.Contains(Field<TextBlock>(host.Window, "_formulaBarHelpText").Text, "IF(");
            StringAssert.Contains(Field<TextBlock>(host.Window, "_formulaBarHelpText").Text, "Đối số 3:");
            Assert.AreSame(host.Bar, Keyboard.FocusedElement);
            Assert.AreSame(state, host.Session.Editor.State);
            Press(host.Bar, Key.Enter);
            Assert.AreEqual(4d, host.Session.ActiveWorksheet.GetValue(default));
            Assert.IsNull(host.Session.ActiveWorksheet.GetValue(reference));
            Assert.AreEqual(1, host.Session.History.UndoCount);
            Assert.IsFalse(popup.IsOpen);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void QueuedRefreshShouldUseCurrentSheetAndNeverResurrectCanceledOrDisposedDrafts(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            var second = host.Session.Workbook.AddWorksheet("Other");
            second.SetValue(default, "other sheet");
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "temporary";
            host.Session.Selection.SetActiveCell(new CellAddress(4, 4));
            host.Session.ActivateWorksheet(second);
            Pump(host.Window);
            Assert.IsNull(host.Grid.CurrentEditorDraft);
            Assert.AreEqual("other sheet", host.Bar.Text);
            Assert.AreEqual("A1", Field<TextBlock>(host.Window, "_address").Text);
            Assert.AreEqual(0, host.Session.History.UndoCount);
            second.SetValue(default, "latest raw value");
            Assert.AreEqual("latest raw value", host.Bar.Text);
            Assert.IsTrue(host.Bar.Focus());
            host.Bar.SelectAll();
            host.Bar.SelectedText = "canceled";
            Assert.IsTrue(host.Session.Editor.Cancel());
            Pump(host.Window);
            Assert.IsNull(host.Grid.CurrentEditorDraft);
            Assert.AreEqual("latest raw value", host.Bar.Text);
            host.Session.Selection.SetActiveCell(new CellAddress(1, 1));
            host.Window.Dispose();
            host.Bar.Text = "detached view";
            Pump(host.Window);
            Assert.AreEqual("detached view", host.Bar.Text);
            Assert.IsFalse(host.Session.Editor.IsEditing);
            Assert.AreEqual(0, host.Session.History.UndoCount);
        });
    }

    private static void Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None, bool systemKey = false)
    {
        var original = new byte[256];
        Assert.IsTrue(GetKeyboardState(original));
        var state = (byte[])original.Clone();
        foreach (var code in new[] { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C }) state[code] = 0;
        if ((modifiers & ModifierKeys.Control) != 0) state[0x11] = state[0xA2] = 0x80;
        if ((modifiers & ModifierKeys.Alt) != 0) state[0x12] = state[0xA4] = 0x80;
        if ((modifiers & ModifierKeys.Shift) != 0) state[0x10] = state[0xA0] = 0x80;
        try
        {
            Assert.IsTrue(SetKeyboardState(state));
            Assert.AreEqual(modifiers, Keyboard.Modifiers, "The native UI thread must expose the requested modifiers.");
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, key)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            if (systemKey)
            {
                // Construct WPF's actual SystemKey shape without changing production input code.
                typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(args, null);
                Assert.AreEqual(Key.System, args.Key);
                Assert.AreEqual(key, args.SystemKey);
            }
            target.RaiseEvent(args);
            Assert.IsTrue(args.Handled, $"The loaded window did not handle {modifiers}+{key}.");
        }
        finally { Assert.IsTrue(SetKeyboardState(original)); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] state);

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
        window.UpdateLayout();
    }

    private sealed record Host(RibbonPreviewWindow Window, SpreadsheetSession Session, NeraSpreadsheetControl Grid,
        TextBox Bar, object Surface, TextBox Editor, NeraSpreadsheetSplitController? Split,
        RibbonRuntimeController Runtime, NeraRibbonControl Ribbon);

    private static void RunLoaded(bool useSplit, Action<Host> verify)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workbook = new Workbook();
                workbook.Worksheets[0].SetValue(default, "original");
                var session = new SpreadsheetSession(workbook);
                if (useSplit) session.View.SetSplitState(new SpreadsheetSplitViewState(SpreadsheetSplitViewMode.Both,
                    380.5, 180.25, SpreadsheetSplitViewPane.BottomRight, default, default, default, default));
                using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
                try
                {
                    window.Show();
                    Pump(window);
                    var grid = Field<NeraSpreadsheetControl>(window, "_sheet");
                    var split = useSplit ? Field<NeraSpreadsheetSplitController>(window, "_splitShell") : null;
                    split?.RenderNow();
                    Pump(window);
                    object surface = split is null ? grid : Field<object>(split, "_adorner");
                    var bar = Field<TextBox>(window, "_formula");
                    Assert.AreEqual("Nera.FormulaBar.Editor", AutomationProperties.GetAutomationId(bar));
                    if (split is not null) Assert.IsFalse(grid.Focusable || grid.IsHitTestVisible);
                    verify(new Host(window, session, grid, bar, surface, Field<TextBox>(surface, "_editor"), split,
                        Field<RibbonRuntimeController>(window, "_runtime"), Field<NeraRibbonControl>(window, "_ribbon")));
                }
                finally { window.Close(); Pump(window); }
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(65)), "Loaded formula bar smoke timed out.");
        failure?.Throw();
    }
}
