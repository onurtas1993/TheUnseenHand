using GameKeystrokes;
using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public class MacroExecutionService : IMacroExecutionService
{
    private static readonly TimeSpan FocusPollInterval = TimeSpan.FromMilliseconds(50);

    public async Task ExecuteAsync(
        IEnumerable<MacroAction> actions,
        CancellationToken cancellationToken = default)
    {
        foreach (var action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (action.Type)
            {
                case MacroActionType.Press:
                    await KeyboardInput.TapAsync(
                        action.Value,
                        cancellationToken);
                    break;

                case MacroActionType.Wait:
                    await WaitAsync(
                        action.Value,
                        cancellationToken);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported action type: {action.Type}");
            }
        }
    }

    public async Task ExecuteWhileForegroundAsync(
        IEnumerable<MacroAction> actions,
        string processName,
        CancellationToken cancellationToken = default)
    {
        MacroAction[] actionList = actions.ToArray();
        if (actionList.Length == 0)
            throw new InvalidOperationException("Add at least one action before starting.");

        WindowTarget target = WindowTarget.FromProcessName(processName);
        await target.FocusAsync(cancellationToken: cancellationToken);

        while (true)
        {
            foreach (MacroAction action in actionList)
            {
                await WaitForForegroundAsync(processName, cancellationToken);

                if (action.Type == MacroActionType.Wait)
                    await FocusAwareWaitAsync(action.Value, processName, cancellationToken);
                else
                    await ExecuteAsync(new[] { action }, cancellationToken);
            }
        }
    }

    private static async Task WaitForForegroundAsync(
        string processName,
        CancellationToken cancellationToken)
    {
        while (!WindowTarget.IsProcessForeground(processName))
            await Task.Delay(FocusPollInterval, cancellationToken);
    }

    private static async Task FocusAwareWaitAsync(
        string value,
        string processName,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(value, out int milliseconds) || milliseconds < 0)
            throw new InvalidOperationException($"Invalid wait value: '{value}'");

        int remaining = milliseconds;
        while (remaining > 0)
        {
            await WaitForForegroundAsync(processName, cancellationToken);
            int delay = Math.Min(remaining, (int)FocusPollInterval.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);

            if (WindowTarget.IsProcessForeground(processName))
                remaining -= delay;
        }
    }

    private static async Task WaitAsync(
        string value,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(value, out var milliseconds))
        {
            throw new InvalidOperationException(
                $"Invalid wait value: '{value}'");
        }

        if (milliseconds < 0)
        {
            throw new InvalidOperationException(
                "Wait duration cannot be negative.");
        }

        await Task.Delay(
            milliseconds,
            cancellationToken);
    }
}
