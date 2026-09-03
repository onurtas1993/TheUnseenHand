using TheUnseenHand.Models;
using Input.Abstractions;

namespace TheUnseenHand.Services;

public interface IMacroExecutionService
{
    event EventHandler<GameStateReadEventArgs>? GameStateRead;

    void ResetIntervalHistory();

    Task ExecuteAsync(
        IEnumerable<MacroAction> actions,
        CancellationToken cancellationToken = default);

    Task ExecuteWhileForegroundAsync(
        IEnumerable<MacroAction> actions,
        string processName,
        KeyboardProvider keyboardProvider,
        CancellationToken cancellationToken = default);
}

public sealed class GameStateReadEventArgs : EventArgs
{
    public required string Source { get; init; }
    public required string ActualValue { get; init; }
    public required string Comparison { get; init; }
    public required bool Result { get; init; }
}