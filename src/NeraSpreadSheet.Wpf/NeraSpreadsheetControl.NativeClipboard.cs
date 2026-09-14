using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraSpreadsheetControl
{
    /// <summary>
    /// Copies or cuts the current worksheet selection through the actual Windows clipboard.
    /// A cut clears its source only after Clipboard.SetDataObject has acknowledged the write.
    /// </summary>
    public bool CopyToOperatingSystemClipboard(bool cut = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is null || IsEditing)
        {
            return false;
        }

        try
        {
            var pending = _session.Clipboard.CopyToClipboardAsync(
                static (package, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var data = new DataObject();
                    data.SetData(DataFormats.UnicodeText, package.ToTabSeparatedText());
                    data.SetData(
                        SpreadsheetClipboardNativeFormats.PayloadId,
                        package.PayloadId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
                    Clipboard.SetDataObject(data, copy: true);
                    return ValueTask.CompletedTask;
                },
                cut);
            return CompleteClipboardOperation(pending);
        }
        catch (Exception exception) when (IsExpectedClipboardFailure(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the actual Windows clipboard and pastes at the active cell. When the private
    /// Nera payload id still matches, rich formulas/styles remain available; otherwise the
    /// external Unicode text payload is imported instead of falling back to stale session data.
    /// </summary>
    public bool PasteFromOperatingSystemClipboard(
        SpreadsheetClipboardPasteMode mode = SpreadsheetClipboardPasteMode.All)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is null || IsEditing)
        {
            return false;
        }

        try
        {
            var pending = _session.Clipboard.PasteFromClipboardAsync(
                static cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        return ValueTask.FromResult<SpreadsheetClipboardReadResult?>(null);
                    }

                    var text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    Guid? payloadId = null;
                    if (Clipboard.ContainsData(SpreadsheetClipboardNativeFormats.PayloadId) &&
                        Clipboard.GetData(SpreadsheetClipboardNativeFormats.PayloadId) is string raw &&
                        Guid.TryParse(raw, out var parsed))
                    {
                        payloadId = parsed;
                    }
                    return ValueTask.FromResult<SpreadsheetClipboardReadResult?>(
                        new SpreadsheetClipboardReadResult(text, payloadId));
                },
                mode);
            return CompleteClipboardOperation(pending);
        }
        catch (Exception exception) when (IsExpectedClipboardFailure(exception))
        {
            return false;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || _disposed || _session is null || IsEditing)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            switch (e.Key)
            {
                case Key.C:
                    _ = CopyToOperatingSystemClipboard();
                    e.Handled = true;
                    return;
                case Key.X:
                    _ = CopyToOperatingSystemClipboard(cut: true);
                    e.Handled = true;
                    return;
                case Key.V:
                    _ = PasteFromOperatingSystemClipboard();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Escape && _session.Clipboard.CancelCopyMode())
        {
            e.Handled = true;
        }
    }

    private static bool CompleteClipboardOperation(ValueTask<bool> pending) =>
        pending.IsCompletedSuccessfully
            ? pending.Result
            : pending.AsTask().GetAwaiter().GetResult();

    private static bool IsExpectedClipboardFailure(Exception exception) =>
        exception is ExternalException or InvalidOperationException;
}
