using System.Text.Json;

namespace LocalAIAdapter;

public sealed class LocalAIConfig
{
    public string BaseUrl { get; set; } = "http://localhost:1234/v1";
    public string Model { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public int MaxTokens { get; set; } = 128;
    public double Temperature { get; set; } = 0.0;
    public int TimeoutSeconds { get; set; } = 60;

    public static LocalAIConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Local AI configuration file was not found: {path}");

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<LocalAIConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null)
            throw new InvalidOperationException("Could not read LocalAIAdapter configuration.");

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
            throw new InvalidOperationException("BaseUrl is missing from localai.json.");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("Model is missing from localai.json.");

        if (config.MaxTokens <= 0)
            throw new InvalidOperationException("MaxTokens must be greater than zero.");

        if (config.TimeoutSeconds <= 0)
            throw new InvalidOperationException("TimeoutSeconds must be greater than zero.");

        return config;
    }
}
