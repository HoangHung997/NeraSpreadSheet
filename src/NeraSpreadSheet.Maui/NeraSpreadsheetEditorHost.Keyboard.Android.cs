#if ANDROID
using Android.Views;
using Android.Widget;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetEditorHost
{
    private EditText? _nativeEditor;

    partial void AttachNativeEditor()
    {
        DetachNativeEditor();
        if (_disposed || _editor.Handler?.PlatformView is not EditText editor) return;
        _nativeEditor = editor;
        editor.KeyPress += OnNativeEditorKeyPress;
        editor.EditorAction += OnNativeEditorAction;
    }

    partial void DetachNativeEditor()
    {
        if (_nativeEditor is { } editor)
        {
            editor.KeyPress -= OnNativeEditorKeyPress;
            editor.EditorAction -= OnNativeEditorAction;
        }
        _nativeEditor = null;
    }

    private void OnNativeEditorAction(object? sender, TextView.EditorActionEventArgs e)
    {
        if (e.ActionId == Android.Views.InputMethods.ImeAction.Done && _session?.Editor.IsEditing == true)
        {
            CommitEditor();
            e.Handled = true;
        }
    }

    private void OnNativeEditorKeyPress(object? sender, Android.Views.View.KeyEventArgs e)
    {
        if (_disposed || _session?.Editor.IsEditing != true || e.Event is not { } key || key.IsCtrlPressed) return;
        if (_nativeEditor?.EditableText is { } text && Android.Views.InputMethods.BaseInputConnection.GetComposingSpanStart(text) >= 0) return;
        var handled = e.KeyCode is Keycode.Enter or Keycode.NumpadEnter or Keycode.Escape ||
            e.KeyCode == Keycode.Tab && !key.IsAltPressed && !key.IsShiftPressed && _candidates.Count > 0;
        if (!handled) return;
        e.Handled = true;
        if (key.Action != KeyEventActions.Down || key.RepeatCount > 0) return;
        if (e.KeyCode == Keycode.Escape) CancelEditor();
        else if (e.KeyCode == Keycode.Tab) AcceptStructuredReferenceSuggestion(_selectedCandidate);
        else if (key.IsAltPressed) InsertNewline();
        else CommitEditor();
    }
}
#endif
