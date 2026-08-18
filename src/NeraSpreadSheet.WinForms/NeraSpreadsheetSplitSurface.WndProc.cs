using System.Windows.Forms;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;
    private const int WindowMessageCaptureChanged = 0x0215;
    private const long MouseKeyLeftButton = 0x0001L;

    protected override void WndProc(ref Message message)
    {
        switch (message.Msg)
        {
            case WindowMessageLeftButtonDown:
            {
                var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                if (TryBeginScrollBarInteraction(clientX, clientY))
                {
                    message.Result = IntPtr.Zero;
                    return;
                }
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

                _ = TryBeginHeaderReorderCandidate(clientX, clientY);
                break;
            }
            case WindowMessageMouseMove:
            {
                var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                if (_scrollBarDrag is not null)
                {
                    UpdateScrollBarDrag(clientX, clientY);
                    Cursor = Cursors.Hand;
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_headerResize is { } activeResize)
                {
                    ApplyHeaderResize(activeResize, clientX, clientY);
                    Cursor = GetHeaderResizeCursor(activeResize.Axis);
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_headerReorder is not null &&
                    UpdateHeaderReorder(
                        clientX,
                        clientY,
                        (message.WParam.ToInt64() & MouseKeyLeftButton) != 0L))
                {
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_splitDrag is null &&
                    TryGetScrollBarHit(
                        clientX,
                        clientY,
                        out _,
                        out _,
                        out _))
                {
                    Cursor = Cursors.Hand;
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_splitDrag is null &&
                    HitTestSeparator(clientX, clientY) is null &&
                    TryGetHeaderResizeHandle(
                        clientX,
                        clientY,
                        out var handle))
                {
                    Cursor = GetHeaderResizeCursor(handle.Axis);
                    message.Result = IntPtr.Zero;
                    return;
                }
                break;
            }
            case WindowMessageLeftButtonUp:
            {
                var (clientX, clientY) = GetMouseCoordinates(message.LParam);
                if (_scrollBarDrag is not null)
                {
                    UpdateScrollBarDrag(clientX, clientY);
                    EndScrollBarDrag(persist: true);
                    UpdatePointerCursor(clientX, clientY);
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_headerResize is { } releasedResize)
                {
                    ApplyHeaderResize(releasedResize, clientX, clientY);
                    _headerResize = null;
                    Capture = false;
                    UpdatePointerCursor(clientX, clientY);
                    message.Result = IntPtr.Zero;
                    return;
                }
                if (_headerReorder is not null &&
                    CompleteHeaderReorder(clientX, clientY))
                {
                    UpdatePointerCursor(clientX, clientY);
                    message.Result = IntPtr.Zero;
                    return;
                }
                break;
            }
            case WindowMessageCaptureChanged:
                if (_scrollBarDrag is not null)
                {
                    EndScrollBarDrag(persist: true);
                    Cursor = Cursors.Default;
                }
                if (_headerResize is not null)
                {
                    _headerResize = null;
                    Cursor = Cursors.Default;
                }
                if (_headerReorder is not null)
                {
                    CancelHeaderReorder();
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
