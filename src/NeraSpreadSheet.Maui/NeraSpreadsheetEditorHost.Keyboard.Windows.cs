#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetEditorHost
{
    private TextBox? _nativeEditor;
    private bool _nativeComposition;

    partial void AttachNativeEditor()
    {
        DetachNativeEditor();
        if (_disposed || _editor.Handler?.PlatformView is not TextBox editor) return;
        _nativeEditor = editor;
        editor.PreviewKeyDown += OnNativeEditorKeyDown;
        editor.TextCompositionStarted += OnNativeCompositionStarted;
        editor.TextCompositionEnded += OnNativeCompositionEnded;
    }

    partial void DetachNativeEditor()
    {
        if (_nativeEditor is { } editor)
        {
            editor.PreviewKeyDown -= OnNativeEditorKeyDown;
            editor.TextCompositionStarted -= OnNativeCompositionStarted;
            editor.TextCompositionEnded -= OnNativeCompositionEnded;
        }
        _nativeEditor = null;
        _nativeComposition = false;
    }

    private void OnNativeCompositionStarted(TextBox sender, TextCompositionStartedEventArgs e) => _nativeComposition = true;
    private void OnNativeCompositionEnded(TextBox sender, TextCompositionEndedEventArgs e) => _nativeComposition = false;

    private void OnNativeEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_disposed || _nativeComposition || _session?.Editor.IsEditing != true ||
            _nativeEditor?.FocusState == FocusState.Unfocused) return;
        var alt = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0;
        var control = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
        var shift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;
        if (control) return;
        // ProcessKey and composition keys retain the native IME path.
        switch (e.Key)
        {
            case VirtualKey.Enter:
                if (alt) InsertNewline();
                else CommitEditor();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                CancelEditor();
                e.Handled = true;
                break;
            case VirtualKey.Tab when !alt && !shift && _candidates.Count > 0:
                e.Handled = AcceptStructuredReferenceSuggestion(_selectedCandidate);
                break;
            case VirtualKey.Up or VirtualKey.Down when !alt && !shift && _candidates.Count > 0:
                _selectedCandidate = Math.Clamp(_selectedCandidate + (e.Key == VirtualKey.Down ? 1 : -1), 0, _candidates.Count - 1);
                e.Handled = true;
                break;
        }
    }
}
#endif
