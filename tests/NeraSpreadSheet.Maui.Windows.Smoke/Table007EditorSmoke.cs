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
    internal static async Task RunAsync(NeraSpreadsheetEditorHost host)
    {
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
        var editor = (Editor)typeof(NeraSpreadsheetEditorHost).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        Require(editor.Handler?.PlatformView is NativeTextBox, "The overlay did not resolve a native multiline TextBox.");
        var native = (NativeTextBox)editor.Handler!.PlatformView!;
        Require(session.Editor.State is not null, "The shared session editor did not begin.");
        Require(host.CurrentStructuredReferenceSuggestions.Count == 1, "The Table completion popup is missing.");
        Require(host.AcceptStructuredReferenceSuggestion(0), "The Table candidate was not accepted.");
        Require(host.CurrentEditText == "=SUM(EditorSales[[#Data],[Amount]]", "The structured reference differs.");
        Require(session.History.UndoCount == 0, "Completion changed workbook history.");
        native.Text += ")";
        await PressNativeAsync(host, native, 0x0D);
        Require(!session.Editor.IsEditing, "Native Enter did not commit the editor.");
        Require(Equals(sheet.GetValue(new CellAddress(6, 0)), 60d), "The committed Table formula value differs.");
        Require(session.History.UndoCount == 1 && session.Undo() && session.Redo(), "The editor commit did not use session history.");
        session.Selection.SetActiveCell(new CellAddress(6, 0));
        Require(host.BeginEdit("=EditorSales[Am"), "The reused editor did not reopen.");
        var count = session.History.UndoCount;
        editor.CursorPosition = 1;
        host.AcceptStructuredReferenceSuggestion(0);
        Require(host.CurrentEditText == "=EditorSales[Am", "A stale caret changed the draft.");
        Require(host.CancelEditor() && session.History.UndoCount == count, "Cancel changed history.");
        Require(host.BeginEdit("first"), "The reused editor did not reopen for a multiline draft.");
        await PressNativeAsync(host, native, 0x0D, alt: true);
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
            view.ZoomTo(priorZoom * 1.25d, 0, 0);
            view.ScrollTo(0, 0);
            await Task.Delay(100);
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
        Require(!session.Editor.IsEditing, "Native Escape did not cancel the multiline draft.");
        host.SetEnglishResources(true);
        Require((string)host.Resources["CellEditor.Commit"] == "Commit", "Shell-local editor resources were not applied.");
        host.SetEnglishResources(false);
        sheet.RemoveTable(table.Id);
    }

    private static async Task PressNativeAsync(NeraSpreadsheetEditorHost host, NativeTextBox editor, byte key, bool alt = false)
    {
        var window = host.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The editor has no loaded native window.");
        window.Activate();
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SetForegroundWindow(handle);
        Require(editor.Focus(Microsoft.UI.Xaml.FocusState.Keyboard), "The native editor did not take keyboard focus.");
        await Task.Delay(80);
        Require(GetForegroundWindow() == handle, "Keyboard injection requires the smoke window to be foreground.");
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
