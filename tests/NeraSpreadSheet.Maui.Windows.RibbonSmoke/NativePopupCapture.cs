using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

/// <summary>Captures only a visible rectangle owned by this synthetic smoke process.</summary>
internal static class NativePopupCapture
{
    internal sealed record Snapshot(byte[] Pixels, int Width, int Height);

    internal static Snapshot Capture(Microsoft.UI.Xaml.FrameworkElement popup, nint owner)
    {
        var origin = new NativePoint();
        Check(ClientToScreen(owner, ref origin));
        var offset = popup.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point());
        var scale = popup.XamlRoot.RasterizationScale;
        var x = origin.X + (int)Math.Round(offset.X * scale);
        var y = origin.Y + (int)Math.Round(offset.Y * scale);
        var width = (int)Math.Ceiling(popup.ActualWidth * scale);
        var height = (int)Math.Ceiling(popup.ActualHeight * scale);
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidOperationException("Invalid popup capture bounds.");
        // Reject another process or an occluded popup before reading any pixels.
        foreach (var dx in new[] { 1, width / 2, width - 2 })
        foreach (var dy in new[] { 1, height / 2, height - 2 })
        {
            var window = WindowFromPoint(new NativePoint { X = x + dx, Y = y + dy });
            _ = GetWindowThreadProcessId(window, out var process);
            if (process != Environment.ProcessId) throw new InvalidOperationException("The popup capture rectangle is not owned by the smoke process.");
        }

        var screen = GetDC(0);
        Check(screen != 0);
        nint memory = 0;
        nint bitmap = 0;
        nint previous = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            Check(memory != 0);
            bitmap = CreateCompatibleBitmap(screen, width, height);
            Check(bitmap != 0);
            previous = SelectObject(memory, bitmap);
            Check(previous != 0 && previous != -1);
            Check(BitBlt(memory, 0, 0, width, height, screen, x, y, 0x40CC0020));
            _ = SelectObject(memory, previous);
            previous = 0;
            var header = new BitmapHeader { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            var pixels = new byte[checked(width * height * 4)];
            Check(GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref header, 0) == height);
            for (var index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
            return new Snapshot(pixels, width, height);
        }
        finally
        {
            if (previous != 0) _ = SelectObject(memory, previous);
            if (bitmap != 0) _ = DeleteObject(bitmap);
            if (memory != 0) _ = DeleteDC(memory);
            _ = ReleaseDC(0, screen);
        }
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { internal int X; internal int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

#pragma warning disable SYSLIB1054 // Test-only ABI; keep unsafe source generation out of this existing smoke project.
    [DllImport("user32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    [DllImport("user32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetDC(nint window);
    [DllImport("user32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetDIBits(nint dc, nint bitmap, uint first, uint count, [Out] byte[] pixels, ref BitmapHeader header, uint usage);
    [DllImport("gdi32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
#pragma warning restore SYSLIB1054
}
