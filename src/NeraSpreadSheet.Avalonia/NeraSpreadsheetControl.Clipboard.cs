using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    private static readonly ConditionalWeakTable<SpreadsheetClipboardController, ClipboardStamp> ClipboardStamps = new();
    private long _clipboardEpoch;
    private bool _clipboardBusy;
    private bool _clipboardHooksAttached;
    internal INeraClipboardTransport? ClipboardTransportOverride { get; set; }

    /// <summary>Copies one rectangular selection to the OS clipboard. Cut clears the
    /// original range only after a successful write and an unchanged session lease.</summary>
    public async ValueTask<bool> CopyToClipboardAsync(bool cut = false, CancellationToken cancellationToken = default)
    {
        VerifyUsable();
        if (_clipboardBusy || _session is null || _session.Editor.IsEditing) return false;
        if (_session.Selection.Ranges.Count != 1)
            throw new InvalidOperationException("Copy or cut requires one rectangular selection.");
        EnsureClipboardHooks();
        var transport = GetClipboardTransport(); var lease = CaptureClipboardLease();
        cancellationToken.ThrowIfCancellationRequested(); _clipboardBusy = true;
        try
        {
            var package = lease.Session.Clipboard.CopyPrimarySelection();
            var text = package.ToTabSeparatedText(); ValidateClipboardText(text);
            var token = Guid.NewGuid().ToString("N");
            await transport.WriteAsync(new NeraClipboardData(text, token), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsClipboardLeaseValid(lease)) return false;
            ClipboardStamps.Remove(lease.Session.Clipboard);
            ClipboardStamps.Add(lease.Session.Clipboard, new ClipboardStamp(token, text, package));
            return !cut || lease.Session.ClearSelection();
        }
        finally { _clipboardBusy = false; }
    }

    /// <summary>Reads current OS data, never stale in-process clipboard contents.
    /// Results arriving after a sheet/view/selection/workbook change are discarded.</summary>
    public async ValueTask<bool> PasteFromClipboardAsync(CancellationToken cancellationToken = default)
    {
        VerifyUsable();
        if (_clipboardBusy || _session is null || _session.Editor.IsEditing) return false;
        EnsureClipboardHooks();
        var transport = GetClipboardTransport(); var lease = CaptureClipboardLease();
        cancellationToken.ThrowIfCancellationRequested(); _clipboardBusy = true;
        try
        {
            var data = await transport.ReadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsClipboardLeaseValid(lease) || data is null) return false;
            ValidateClipboardText(data.Text);
            var controller = lease.Session.Clipboard;
            var samePackage = data.Token is not null && ClipboardStamps.TryGetValue(controller, out var stamp) &&
                string.Equals(stamp.Token, data.Token, StringComparison.Ordinal) &&
                string.Equals(stamp.Text, data.Text, StringComparison.Ordinal) && ReferenceEquals(controller.Clipboard, stamp.Package);
            if (!samePackage)
            {
                controller.ImportTabSeparatedText(data.Text);
                ClipboardStamps.Remove(controller);
            }
            return controller.Paste(lease.Address);
        }
        finally { _clipboardBusy = false; }
    }

    private void EnsureClipboardHooks()
    {
        if (_clipboardHooksAttached) return;
        _clipboardHooksAttached = true;
        // Both event publishers are this control; no external service retains the host.
        SessionChanged += OnClipboardContextChanged;
        DetachedFromVisualTree += (_, _) => _clipboardEpoch++;
    }
    private void OnClipboardContextChanged(object? sender, EventArgs e) => _clipboardEpoch++;
    private INeraClipboardTransport GetClipboardTransport() => ClipboardTransportOverride ??
        new NeraSystemClipboardTransport(TopLevel.GetTopLevel(this)?.Clipboard ??
            throw new InvalidOperationException("Attach the spreadsheet to a desktop window before using the OS clipboard."));
    private ClipboardLease CaptureClipboardLease() => new(_session!, _session!.ActiveWorksheet,
        _session.Selection.ActiveCell, _session.Selection.Capture().Version, _session.Workbook.Version, _clipboardEpoch);
    private bool IsClipboardLeaseValid(ClipboardLease lease) => !_disposed && _clipboardEpoch == lease.Epoch &&
        ReferenceEquals(_session, lease.Session) && ReferenceEquals(_session.ActiveWorksheet, lease.Worksheet) &&
        _session.Workbook.Version == lease.WorkbookVersion && _session.Selection.Capture().Version == lease.SelectionVersion &&
        _session.Selection.ActiveCell == lease.Address && !_session.Editor.IsEditing;
    private static void ValidateClipboardText(string text)
    {
        if (text.Length > 16 * 1024 * 1024) throw new InvalidOperationException("Clipboard text exceeds the 16 Mi-character limit.");
    }
    private async void ExecuteClipboardInput(Key key)
    {
        try { if (key == Key.V) await PasteFromClipboardAsync(); else await CopyToClipboardAsync(cut: key == Key.X); }
        catch (Exception exception)
        {
            if (InteractionFailed is not { } handler) throw;
            handler(this, new SpreadsheetInteractionFailedEventArgs(exception));
        }
    }
    private sealed record ClipboardStamp(string Token, string Text, SpreadsheetClipboardPackage Package);
    private sealed record ClipboardLease(SpreadsheetSession Session, Worksheet Worksheet, CellAddress Address, long SelectionVersion, long WorkbookVersion, long Epoch);
}

internal sealed record NeraClipboardData(string Text, string? Token = null);
internal interface INeraClipboardTransport
{
    ValueTask WriteAsync(NeraClipboardData value, CancellationToken cancellationToken);
    ValueTask<NeraClipboardData?> ReadAsync(CancellationToken cancellationToken);
}
internal sealed class NeraSystemClipboardTransport : INeraClipboardTransport
{
    private static readonly DataFormat<string> StampFormat = DataFormat.CreateStringApplicationFormat("NeraSpreadSheet.Selection-v1");
    private readonly IClipboard _clipboard;
    public NeraSystemClipboardTransport(IClipboard clipboard) => _clipboard = clipboard;
    public async ValueTask WriteAsync(NeraClipboardData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = new DataTransferItem(); item.Set(DataFormat.Text, value.Text);
        if (value.Token is not null) item.Set(StampFormat, value.Token);
        var transfer = new DataTransfer(); transfer.Add(item);
        // Clipboard owns the transfer after SetDataAsync; readers own their returned wrapper.
        await _clipboard.SetDataAsync(transfer);
        cancellationToken.ThrowIfCancellationRequested();
        await _clipboard.FlushAsync();
    }
    public async ValueTask<NeraClipboardData?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var data = await _clipboard.TryGetDataAsync();
        if (data is null) return null;
        var text = await data.TryGetValueAsync(DataFormat.Text);
        var token = await data.TryGetValueAsync(StampFormat);
        cancellationToken.ThrowIfCancellationRequested();
        return text is null ? null : new NeraClipboardData(text, token);
    }
}
