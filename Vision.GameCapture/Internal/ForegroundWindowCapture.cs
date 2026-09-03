using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Vision.GameCapture.Internal;

internal static class ForegroundWindowCapture
{
    public static Bitmap Capture(string executableName)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("No foreground window is available.");

        GetWindowThreadProcessId(hwnd, out var processId);
        using var process = Process.GetProcessById((int)processId);

        var expected = Path.GetFileNameWithoutExtension(executableName);
        if (!string.Equals(process.ProcessName, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The focused window belongs to '{process.ProcessName}.exe', not '{executableName}'.");
        }

        if (!GetClientRect(hwnd, out var clientRect))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var width = clientRect.Right - clientRect.Left;
        var height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The focused game window has no drawable client area.");

        var origin = new PointNative { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref origin))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    public static Bitmap CaptureRegion(
        string executableName,
        ScreenRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        using var frame = Capture(executableName);
        Rectangle bounds = ResolveRegionBounds(frame.Size, region);

        if (bounds.X < 0 || bounds.Y < 0 ||
            bounds.Width <= 0 || bounds.Height <= 0 ||
            bounds.Right > frame.Width || bounds.Bottom > frame.Height)
        {
            throw new InvalidOperationException(
                "The configured capture region falls outside the game client area.");
        }

        return frame.Clone(bounds, frame.PixelFormat);
    }

    private static Rectangle ResolveRegionBounds(
        Size frameSize,
        ScreenRegion region)
    {
        int x;
        int y;

        switch (region.Anchor)
        {
            case ScreenAnchor.TopLeft:
                x = region.X;
                y = region.Y;
                break;
            case ScreenAnchor.TopCenter:
                x = (frameSize.Width / 2) + region.X;
                y = region.Y;
                break;
            case ScreenAnchor.TopRight:
                x = frameSize.Width + region.X;
                y = region.Y;
                break;
            case ScreenAnchor.Center:
                x = (frameSize.Width / 2) + region.X;
                y = (frameSize.Height / 2) + region.Y;
                break;
            case ScreenAnchor.BottomLeft:
                x = region.X;
                y = frameSize.Height + region.Y;
                break;
            case ScreenAnchor.BottomCenter:
                x = (frameSize.Width / 2) + region.X;
                y = frameSize.Height + region.Y;
                break;
            case ScreenAnchor.BottomRight:
                x = frameSize.Width + region.X;
                y = frameSize.Height + region.Y;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(region.Anchor));
        }

        return new Rectangle(x, y, region.Width, region.Height);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out RectNative rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointNative point);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }
}
