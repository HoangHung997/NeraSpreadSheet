namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class MainWindow : IDisposable
{
    /// <summary>Closes the native window and releases its caller-owned spreadsheet control.
    /// Must be called on the Avalonia UI thread. A cancelled close leaves the live UI intact.</summary>
    public void Dispose()
    {
        VerifyAccess();
        if (!_isClosed)
        {
            Close();
            if (!_isClosed && IsVisible)
                throw new InvalidOperationException("Window closing was cancelled; the live host has not been disposed.");
            // Also supports disposing a constructed window that was never shown.
            if (!_isClosed) OnClosed(this, EventArgs.Empty);
        }
        _sheet.Dispose();
        GC.SuppressFinalize(this);
    }
}
