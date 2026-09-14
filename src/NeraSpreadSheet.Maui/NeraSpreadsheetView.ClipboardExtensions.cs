using Microsoft.Maui.ApplicationModel.DataTransfer;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Native clipboard integration for the MAUI spreadsheet host. MAUI's portable Clipboard API
/// exposes text only, so paste deliberately uses the shared TSV fallback rather than pretending
/// that a private rich payload id survived an operating-system round trip.
/// </summary>
public static class NeraSpreadsheetViewClipboardExtensions
{
    public static async ValueTask<bool> CopyToOperatingSystemClipboardAsync(
        this NeraSpreadsheetView view,
        bool cut = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Session;
        if (session is null || session.Editor.IsEditing)
        {
            return false;
        }

        return await session.Clipboard.CopyToClipboardAsync(
            async (package, token) =>
            {
                token.ThrowIfCancellationRequested();
                await Clipboard.Default.SetTextAsync(package.ToTabSeparatedText()).ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
            },
            cut,
            cancellationToken).ConfigureAwait(true);
    }

    public static async ValueTask<bool> PasteFromOperatingSystemClipboardAsync(
        this NeraSpreadsheetView view,
        SpreadsheetClipboardPasteMode mode = SpreadsheetClipboardPasteMode.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Session;
        if (session is null || session.Editor.IsEditing)
        {
            return false;
        }

        return await session.Clipboard.PasteFromClipboardAsync(
            async token =>
            {
                token.ThrowIfCancellationRequested();
                if (!Clipboard.Default.HasText)
                {
                    return null;
                }
                var text = await Clipboard.Default.GetTextAsync().ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                return text is null ? null : new SpreadsheetClipboardReadResult(text);
            },
            mode,
            cancellationToken).ConfigureAwait(true);
    }
}
