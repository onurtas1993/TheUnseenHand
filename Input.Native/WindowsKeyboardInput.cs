using Input.Abstractions;

namespace Input.Native;

public sealed class WindowsKeyboardInput : IKeyboardInput
{
    public Task TapAsync(string key, CancellationToken cancellationToken = default) =>
        NativeKeyboardInput.TapAsync(key, cancellationToken);

    public Task<bool> HoldAsync(
        string key,
        int durationMilliseconds,
        Func<bool>? shouldContinue = null,
        CancellationToken cancellationToken = default) =>
        NativeKeyboardInput.HoldAsync(key, durationMilliseconds, shouldContinue, cancellationToken);
}
