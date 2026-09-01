using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public class MacroJsonService : IMacroJsonService
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task SaveAsync(
        string filePath,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string json = JsonSerializer.Serialize(settings, _options);

        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<AppSettings> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The settings file was not found.", filePath);

        string json = await File.ReadAllTextAsync(filePath);

        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, _options)
            ?? throw new InvalidDataException("The settings file is empty or invalid.");

        if (settings.SchemaVersion != 5)
            throw new InvalidDataException(
                $"Unsupported settings schema version: {settings.SchemaVersion}.");

        settings.Target ??= new TargetSettings();
        settings.Macro ??= new MacroSettings();
        settings.Macro.Actions ??= new List<MacroAction>();
        NormalizeActions(settings.Macro.Actions);

        return settings;
    }

    private static void NormalizeActions(IEnumerable<MacroAction> actions)
    {
        foreach (MacroAction action in actions)
        {
            action.Actions ??= new List<MacroAction>();
            action.ElseActions ??= new List<MacroAction>();

            if (action.Type == MacroActionType.Press &&
                action.DurationMilliseconds is < 1 or > 60_000)
            {
                throw new InvalidDataException(
                    $"PRESS '{action.Value}' must specify durationMilliseconds between 1 and 60000.");
            }

            NormalizeActions(action.Actions);
            NormalizeActions(action.ElseActions);
        }
    }
}
