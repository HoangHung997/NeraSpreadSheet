#if IOS || MACCATALYST
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace NeraSpreadSheet.Maui;

internal sealed class NeraCellEditorHandler : EditorHandler
{
    protected override MauiTextView CreatePlatformView() => new CellTextView(this);

    private sealed class CellTextView(NeraCellEditorHandler handler) : MauiTextView
    {
        public override void InsertText(string text)
        {
            if (MarkedTextRange is null && text == "\n" &&
                handler.VirtualView is NeraCellEditor editor && editor.HandleKey?.Invoke("Enter", false, false, false) == true)
                return;
            base.InsertText(text);
        }

        public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent? evt)
        {
            if (MarkedTextRange is null && presses.Count == 1 && handler.VirtualView is NeraCellEditor editor)
            {
                foreach (var press in presses)
                {
                    if (press.Key is not { } key) continue;
                    var command = key.CharactersIgnoringModifiers switch
                    {
                        "\r" or "\n" => "Enter",
                        "\u001b" => "Escape",
                        "\t" => "Tab",
                        _ => string.Empty,
                    };
                    if (command.Length > 0 && editor.HandleKey?.Invoke(command,
                            (key.ModifierFlags & UIKeyModifierFlags.Alternate) != 0,
                            (key.ModifierFlags & UIKeyModifierFlags.Shift) != 0,
                            (key.ModifierFlags & (UIKeyModifierFlags.Control | UIKeyModifierFlags.Command)) != 0) == true)
                        return;
                }
            }
            base.PressesBegan(presses, evt);
        }
    }
}
#endif
