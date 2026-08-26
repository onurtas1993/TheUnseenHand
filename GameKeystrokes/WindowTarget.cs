using System.Diagnostics;

namespace GameKeystrokes;

/// <summary>
/// Locates and focuses a top-level window belonging to a process.
/// </summary>
public sealed class WindowTarget
{
    public IntPtr Handle { get; }

    private WindowTarget(IntPtr handle)
    {
        Handle = handle;
    }

    public static WindowTarget FromProcessName(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        string normalizedName = Path.GetFileNameWithoutExtension(processName);

        Process? process = Process
            .GetProcessesByName(normalizedName)
            .FirstOrDefault(candidate => candidate.MainWindowHandle != IntPtr.Zero);

        if (process is null)
            throw new InvalidOperationException(
                $"No top-level window was found for process '{normalizedName}'.");

        return new WindowTarget(process.MainWindowHandle);
    }

    public bool Focus()
    {
        NativeMethods.ShowWindow(Handle, NativeMethods.ShowRestore);
        return NativeMethods.SetForegroundWindow(Handle);
    }

    public bool IsForeground => NativeMethods.GetForegroundWindow() == Handle;

    public static bool IsProcessForeground(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0)
            return false;

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string normalizedName = Path.GetFileNameWithoutExtension(processName);
            return string.Equals(process.ProcessName, normalizedName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async Task FocusAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        Focus();

        TimeSpan maximumWait = timeout ?? TimeSpan.FromSeconds(1);
        var timer = Stopwatch.StartNew();

        while (!IsForeground && timer.Elapsed < maximumWait)
            await Task.Delay(25, cancellationToken);

        if (!IsForeground)
            throw new InvalidOperationException(
                "Windows did not allow the target window to become foreground.");
    }
}
