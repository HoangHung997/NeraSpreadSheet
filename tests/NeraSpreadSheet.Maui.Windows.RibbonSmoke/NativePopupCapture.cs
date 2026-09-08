using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml.Automation.Peers;
using NativeRect = global::Windows.Foundation.Rect;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

/// <summary>Captures only a visible rectangle owned by this synthetic smoke process.</summary>
internal static class NativePopupCapture
{
    internal sealed record Snapshot(byte[] Pixels, int Width, int Height);

    private readonly record struct ReferenceBounds(NativeRect Screen, NativeRect Local);

    internal static Snapshot Capture(Microsoft.UI.Xaml.FrameworkElement popup, nint owner,
        Microsoft.UI.Xaml.Controls.ComboBox picker, IReadOnlyList<Microsoft.UI.Xaml.Controls.ComboBoxItem?> items)
    {
        Require(picker.IsDropDownOpen && popup.IsLoaded && items.Count >= 2, "The actual Picker must remain open and realized.");
        var references = ReadReferences(popup, items);
        var scale = popup.XamlRoot.RasterizationScale;
        // Capture bounded numeric evidence before validation: an automation peer
        // may report clipped bounds, unlike the complete local layout rectangle.
        Console.WriteLine("NERA_PICKER_CAPTURE_REFERENCES:" + JsonSerializer.Serialize(new
        {
            schema = "native-picker-references-v1", scale = FiniteNumber(scale),
            popupWidth = FiniteNumber(popup.ActualWidth), popupHeight = FiniteNumber(popup.ActualHeight),
            count = references.Length, clipped = references.Length > 16,
            items = references.Take(16).Select((reference, index) => new
            {
                index, screenX = FiniteNumber(reference.Screen.X), screenY = FiniteNumber(reference.Screen.Y),
                screenWidth = FiniteNumber(reference.Screen.Width), screenHeight = FiniteNumber(reference.Screen.Height),
                localX = FiniteNumber(reference.Local.X), localY = FiniteNumber(reference.Local.Y),
                localWidth = FiniteNumber(reference.Local.Width), localHeight = FiniteNumber(reference.Local.Height),
            }),
        }));
        var bounds = ResolveBounds(references, scale, popup.ActualWidth, popup.ActualHeight);
        // Popup and owner can have different coordinate roots. The peer contract
        // supplies screen coordinates; two or more items must agree on the origin.
        var origin = new NativePoint();
        Check(ClientToScreen(owner, ref origin));
        var offset = popup.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point());
        var x = checked((int)Math.Round(bounds.X));
        var y = checked((int)Math.Round(bounds.Y));
        var width = checked((int)Math.Ceiling(bounds.Width));
        var height = checked((int)Math.Ceiling(bounds.Height));
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidOperationException("Invalid popup capture bounds.");
        nint referenceWindow = 0;
        foreach (var reference in references)
        {
            var point = new NativePoint { X = checked((int)Math.Round(reference.Screen.X + reference.Screen.Width / 2)),
                Y = checked((int)Math.Round(reference.Screen.Y + reference.Screen.Height / 2)) };
            var window = WindowFromPoint(point);
            _ = GetWindowThreadProcessId(window, out var process);
            Require(window != 0 && process == Environment.ProcessId && IsWindowVisible(window),
                "A Picker item is not physically visible in the smoke process.");
            Check(GetWindowRect(window, out var windowBounds));
            Require(point.X >= windowBounds.Left && point.X < windowBounds.Right &&
                point.Y >= windowBounds.Top && point.Y < windowBounds.Bottom,
                "An item peer does not map inside its actual hit window.");
            Require(referenceWindow == 0 || referenceWindow == window, "Picker items map to different native windows.");
            referenceWindow = window;
        }
        Console.WriteLine("NERA_PICKER_CAPTURE_GEOMETRY:" + JsonSerializer.Serialize(new
        {
            schema = "native-picker-capture-v1", references = references.Length, scale,
            ownerDerivedX = origin.X + offset.X * scale, ownerDerivedY = origin.Y + offset.Y * scale,
            screenX = bounds.X, screenY = bounds.Y, width, height, referenceWindowIsOwner = referenceWindow == owner,
        }));
        // Reject other-process coverage. Same-process occlusion still requires
        // the caller's independent per-caption and palette pixel assertions.
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
            Require(picker.IsDropDownOpen && popup.IsLoaded, "Picker closed during capture.");
            var after = ResolveBounds(ReadReferences(popup, items), popup.XamlRoot.RasterizationScale,
                popup.ActualWidth, popup.ActualHeight);
            Require(Near(bounds.X, after.X) && Near(bounds.Y, after.Y) && Near(bounds.Width, after.Width) &&
                Near(bounds.Height, after.Height), "Picker screen geometry changed during capture.");
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

    private static ReferenceBounds[] ReadReferences(Microsoft.UI.Xaml.FrameworkElement popup,
        IReadOnlyList<Microsoft.UI.Xaml.Controls.ComboBoxItem?> items)
    {
        return items.Select(item =>
        {
            Require(item is { IsLoaded: true, ActualWidth: > 0d, ActualHeight: > 0d }, "Picker item lost its native layout.");
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(item!);
            Require(peer is not null && !peer.IsOffscreen(), "Picker item has no visible automation peer.");
            var local = item!.TransformToVisual(popup).TransformBounds(new NativeRect(0, 0, item.ActualWidth, item.ActualHeight));
            return new ReferenceBounds(peer!.GetBoundingRectangle(), local);
        }).ToArray();
    }

    private static NativeRect ResolveBounds(IReadOnlyList<ReferenceBounds> references, double scale, double width, double height)
    {
        Require(references.Count >= 2 && double.IsFinite(scale) && scale > 0 &&
            double.IsFinite(width) && width > 0 && double.IsFinite(height) && height > 0,
            "Popup requires multiple references and finite positive dimensions.");
        NativeRect? result = null;
        foreach (var reference in references)
        {
            var screen = reference.Screen;
            var local = reference.Local;
            Require(FiniteRect(screen) && FiniteRect(local) && Near(screen.Width, local.Width * scale) &&
                Near(screen.Height, local.Height * scale), "Item screen bounds do not verify the popup DPI scale.");
            Require(local.X >= -2 && local.Y >= -2 && local.Right <= width + 2 && local.Bottom <= height + 2,
                "A Picker reference is outside the popup layout.");
            var candidate = new NativeRect(screen.X - local.X * scale, screen.Y - local.Y * scale, width * scale, height * scale);
            Require(FiniteRect(candidate) && (result is not { } first || Near(first.X, candidate.X) && Near(first.Y, candidate.Y)),
                "Item peers disagree about the popup screen origin.");
            result ??= candidate;
        }
        return result!.Value;
    }

    private static bool FiniteRect(NativeRect value) => double.IsFinite(value.X) && double.IsFinite(value.Y) &&
        double.IsFinite(value.Width) && double.IsFinite(value.Height) && value.Width > 0 && value.Height > 0;

    private static bool Near(double left, double right) => Math.Abs(left - right) <= 2d;

    private static double? FiniteNumber(double value) => double.IsFinite(value) ? value : null;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void VerifyGeometryContract()
    {
        foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
        {
            ReferenceBounds[] references = [new(new NativeRect(-120 + 10 * scale, 80 + 8 * scale, 100 * scale, 20 * scale),
                new NativeRect(10, 8, 100, 20)), new(new NativeRect(-120 + 10 * scale, 80 + 38 * scale, 100 * scale, 20 * scale),
                new NativeRect(10, 38, 100, 20))];
            var result = ResolveBounds(references, scale, 140, 80);
            Require(result.X == -120 && result.Y == 80 && result.Width == 140 * scale && result.Height == 80 * scale,
                "Synthetic popup screen-origin/DPI regression.");
            void Rejected(IReadOnlyList<ReferenceBounds> values, double dpi)
            {
                try { ResolveBounds(values, dpi, 140, 80); }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("Invalid popup geometry was accepted.");
            }
            Rejected([references[0]], scale);
            Rejected(references, double.NaN);
            Rejected(references, scale * 2);
            Rejected([references[0], references[1] with { Screen = new NativeRect(500, 500, 100 * scale, 20 * scale) }], scale);
            Rejected([references[0], references[1] with { Local = new NativeRect(10, 90, 100, 20) }], scale);
        }
        Console.WriteLine("Native Picker screen geometry self-checks passed; actual peer/pixel checks remain required.");
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { internal int X; internal int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBounds { internal int Left; internal int Top; internal int Right; internal int Bottom; }
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
    [DllImport("user32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeBounds bounds);
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
