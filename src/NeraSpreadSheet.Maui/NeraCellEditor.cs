using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui;

// The platform subclass only intercepts editor commands; MAUI owns text/IME mapping.
internal sealed class NeraCellEditor : Editor
{
    internal Func<string, bool, bool, bool, bool>? HandleKey { get; set; }
}
