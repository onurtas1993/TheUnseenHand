using System.Globalization;
using System.IO;
using GameKeystrokes;
using GameVision;
using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public class MacroExecutionService : IMacroExecutionService
{
    private static readonly TimeSpan FocusPollInterval = TimeSpan.FromMilliseconds(50);
    private readonly Lazy<GameVisionReader> _vision;

    public event EventHandler<GameStateReadEventArgs>? GameStateRead;

    public MacroExecutionService(GameVisionReader? vision = null)
    {
        _vision = new Lazy<GameVisionReader>(() => vision ?? new GameVisionReader(
            Path.Combine(AppContext.BaseDirectory, "gamevision.json"),
            Path.Combine(AppContext.BaseDirectory, "localai.json")));
    }

    public async Task ExecuteAsync(
        IEnumerable<MacroAction> actions,
        CancellationToken cancellationToken = default)
    {
        await ExecuteSequenceAsync(actions, null, cancellationToken);
    }

    public async Task ExecuteWhileForegroundAsync(
        IEnumerable<MacroAction> actions,
        string processName,
        CancellationToken cancellationToken = default)
    {
        MacroAction[] actionList = actions.ToArray();
        if (actionList.Length == 0)
            throw new InvalidOperationException("Add at least one action before starting.");

        if (ContainsVisionCondition(actionList))
            await _vision.Value.EnsureAvailableAsync(cancellationToken);

        WindowTarget target = WindowTarget.FromProcessName(processName);
        await target.FocusAsync(cancellationToken: cancellationToken);

        while (true)
        {
            foreach (MacroAction action in actionList)
            {
                await WaitForForegroundAsync(processName, cancellationToken);
                await ExecuteActionAsync(action, processName, cancellationToken);
            }
        }
    }

    private async Task<bool> ExecuteSequenceAsync(
        IEnumerable<MacroAction> actions,
        string? processName,
        CancellationToken cancellationToken)
    {
        foreach (MacroAction action in actions)
        {
            if (!await ExecuteActionAsync(
                    action,
                    processName,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ExecuteActionAsync(
        MacroAction action,
        string? processName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (processName is not null)
            await WaitForForegroundAsync(processName, cancellationToken);

        switch (action.Type)
        {
            case MacroActionType.Press:
                if (action.DurationMilliseconds is < 1 or > 60_000)
                {
                    throw new InvalidOperationException(
                        "Key hold duration must be between 1 and 60000 ms.");
                }

                return await KeyboardInput.HoldAsync(
                    action.Value,
                    action.DurationMilliseconds,
                    processName is null
                        ? null
                        : () => WindowTarget.IsProcessForeground(processName),
                    cancellationToken);

            case MacroActionType.Wait:
                if (processName is null)
                    await WaitAsync(action.Value, cancellationToken);
                else
                    await FocusAwareWaitAsync(action.Value, processName, cancellationToken);
                return true;

            case MacroActionType.If:
                return await ExecuteIfAsync(
                    action,
                    processName,
                    cancellationToken);

            default:
                throw new NotSupportedException($"Unsupported action type: {action.Type}");
        }
    }

    private async Task<bool> ExecuteIfAsync(
        MacroAction action,
        string? processName,
        CancellationToken cancellationToken)
    {
        MacroCondition condition = action.Condition
            ?? throw new InvalidOperationException("The IF action has no condition.");

        if (action.Actions.Count == 0 && action.ElseActions.Count == 0)
            return true;

        try
        {
            if (condition.Source == ConditionSource.CurrentMob)
            {
                string recognizedName =
                    await _vision.Value.ReadMobNameAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(recognizedName))
                    return true;

                return await ExecuteSequenceAsync(
                    CompareTextList(recognizedName, condition.Operator, condition.Values)
                        ? action.Actions
                        : action.ElseActions,
                    processName,
                    cancellationToken);
            }

            GameVitals state = await _vision.Value.ReadVitalsAsync(cancellationToken);
            double actual = GetNumericValue(state, condition.Source);

            if (!double.TryParse(
                    condition.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double expected))
            {
                throw new InvalidOperationException(
                    $"Invalid numeric IF value: '{condition.Value}'.");
            }

            bool conditionResult = CompareNumber(actual, condition.Operator, expected);
            GameStateRead?.Invoke(this, new GameStateReadEventArgs
            {
                PlayerHP = state.PlayerHP,
                PlayerMaxHP = state.PlayerMaxHP,
                PlayerMP = state.PlayerMP,
                PlayerMaxMP = state.PlayerMaxMP,
                Comparison = $"{FormatSource(condition.Source)} {actual:0.##} " +
                             $"{FormatOperator(condition.Operator)} {expected:0.##}",
                Result = conditionResult
            });

            return await ExecuteSequenceAsync(
                conditionResult
                    ? action.Actions
                    : action.ElseActions,
                processName,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            // A single image can be blurred, partially updated, or misread.
            // Skip this IF without selecting THEN or ELSE; the next macro loop
            // will capture and evaluate a fresh image.
            return true;
        }
    }

    private static double GetNumericValue(GameVitals state, ConditionSource source) => source switch
    {
        ConditionSource.PlayerHP => state.PlayerHP,
        ConditionSource.PlayerMaxHP => state.PlayerMaxHP,
        ConditionSource.PlayerHPPercent => state.PlayerHPPercent,
        ConditionSource.PlayerMP => state.PlayerMP,
        ConditionSource.PlayerMaxMP => state.PlayerMaxMP,
        ConditionSource.PlayerMPPercent => state.PlayerMPPercent,
        _ => throw new InvalidOperationException($"'{source}' is not numeric.")
    };

    private static bool CompareNumber(
        double actual,
        ComparisonOperator comparison,
        double expected) => comparison switch
    {
        ComparisonOperator.Equals => actual == expected,
        ComparisonOperator.NotEquals => actual != expected,
        ComparisonOperator.LessThan => actual < expected,
        ComparisonOperator.LessThanOrEqual => actual <= expected,
        ComparisonOperator.GreaterThan => actual > expected,
        ComparisonOperator.GreaterThanOrEqual => actual >= expected,
        _ => false
    };

    private static bool CompareTextList(
        string actual,
        ComparisonOperator comparison,
        IEnumerable<string> expectedValues)
    {
        string normalizedActual = NormalizeText(actual);
        bool contains = expectedValues.Any(expected => string.Equals(
            normalizedActual,
            NormalizeText(expected),
            StringComparison.OrdinalIgnoreCase));

        return comparison switch
        {
            ComparisonOperator.Equals => contains,
            ComparisonOperator.NotEquals => !contains,
            _ => false
        };
    }

    private static string NormalizeText(string value)
    {
        return string.Join(
            ' ',
            value.Trim().Trim('"', '\'', '`')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FormatSource(ConditionSource source) => source switch
    {
        ConditionSource.PlayerHP => "HP",
        ConditionSource.PlayerMaxHP => "MAX HP",
        ConditionSource.PlayerHPPercent => "HP %",
        ConditionSource.PlayerMP => "MP",
        ConditionSource.PlayerMaxMP => "MAX MP",
        ConditionSource.PlayerMPPercent => "MP %",
        _ => source.ToString()
    };

    private static string FormatOperator(ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.Equals => "=",
        ComparisonOperator.NotEquals => "!=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        _ => comparison.ToString()
    };

    private static bool ContainsVisionCondition(IEnumerable<MacroAction> actions)
    {
        return actions.Any(action =>
            action.Type == MacroActionType.If &&
            (action.Condition is not null ||
             ContainsVisionCondition(action.Actions) ||
             ContainsVisionCondition(action.ElseActions)));
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
        if (!int.TryParse(value, out int milliseconds) || milliseconds < 0)
            throw new InvalidOperationException($"Invalid wait value: '{value}'");

        await Task.Delay(milliseconds, cancellationToken);
    }
}
