using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameKeystrokes;

public static class KeyboardInput
{
    public static async Task TapAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ScanCode scanCode = Parse(key);

        Send(scanCode, keyUp: false);

        try
        {
            await Task.Delay(75, cancellationToken);
        }
        finally
        {
            Send(scanCode, keyUp: true);
        }
    }

    private static ScanCode Parse(string key)
    {
        return key.Trim().ToUpperInvariant() switch
        {
            "0" => ScanCode.Zero,
            "1" => ScanCode.One,
            "2" => ScanCode.Two,
            "3" => ScanCode.Three,
            "4" => ScanCode.Four,
            "5" => ScanCode.Five,
            "6" => ScanCode.Six,
            "7" => ScanCode.Seven,
            "8" => ScanCode.Eight,
            "9" => ScanCode.Nine,

            var name when Enum.TryParse<ScanCode>(
                name,
                true,
                out var scanCode) => scanCode,

            _ => throw new ArgumentException(
                $"Unknown key: '{key}'",
                nameof(key))
        };
    }

    private static void Send(ScanCode key, bool keyUp)
    {
        uint flags = NativeMethods.KeyEventScanCode;

        if (IsExtended(key))
            flags |= NativeMethods.KeyEventExtendedKey;

        if (keyUp)
            flags |= NativeMethods.KeyEventKeyUp;

        var input = new[]
        {
            new NativeMethods.Input
            {
                Type = NativeMethods.InputKeyboard,
                Data = new NativeMethods.InputUnion
                {
                    Keyboard = new NativeMethods.KeyboardInput
                    {
                        VirtualKey = 0,
                        ScanCode = (ushort)key,
                        Flags = flags
                    }
                }
            }
        };

        var sent = NativeMethods.SendInput(
            1,
            input,
            Marshal.SizeOf<NativeMethods.Input>());

        if (sent != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static bool IsExtended(ScanCode key) =>
        key is ScanCode.ArrowUp
            or ScanCode.ArrowLeft
            or ScanCode.ArrowRight
            or ScanCode.ArrowDown;
}
