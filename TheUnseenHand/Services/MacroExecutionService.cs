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
        await ExecuteSequenceAsync(actions, null, new VisionReadCache(), cancellationToken);
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
            var visionCache = new VisionReadCache();
            foreach (MacroAction action in actionList)
            {
                await WaitForForegroundAsync(processName, cancellationToken);
                await ExecuteActionAsync(action, processName, visionCache, cancellationToken);
            }
        }
    }

    private async Task<bool> ExecuteSequenceAsync(
        IEnumerable<MacroAction> actions,
        string? processName,
        VisionReadCache visionCache,
        CancellationToken cancellationToken)
    {
        foreach (MacroAction action in actions)
        {
            if (!await ExecuteActionAsync(
                    action,
                    processName,
                    visionCache,
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
        VisionReadCache visionCache,
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
                    visionCache,
                    cancellationToken);

            default:
                throw new NotSupportedException($"Unsupported action type: {action.Type}");
        }
    }

    private async Task<bool> ExecuteIfAsync(
        MacroAction action,
        string? processName,
        VisionReadCache visionCache,
        CancellationToken cancellationToken)
    {
        MacroCondition condition = action.Condition
            ?? throw new InvalidOperationException("The IF action has no condition.");

        if (action.Actions.Count == 0 && action.ElseActions.Count == 0)
            return true;

        try
        {
            GameVisionValue? value = await ReadCachedValueAsync(
                condition.Source, visionCache, cancellationToken);
            if (value is null)
                return true;

            bool conditionResult = Compare(value, condition.Operator, condition.Value);
            GameStateRead?.Invoke(this, new GameStateReadEventArgs
            {
                Source = value.Name,
                ActualValue = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                Comparison = $"{value.Name} {value.Value} {FormatOperator(condition.Operator)} {condition.Value}",
                Result = conditionResult
            });

            return await ExecuteSequenceAsync(
                conditionResult
                    ? action.Actions
                    : action.ElseActions,
                processName,
                visionCache,
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

    private async Task<GameVisionValue?> ReadCachedValueAsync(
        string outputName,
        VisionReadCache cache,
        CancellationToken cancellationToken)
    {
        string readerName = _vision.Value.GetReaderNameForOutput(outputName);
        if (!cache.Results.TryGetValue(readerName, out GameVisionResult? result))
        {
            result = await _vision.Value.ReadAsync(readerName, cancellationToken);
            cache.Results.Add(readerName, result);
        }

        return result.Values.GetValueOrDefault(outputName);
    }

    private static bool Compare(GameVisionValue value, ComparisonOperator comparison, string expected)
    {
        if (value.Type == GameVisionValueType.Text)
        {
            if (comparison is not (ComparisonOperator.Equals or ComparisonOperator.NotEquals))
                throw new InvalidOperationException($"Text output '{value.Name}' only supports Equals and NotEquals.");
            bool equals = string.Equals(NormalizeText(value.GetText()), NormalizeText(expected),
                StringComparison.OrdinalIgnoreCase);
            return comparison == ComparisonOperator.Equals ? equals : !equals;
        }

        if (value.Type == GameVisionValueType.Boolean)
        {
            if (comparison is not (ComparisonOperator.Equals or ComparisonOperator.NotEquals) ||
                !bool.TryParse(expected, out bool expectedBoolean))
                throw new InvalidOperationException($"Boolean output '{value.Name}' requires Equals/NotEquals and true or false.");
            bool equals = value.GetBoolean() == expectedBoolean;
            return comparison == ComparisonOperator.Equals ? equals : !equals;
        }

        if (!decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal expectedNumber))
            throw new InvalidOperationException($"Invalid numeric IF value: '{expected}'.");
        return CompareNumber(value.GetDecimal(), comparison, expectedNumber);
    }

    private static bool CompareNumber(
        decimal actual,
        ComparisonOperator comparison,
        decimal expected) => comparison switch
    {
        ComparisonOperator.Equals => actual == expected,
        ComparisonOperator.NotEquals => actual != expected,
        ComparisonOperator.LessThan => actual < expected,
        ComparisonOperator.LessThanOrEqual => actual <= expected,
        ComparisonOperator.GreaterThan => actual > expected,
        ComparisonOperator.GreaterThanOrEqual => actual >= expected,
        _ => false
    };

    private static string NormalizeText(string value)
    {
        return string.Join(
            ' ',
            value.Trim().Trim('"', '\'', '`')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

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

    private sealed class VisionReadCache
    {
        public Dictionary<string, GameVisionResult> Results { get; } =
            new(StringComparer.OrdinalIgnoreCase);
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
