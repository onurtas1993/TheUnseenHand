using System.Text.Json;
using System.Text.Json.Serialization;

namespace Input.Abstractions;

public sealed class InputSettings
{
    public int SchemaVersion { get; set; } = 1;
    public KeyboardProvider KeyboardProvider { get; set; } = KeyboardProvider.Windows;

    public static InputSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The input configuration file was not found.", path);

        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        InputSettings settings = JsonSerializer.Deserialize<InputSettings>(json, options)
            ?? throw new InvalidDataException("The input configuration file is empty or invalid.");

        if (settings.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported input configuration schema version: {settings.SchemaVersion}.");
        }

        return settings;
    }
}
