using System.Reflection;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using NativeText = global::Android.Widget.EditText;
using NativeKey = global::Android.Views.KeyEvent;
using NativeKeycode = global::Android.Views.Keycode;
using NativeKeyAction = global::Android.Views.KeyEventActions;
using NativeMeta = global::Android.Views.MetaKeyStates;

namespace NeraSpreadSheet.Maui.Android.AnalyticsSmoke;

internal static class Table007EditorSmoke
{
    internal static void Run(NeraSpreadsheetEditorHost host)
    {
        var session = host.Spreadsheet.Session ?? throw new InvalidOperationException("The editor has no session.");
        var sheet = session.ActiveWorksheet;
        var selection = session.Selection.Capture();
        var target = new CellAddress(6, 0);
        var table = new SpreadsheetTable(Guid.NewGuid(), "EditorSales",
            new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]);
        sheet.AddTable(table);
        try
        {
            session.Selection.SetActiveCell(target);
            Require(host.BeginEdit("=SUM(EditorSales[Am"), "The loaded reused editor did not open.");
            var editor = (Editor)typeof(NeraSpreadsheetEditorHost).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
            var native = editor.Handler?.PlatformView as NativeText
                ?? throw new InvalidOperationException("The overlay has no native editor.");
            Require(host.CurrentStructuredReferenceSuggestions.Count == 1, "The native Table popup is missing.");
            Require(host.AcceptStructuredReferenceSuggestion(0), "The Table candidate was not accepted.");
            Require(host.CurrentEditText == "=SUM(EditorSales[[#Data],[Amount]]", "Structured completion differs.");
            Require(session.History.UndoCount == 0, "Completion changed workbook history.");
            native.Text += ")";
            Press(native, NativeKeycode.Enter);
            Require(!session.Editor.IsEditing, "Native Enter did not commit.");
            Require(Equals(sheet.GetValue(target), 60d), "The native editor formula value differs.");
            Require(session.History.UndoCount == 1 && session.Undo(), "Commit did not use session undo.");
            session.Selection.SetActiveCell(target);
            Require(host.BeginEdit("first"), "The same overlay did not reopen.");
            Require(ReferenceEquals(native, editor.Handler?.PlatformView), "The native overlay was recreated.");
            Press(native, NativeKeycode.Enter, NativeMeta.AltOn);
            Require(host.CurrentEditText == "first\n" && session.Editor.IsEditing, "Native Alt+Enter committed instead of inserting a newline.");
            Press(native, NativeKeycode.Escape);
            Require(!session.Editor.IsEditing, "Native Escape did not cancel.");
        }
        finally
        {
            if (session.Editor.IsEditing) host.CancelEditor();
            sheet.RemoveTable(table.Id);
            session.Selection.Restore(selection);
        }
    }

    private static void Press(NativeText editor, NativeKeycode key, NativeMeta meta = NativeMeta.None)
    {
        using var down = new NativeKey(0, 0, NativeKeyAction.Down, key, 0, meta);
        using var up = new NativeKey(0, 0, NativeKeyAction.Up, key, 0, meta);
        Require(editor.DispatchKeyEvent(down), "The native editor did not handle key down.");
        editor.DispatchKeyEvent(up);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
