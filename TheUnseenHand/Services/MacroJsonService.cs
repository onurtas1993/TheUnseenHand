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

        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            List<MacroAction> legacyActions =
                JsonSerializer.Deserialize<List<MacroAction>>(json, _options) ?? new();

            return new AppSettings
            {
                Macro = new MacroSettings { Actions = legacyActions }
            };
        }

        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, _options)
            ?? throw new InvalidDataException("The settings file is empty or invalid.");

        if (settings.SchemaVersion != 1)
            throw new InvalidDataException(
                $"Unsupported settings schema version: {settings.SchemaVersion}.");

        settings.Target ??= new TargetSettings();
        settings.Macro ??= new MacroSettings();
        settings.Macro.Actions ??= new List<MacroAction>();

        return settings;
    }
}
