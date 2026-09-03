namespace Input.Abstractions;

public interface IKeyboardInput
{
    Task TapAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> HoldAsync(
        string key,
        int durationMilliseconds,
        Func<bool>? shouldContinue = null,
        CancellationToken cancellationToken = default);
}
