using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Maui;
using NativeTextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

internal static class Table007EditorSmoke
{
    private static readonly List<object> _commitEvidence = [];
    private static bool _commitEvidenceClipped;

    internal static object CommitEvidence => new
    {
        schema = "table007WindowsCommitV2",
        commitArgumentObserved = false,
        clipped = _commitEvidenceClipped,
        points = _commitEvidence,
    };

    internal static async Task RunAsync(NeraSpreadsheetEditorHost host)
    {
        _commitEvidence.Clear();
        _commitEvidenceClipped = false;
        Trace("table-editor-enter");
        var view = host.Spreadsheet;
        var session = view.Session ?? throw new InvalidOperationException("The editor has no canonical session.");
        var sheet = session.ActiveWorksheet;
        var table = new SpreadsheetTable(Guid.NewGuid(), "EditorSales",
            new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]);
        sheet.AddTable(table);
        for (var row = 1; row <= 3; row++) sheet.SetValue(new CellAddress(row, 1), row * 10d);
        session.Selection.SetActiveCell(new CellAddress(6, 0));
        Require(host.BeginEdit("=SUM(EditorSales[Am"), "The loaded editor did not open.");
        Trace("table-editor-opened");
        var editor = (Editor)typeof(NeraSpreadsheetEditorHost).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        Require(editor.Handler?.PlatformView is NativeTextBox, "The overlay did not resolve a native multiline TextBox.");
        var native = (NativeTextBox)editor.Handler!.PlatformView!;
        Require(session.Editor.State is not null, "The shared session editor did not begin.");
        Require(host.CurrentStructuredReferenceSuggestions.Count == 1, "The Table completion popup is missing.");
        Require(host.AcceptStructuredReferenceSuggestion(0), "The Table candidate was not accepted.");
        Trace("table-editor-candidate-accepted");
        Require(host.CurrentEditText == "=SUM(EditorSales[[#Data],[Amount]]", "The structured reference differs.");
        Require(session.History.UndoCount == 0, "Completion changed workbook history.");
        var initialEdit = session.Editor.State;
        var initialUndoCount = session.History.UndoCount;
        var initialRedoCount = session.History.RedoCount;
        void Capture(string phase) => CaptureCommitEvidence(phase, host, editor, native,
            session, sheet, initialEdit, initialUndoCount, initialRedoCount);
        void OnCommitStateChanged(object? sender, CellEditStateChangedEventArgs e)
        {
            if (e.State is null) Capture("canonical-ended");
        }
        session.Editor.StateChanged += OnCommitStateChanged;
        try
        {
            Capture("after-completion");
            native.Text += ")";
            await PressNativeAsync(host, native, 0x0D,
                beforeInput: () => Capture("before-native-enter"));
            Capture("after-native-enter");
        }
        finally
        {
            session.Editor.StateChanged -= OnCommitStateChanged;
        }
        Trace("table-editor-enter-returned");
        Require(!session.Editor.IsEditing, "Native Enter did not commit the editor.");
        Require(Equals(sheet.GetValue(new CellAddress(6, 0)), 60d), "The committed Table formula value differs.");
        Require(session.History.UndoCount == 1 && session.Undo() && session.Redo(), "The editor commit did not use session history.");
        Trace("table-editor-history-returned");
        session.Selection.SetActiveCell(new CellAddress(6, 0));
        Require(host.BeginEdit("=EditorSales[Am"), "The reused editor did not reopen.");
        var count = session.History.UndoCount;
        editor.CursorPosition = 1;
        host.AcceptStructuredReferenceSuggestion(0);
        Require(host.CurrentEditText == "=EditorSales[Am", "A stale caret changed the draft.");
        Require(host.CancelEditor() && session.History.UndoCount == count, "Cancel changed history.");
        Trace("table-editor-stale-caret-cancel-returned");
        Require(host.BeginEdit("first"), "The reused editor did not reopen for a multiline draft.");
        await PressNativeAsync(host, native, 0x0D, alt: true);
        Trace("table-editor-alt-enter-returned");
        Require(host.CurrentEditText?.ReplaceLineEndings("\n") == "first\n" && session.Editor.IsEditing,
            $"Native Alt+Enter differs: editing={session.Editor.IsEditing}, draft={System.Text.Json.JsonSerializer.Serialize(host.CurrentEditText)}.");
        Require(ReferenceEquals(native, editor.Handler?.PlatformView), "Editing created another native overlay.");
        Require(editor.Clip is RectangleGeometry, "The editor has no cell/viewport clip.");
        var priorWidth = sheet.Dimensions.GetColumnWidth(0);
        var priorZoom = view.Zoom;
        var priorScroll = view.ScrollSnapshot;
        var priorFontSize = editor.FontSize;
        try
        {
            sheet.Dimensions.SetColumnWidth(0, view.Width * 2d);
            Trace("table-editor-column-resized");
            view.ZoomTo(priorZoom * 1.25d, 0, 0);
            Trace("table-editor-zoomed");
            view.ScrollTo(0, 0);
            await Task.Delay(100);
            Trace("table-editor-geometry-settled");
            Require(view.TryGetEditorBounds(new CellAddress(6, 0), out var raw, out var visible),
                "The edited cell lost geometry after zoom.");
            Require(raw.Width > visible.Width && Math.Abs(editor.Width - raw.Width) < 2d,
                "The native editor wrap width was reduced to its viewport clip.");
            Require(Math.Abs(editor.FontSize - priorFontSize * 1.25d) < 0.01d,
                "The native draft font did not track the new zoom.");
            Require(editor.Clip is RectangleGeometry geometry && Math.Abs(geometry.Rect.Width - visible.Width) < 2d,
                "The native clip no longer matches visible cell geometry.");
            Require(session.Editor.IsEditing && session.History.UndoCount == count,
                "Editor geometry changed the active draft or history.");
        }
        finally
        {
            sheet.Dimensions.SetColumnWidth(0, priorWidth);
            view.ZoomTo(priorZoom, 0, 0);
            view.ScrollTo(priorScroll.OffsetX, priorScroll.OffsetY);
        }
        await PressNativeAsync(host, native, 0x1B);
        Trace("table-editor-escape-returned");
        Require(!session.Editor.IsEditing, "Native Escape did not cancel the multiline draft.");
        var cellsBeforeCancel = sheet.EnumerateUsedCells().ToArray();
        var selectionBeforeCancel = session.Selection.Capture();
        var historyBeforeCancel = session.History.UndoCount;
        var redoBeforeCancel = session.History.RedoCount;
        Require(host.BeginEdit("=SUM(EditorSales[Am"), "The native editor did not reopen for canonical cancellation.");
        Require(editor.IsVisible && host.CurrentStructuredReferenceSuggestions.Count == 1,
            "Canonical cancellation requires a visible native draft and candidates.");
        Require(session.Editor.Cancel(), "Canonical cancellation did not end the edit.");
        session.Selection.SetActiveCell(new CellAddress(7, 2));
        var selectionVersion = session.Selection.Version;
        Require(!host.CancelEditor(), "Repeated cancellation reported another canonical edit.");
        Require(!editor.IsVisible && string.IsNullOrEmpty(native.Text) && host.CurrentEditText is null &&
            host.CurrentStructuredReferenceSuggestions.Count == 0, "Canonical cancellation left native draft or candidates visible.");
        Require(session.Selection.Version == selectionVersion && session.Selection.ActiveCell == new CellAddress(7, 2),
            "Cleanup changed the caller's newer selection.");
        Require(session.History.UndoCount == historyBeforeCancel && session.History.RedoCount == redoBeforeCancel &&
            cellsBeforeCancel.SequenceEqual(sheet.EnumerateUsedCells()), "Cleanup changed cells or history.");
        session.Selection.Restore(selectionBeforeCancel);
        host.SetEnglishResources(true);
        Require((string)host.Resources["CellEditor.Commit"] == "Commit", "Shell-local editor resources were not applied.");
        host.SetEnglishResources(false);
        sheet.RemoveTable(table.Id);
        Trace("table-editor-complete");
    }

    private static void CaptureCommitEvidence(string phase, NeraSpreadsheetEditorHost host,
        Editor editor, NativeTextBox native, SpreadsheetSession session, Worksheet sheet,
        CellEditState? initialEdit, int initialUndoCount, int initialRedoCount)
    {
        if (_commitEvidence.Count >= 4)
        {
            _commitEvidenceClipped = true;
            return;
        }
        var target = new CellAddress(6, 0);
        var cell = sheet.GetCell(target);
        var nativeText = native.Text;
        var managedText = editor.Text;
        var undoDelta = session.History.UndoCount - initialUndoCount;
        var redoDelta = session.History.RedoCount - initialRedoCount;
        _commitEvidence.Add(new
        {
            phase,
            nativeText = DescribeCommitText(nativeText),
            managedText = DescribeCommitText(managedText),
            publicDraft = DescribeCommitText(host.CurrentEditText),
            nativeManagedEqual = string.Equals(nativeText, managedText, StringComparison.Ordinal),
            nativeSelectionStart = Math.Clamp(native.SelectionStart, 0, 128),
            nativeSelectionLength = Math.Clamp(native.SelectionLength, 0, 128),
            managedCursor = Math.Clamp(editor.CursorPosition, 0, 128),
            managedSelectionLength = Math.Clamp(editor.SelectionLength, 0, 128),
            nativeFocused = native.FocusState != Microsoft.UI.Xaml.FocusState.Unfocused,
            editing = session.Editor.IsEditing,
            sameEditState = initialEdit is not null && ReferenceEquals(session.Editor.State, initialEdit),
            editAddressExpected = session.Editor.State?.Address == target,
            activeCellExpected = session.Selection.ActiveCell == target,
            sameSession = ReferenceEquals(host.Spreadsheet.Session, session),
            sameWorksheet = ReferenceEquals(session.ActiveWorksheet, sheet),
            targetFormula = DescribeCommitText(cell.Formula),
            targetKind = cell.Value.Kind switch
            {
                CellValueKind.Blank => "Blank",
                CellValueKind.Number => "Number",
                CellValueKind.Text => "Text",
                CellValueKind.Boolean => "Boolean",
                CellValueKind.DateTime => "DateTime",
                CellValueKind.Error => "Error",
                _ => "Unknown",
            },
            targetIsExpectedNumber = Equals(cell.Value.RawValue, 60d),
            targetIsSeedText = Equals(cell.Value.RawValue, "R7C1"),
            sourceValuesExpected = Equals(sheet.GetValue(new CellAddress(1, 1)), 10d) &&
                Equals(sheet.GetValue(new CellAddress(2, 1)), 20d) &&
                Equals(sheet.GetValue(new CellAddress(3, 1)), 30d),
            undoDelta = Math.Clamp(undoDelta, -64, 64),
            redoDelta = Math.Clamp(redoDelta, -64, 64),
            valuesClipped = native.SelectionStart is < 0 or > 128 || native.SelectionLength is < 0 or > 128 ||
                editor.CursorPosition is < 0 or > 128 || editor.SelectionLength is < 0 or > 128 ||
                undoDelta is < -64 or > 64 || redoDelta is < -64 or > 64,
        });
    }

    private static object DescribeCommitText(string? text) => new
    {
        isNull = text is null,
        isEmpty = text is "",
        isSeedText = text is "R7C1",
        isInitialPrefix = text is "=SUM(EditorSales[Am",
        isCompletionPrefix = text is "=SUM(EditorSales[[#Data],[Amount]]",
        isCompleteFormula = text is "=SUM(EditorSales[[#Data],[Amount]])",
        length = Math.Min(text?.Length ?? 0, 128),
        clipped = (text?.Length ?? 0) > 128,
    };

    internal static void Trace(string stage)
    {
        var resultPath = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (!string.IsNullOrWhiteSpace(resultPath))
            File.AppendAllText(resultPath + ".trace", stage + Environment.NewLine);
    }

    internal static void TraceNativeSurface(NeraSpreadsheetEditorHost host)
    {
        var surface = host.Spreadsheet.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        Trace(surface?.IsLoaded == true ? "smoke-native-surface-loaded" : "smoke-native-surface-unloaded");
        var editor = (Editor)typeof(NeraSpreadsheetEditorHost).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var focused = surface?.XamlRoot is { } root
            ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) : null;
        Trace(focused is null ? "smoke-native-focus-none" :
            ReferenceEquals(focused, surface) ? "smoke-native-focus-surface" :
            ReferenceEquals(focused, editor.Handler?.PlatformView) ? "smoke-native-focus-editor" : "smoke-native-focus-other");
    }

    private static async Task PressNativeAsync(NeraSpreadsheetEditorHost host, NativeTextBox editor, byte key,
        bool alt = false, Action? beforeInput = null)
    {
        var window = host.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The editor has no loaded native window.");
        window.Activate();
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SetForegroundWindow(handle);
        Require(editor.Focus(Microsoft.UI.Xaml.FocusState.Keyboard), "The native editor did not take keyboard focus.");
        await Task.Delay(80);
        Require(GetForegroundWindow() == handle, "Keyboard injection requires the smoke window to be foreground.");
        beforeInput?.Invoke();
        try
        {
            if (alt) KeyEvent(0x12, 0, 0, 0);
            KeyEvent(key, 0, 0, 0);
            KeyEvent(key, 0, 2, 0);
        }
        finally { if (alt) KeyEvent(0x12, 0, 2, 0); }
        await Task.Delay(100);
    }

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeyEvent(byte key, byte scan, uint flags, nuint extraInfo);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
