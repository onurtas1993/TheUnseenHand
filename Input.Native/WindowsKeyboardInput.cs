using Input.Abstractions;

namespace Input.Native;

public sealed class WindowsKeyboardInput : IKeyboardInput
{
    public Task TapAsync(string key, CancellationToken cancellationToken = default) =>
        KeyboardInput.TapAsync(key, cancellationToken);

    public Task<bool> HoldAsync(
        string key,
        int durationMilliseconds,
        Func<bool>? shouldContinue = null,
        CancellationToken cancellationToken = default) =>
        KeyboardInput.HoldAsync(key, durationMilliseconds, shouldContinue, cancellationToken);
}