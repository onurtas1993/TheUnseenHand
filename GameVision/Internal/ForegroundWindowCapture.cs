using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GameVision.Internal;

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
