using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public interface IMacroExecutionService
{
    Task ExecuteAsync(
        IEnumerable<MacroAction> actions,
        CancellationToken cancellationToken = default);

    Task ExecuteWhileForegroundAsync(
        IEnumerable<MacroAction> actions,
        string processName,
        CancellationToken cancellationToken = default);
}
