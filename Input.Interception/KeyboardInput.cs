using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Input.Interception;

/// <summary>
/// Sends scan-code input through the Interception keyboard filter driver.
/// </summary>
public static class KeyboardInput
{
    private const ushort KeyUp = 0x01;
    private const ushort KeyE0 = 0x02;
    private static readonly object SyncRoot = new();
    private static readonly Lazy<InterceptionContextHandle> Context = new(CreateContext);
    private static readonly Lazy<int> KeyboardDevice = new(FindKeyboardDevice);

    public static async Task TapAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await HoldAsync(key, 75, null, cancellationToken);
    }

    public static async Task<bool> HoldAsync(
        string key,
        int durationMilliseconds,
        Func<bool>? shouldContinue = null,
        CancellationToken cancellationToken = default)
    {
        if (durationMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMilliseconds),
                "Key duration must be greater than zero.");
        }

        KeyDefinition definition = Parse(key);
        Send(definition, keyUp: false);

        try
        {
            int remaining = durationMilliseconds;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (shouldContinue is not null && !shouldContinue())
                    return false;

                int delay = Math.Min(remaining, 25);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                remaining -= delay;
            }

            return true;
        }
        finally
        {
            Send(definition, keyUp: true);
        }
    }

    private static void Send(KeyDefinition key, bool keyUp)
    {
        var stroke = new KeyStroke
        {
            Code = key.ScanCode,
            State = (ushort)((key.Extended ? KeyE0 : 0) | (keyUp ? KeyUp : 0))
        };

        lock (SyncRoot)
        {
            int sent;
            try
            {
                sent = NativeMethods.Send(Context.Value, KeyboardDevice.Value, ref stroke, 1);
            }
            catch (DllNotFoundException exception)
            {
                throw MissingLibrary(exception);
            }

            if (sent != 1)
            {
                throw new InvalidOperationException(
                    "The Interception driver did not accept the keyboard input. Verify that " +
                    "the driver is installed, reboot Windows, and reconnect the keyboard if necessary.");
            }
        }
    }

    private static InterceptionContextHandle CreateContext()
    {
        try
        {
            InterceptionContextHandle context = NativeMethods.CreateContext();
            if (context.IsInvalid)
            {
                context.Dispose();
                throw new InvalidOperationException(
                    "The Interception context could not be created. Install the Interception " +
                    "driver as administrator and reboot Windows.");
            }

            return context;
        }
        catch (DllNotFoundException exception)
        {
            throw MissingLibrary(exception);
        }
    }

    private static int FindKeyboardDevice()
    {
        var hardwareId = new byte[1_000];
        for (int device = 1; device <= 10; device++)
        {
            Array.Clear(hardwareId);
            if (NativeMethods.GetHardwareId(
                    Context.Value,
                    device,
                    hardwareId,
                    (uint)hardwareId.Length) > 0)
            {
                return device;
            }
        }

        throw new InvalidOperationException(
            "No keyboard was found through the Interception driver. Verify that the driver is " +
            "installed, reboot Windows, and press a key on the physical keyboard before retrying.");
    }

    private static InvalidOperationException MissingLibrary(Exception innerException) => new(
        "interception.dll was not found beside the application. Rebuild or republish the " +
        "application so its native Interception dependency is copied to the output folder.",
        innerException);

    private static KeyDefinition Parse(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return key.Trim().ToUpperInvariant() switch
        {
            "0" => new(0x0B), "1" => new(0x02), "2" => new(0x03),
            "3" => new(0x04), "4" => new(0x05), "5" => new(0x06),
            "6" => new(0x07), "7" => new(0x08), "8" => new(0x09),
            "9" => new(0x0A),
            "Q" => new(0x10), "W" => new(0x11), "E" => new(0x12),
            "R" => new(0x13), "T" => new(0x14), "Y" => new(0x15),
            "U" => new(0x16), "I" => new(0x17), "O" => new(0x18),
            "P" => new(0x19),
            "A" => new(0x1E), "S" => new(0x1F), "D" => new(0x20),
            "F" => new(0x21), "G" => new(0x22), "H" => new(0x23),
            "J" => new(0x24), "K" => new(0x25), "L" => new(0x26),
            "Z" => new(0x2C), "X" => new(0x2D), "C" => new(0x2E),
            "V" => new(0x2F), "B" => new(0x30), "N" => new(0x31),
            "M" => new(0x32),
            "ESC" or "ESCAPE" => new(0x01),
            "ENTER" or "RETURN" => new(0x1C),
            "LEFTCONTROL" or "LEFTCTRL" or "LCTRL" => new(0x1D),
            "LEFTSHIFT" or "LSHIFT" => new(0x2A),
            "RIGHTSHIFT" or "RSHIFT" => new(0x36),
            "LEFTALT" or "LALT" => new(0x38),
            "SPACE" or "SPACEBAR" => new(0x39),
            "F1" => new(0x3B), "F2" => new(0x3C), "F3" => new(0x3D),
            "F4" => new(0x3E), "F5" => new(0x3F), "F6" => new(0x40),
            "F7" => new(0x41), "F8" => new(0x42), "F9" => new(0x43),
            "F10" => new(0x44), "F11" => new(0x57), "F12" => new(0x58),
            "ARROWUP" or "UP" => new(0x48, true),
            "ARROWLEFT" or "LEFT" => new(0x4B, true),
            "ARROWRIGHT" or "RIGHT" => new(0x4D, true),
            "ARROWDOWN" or "DOWN" => new(0x50, true),
            _ => throw new ArgumentException($"Unknown key: '{key}'", nameof(key))
        };
    }

    private readonly record struct KeyDefinition(ushort ScanCode, bool Extended = false);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    private sealed class InterceptionContextHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private InterceptionContextHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.DestroyContext(handle);
            return true;
        }
    }

    private static class NativeMethods
    {
        private const string LibraryName = "interception.dll";

        [DllImport(LibraryName, EntryPoint = "interception_create_context",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern InterceptionContextHandle CreateContext();

        [DllImport(LibraryName, EntryPoint = "interception_destroy_context",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void DestroyContext(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "interception_send", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Send(
            InterceptionContextHandle context,
            int device,
            ref KeyStroke stroke,
            uint strokeCount);

        [DllImport(LibraryName, EntryPoint = "interception_get_hardware_id",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetHardwareId(
            InterceptionContextHandle context,
            int device,
            [Out] byte[] hardwareIdBuffer,
            uint bufferSize);
    }
}