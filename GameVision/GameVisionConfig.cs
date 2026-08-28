using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameVision;

public sealed class GameVisionConfig
{
    public string ExecutableName { get; set; } = string.Empty;

    public ScreenRegion VitalsRegion { get; set; } = new();

    public ScreenRegion MobNameRegion { get; set; } = new();

    public static GameVisionConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"GameVision configuration file was not found: {path}");
        }

        var json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        var config =
            JsonSerializer.Deserialize<GameVisionConfig>(
                json,
                options);

        if (config is null)
        {
            throw new InvalidOperationException(
                "Could not read GameVision configuration.");
        }

        if (string.IsNullOrWhiteSpace(config.ExecutableName))
        {
            throw new InvalidOperationException(
                "ExecutableName is missing from GameVision configuration.");
        }

        return config;
    }
}
