using Input.Abstractions;

namespace Input.Interception;

public sealed class InterceptionKeyboardInput : IKeyboardInput
{
    public Task TapAsync(string key, CancellationToken cancellationToken = default) =>
        InterceptionKeyboardSender.TapAsync(key, cancellationToken);

    public Task<bool> HoldAsync(
        string key,
        int durationMilliseconds,
        Func<bool>? shouldContinue = null,
        CancellationToken cancellationToken = default) =>
        InterceptionKeyboardSender.HoldAsync(key, durationMilliseconds, shouldContinue, cancellationToken);
}
