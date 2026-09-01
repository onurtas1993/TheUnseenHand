using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameVision;

public sealed class GameVisionConfig
{
    public string ExecutableName { get; set; } = string.Empty;
    public Dictionary<string, GameVisionReaderConfig> Readers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

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

        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExecutableName))
            throw new InvalidOperationException("ExecutableName is missing from GameVision configuration.");
        Readers = new Dictionary<string, GameVisionReaderConfig>(Readers, StringComparer.OrdinalIgnoreCase);
        if (Readers.Count == 0)
            throw new InvalidOperationException("At least one reader must be configured.");

        var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string readerName, GameVisionReaderConfig reader) in Readers)
        {
            if (string.IsNullOrWhiteSpace(readerName))
                throw new InvalidOperationException("Reader names cannot be empty.");
            if (reader.Region.Width <= 0 || reader.Region.Height <= 0)
                throw new InvalidOperationException($"Reader '{readerName}' has an invalid region size.");
            if (reader.Prompt.Count == 0 || reader.Prompt.All(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Reader '{readerName}' has no prompt.");
            if (reader.Outputs.Count == 0)
                throw new InvalidOperationException($"Reader '{readerName}' has no outputs.");

            reader.Outputs = new Dictionary<string, GameVisionOutputConfig>(
                reader.Outputs, StringComparer.OrdinalIgnoreCase);

            foreach (string outputName in reader.Outputs.Keys)
            {
                if (string.IsNullOrWhiteSpace(outputName))
                    throw new InvalidOperationException($"Reader '{readerName}' has an empty output name.");
                if (!outputNames.Add(outputName))
                    throw new InvalidOperationException($"Output name '{outputName}' is configured more than once.");

                GameVisionOutputConfig output = reader.Outputs[outputName];
                if (output.Minimum > output.Maximum)
                    throw new InvalidOperationException($"Output '{outputName}' has Minimum greater than Maximum.");
                if (output.MinimumDigits is <= 0 || output.MaximumDigits is <= 0 ||
                    output.MinimumDigits > output.MaximumDigits)
                    throw new InvalidOperationException($"Output '{outputName}' has invalid digit limits.");
                if ((output.MinimumDigits is not null || output.MaximumDigits is not null) &&
                    output.Type != GameVisionValueType.Integer)
                    throw new InvalidOperationException($"Output '{outputName}' can use digit limits only with Type Integer.");
                if ((output.Minimum is not null || output.Maximum is not null) &&
                    output.Type is not (GameVisionValueType.Integer or GameVisionValueType.Decimal))
                    throw new InvalidOperationException($"Output '{outputName}' can use Minimum/Maximum only with a numeric type.");
            }
        }
    }
}

public sealed class GameVisionReaderConfig
{
    public ScreenRegion Region { get; set; } = new();
    public List<string> Prompt { get; set; } = new();
    public Dictionary<string, GameVisionOutputConfig> Outputs { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GameVisionOutputConfig
{
    public GameVisionValueType Type { get; set; } = GameVisionValueType.Text;
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? MinimumDigits { get; set; }
    public int? MaximumDigits { get; set; }
}

public enum GameVisionValueType { Text, Integer, Decimal, Boolean }
