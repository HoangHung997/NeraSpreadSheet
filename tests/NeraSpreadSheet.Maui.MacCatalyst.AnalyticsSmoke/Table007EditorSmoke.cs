using System.Reflection;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using Foundation;
using UIKit;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

internal static class Table007EditorSmoke
{
    internal static async Task RunAsync(NeraSpreadsheetEditorHost host)
    {
        SmokeTrace.Append("table-editor-enter");
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
            SmokeTrace.Append("table-editor-opened");
            await Task.Delay(60);
            SmokeTrace.Append("table-editor-native-ready");
            var editor = (Editor)typeof(NeraSpreadsheetEditorHost).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
            var native = editor.Handler?.PlatformView as UITextView
                ?? throw new InvalidOperationException("The overlay has no native editor.");
            Require(host.CurrentStructuredReferenceSuggestions.Count == 1, "The native Table popup is missing.");
            Require(host.AcceptStructuredReferenceSuggestion(0), "The Table candidate was not accepted.");
            SmokeTrace.Append("table-editor-candidate-accepted");
            Require(host.CurrentEditText == "=SUM(EditorSales[[#Data],[Amount]]", "Structured completion differs.");
            Require(session.History.UndoCount == 0, "Completion changed workbook history.");
            native.InsertText(")");
            SmokeTrace.Append("table-editor-native-text-inserted");
            native.InsertText("\n");
            SmokeTrace.Append("table-editor-native-enter-returned");
            Require(!session.Editor.IsEditing, "Native Enter did not commit.");
            Require(Equals(sheet.GetValue(target), 60d), "The native editor formula value differs.");
            Require(session.History.UndoCount == 1 && session.Undo(), "Commit did not use session undo.");
            SmokeTrace.Append("table-editor-undo-returned");
            session.Selection.SetActiveCell(target);
            Require(host.BeginEdit("first"), "The same overlay did not reopen.");
            SmokeTrace.Append("table-editor-reopened");
            await Task.Delay(60);
            Require(ReferenceEquals(native, editor.Handler?.PlatformView), "The native overlay was recreated.");
            native.SetMarkedText("語", new NSRange(1, 0));
            SmokeTrace.Append("table-editor-marked-text-set");
            Require(native.MarkedTextRange is not null, "Native marked text was not active.");
            native.InsertText("\n");
            SmokeTrace.Append("table-editor-marked-enter-returned");
            Require(session.Editor.IsEditing, "Enter committed while IME marked text was active.");
            native.UnmarkText();
            SmokeTrace.Append("table-editor-unmark-returned");
            Require(host.CancelEditor(), "The marked-text draft did not cancel.");
            SmokeTrace.Append("table-editor-cancel-returned");
        }
        finally
        {
            SmokeTrace.Append("table-editor-cleanup-enter");
            if (session.Editor.IsEditing) host.CancelEditor();
            sheet.RemoveTable(table.Id);
            session.Selection.Restore(selection);
            SmokeTrace.Append("table-editor-cleanup-returned");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
