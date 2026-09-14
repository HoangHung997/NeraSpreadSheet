using System.Runtime.InteropServices;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraSpreadsheetControl
{
    /// <summary>
    /// Copies or cuts through the Windows clipboard. Cut source mutation is deferred until
    /// the operating-system write has completed successfully.
    /// </summary>
    public bool CopyToOperatingSystemClipboard(bool cut = false)
    {
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
    /// Reads the real Windows clipboard. Matching private ids retain rich Nera payloads;
    /// foreign/replaced clipboard content is imported as Unicode TSV text.
    /// </summary>
    public bool PasteFromOperatingSystemClipboard(
        SpreadsheetClipboardPasteMode mode = SpreadsheetClipboardPasteMode.All)
    {
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
                    if (Clipboard.TryGetData<string>(
                            SpreadsheetClipboardNativeFormats.PayloadId,
                            out var raw) &&
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_session is not null && !IsEditing)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                _ = CopyToOperatingSystemClipboard();
                return true;
            }
            if (keyData == (Keys.Control | Keys.X))
            {
                _ = CopyToOperatingSystemClipboard(cut: true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                _ = PasteFromOperatingSystemClipboard();
                return true;
            }
            if (keyData == Keys.Escape && _session.Clipboard.CancelCopyMode())
            {
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static bool CompleteClipboardOperation(ValueTask<bool> pending) =>
        pending.IsCompletedSuccessfully
            ? pending.Result
            : pending.AsTask().GetAwaiter().GetResult();

    private static bool IsExpectedClipboardFailure(Exception exception) =>
        exception is ExternalException or InvalidOperationException;
}
