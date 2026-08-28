using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public interface IMacroExecutionService
{
    event EventHandler<GameStateReadEventArgs>? GameStateRead;

    Task ExecuteAsync(
        IEnumerable<MacroAction> actions,
        CancellationToken cancellationToken = default);

    Task ExecuteWhileForegroundAsync(
        IEnumerable<MacroAction> actions,
        string processName,
        CancellationToken cancellationToken = default);
}

public sealed class GameStateReadEventArgs : EventArgs
{
    public required int PlayerHP { get; init; }
    public required int PlayerMaxHP { get; init; }
    public required int PlayerMP { get; init; }
    public required int PlayerMaxMP { get; init; }
    public required string Comparison { get; init; }
    public required bool Result { get; init; }
}
