using System.Windows.Forms;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;
    private const int WindowMessageCaptureChanged = 0x0215;

    protected override void WndProc(ref Message message)
    {
        switch (message.Msg)
        {
            case WindowMessageLeftButtonDown:
            {
                var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                if (HitTestSeparator(clientX, clientY) is null &&
                    TryGetHeaderResizeHandle(clientX, clientY, out _))
                {
                    if (IsEditing)
                    {
                        CommitEditor();
                    }
                    Focus();
                    TryBeginHeaderResize(clientX, clientY);
                    message.Result = IntPtr.Zero;
                    return;
                }
                break;
            }
            case WindowMessageMouseMove:
            {
                var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                if (_headerResize is { } resize)
                {
                    ApplyHeaderResize(resize, clientX, clientY);
                    Cursor = GetHeaderResizeCursor(resize.Axis);
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_splitDrag is null &&
                    HitTestSeparator(clientX, clientY) is null &&
                    TryGetHeaderResizeHandle(clientX, clientY, out var handle))
                {
                    Cursor = GetHeaderResizeCursor(handle.Axis);
                    message.Result = IntPtr.Zero;
                    return;
                }
                break;
            }
            case WindowMessageLeftButtonUp:
                if (_headerResize is { } resize)
                {
                    var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                    ApplyHeaderResize(resize, clientX, clientY);
                    _headerResize = null;
                    Capture = false;
                    UpdatePointerCursor(clientX, clientY);
                    message.Result = IntPtr.Zero;
                    return;
                }
                break;
            case WindowMessageCaptureChanged:
                if (_headerResize is not null)
                {
                    _headerResize = null;
                    Cursor = Cursors.Default;
                }
                break;
        }

        base.WndProc(ref message);
    }

    private static (double X, double Y) GetMouseCoordinates(IntPtr lParam)
    {
        var packed = unchecked((int)lParam.ToInt64());
        return (
            unchecked((short)(packed & 0xFFFF)),
            unchecked((short)((packed >> 16) & 0xFFFF)));
    }
}
