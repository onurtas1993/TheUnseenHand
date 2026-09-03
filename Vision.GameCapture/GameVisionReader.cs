using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vision.GameCapture.Internal;
using Vision.Inference;

namespace Vision.GameCapture;

public sealed class GameVisionReader : IDisposable
{
    private readonly GameVisionConfig _config;
    private readonly LocalAIConfig _localAiConfig;
    private readonly LocalAIClient _localAi;

    public GameVisionReader(string configPath = "gamevision.json", string localAiConfigPath = "localai.json")
    {
        _config = GameVisionConfig.Load(configPath);
        _localAiConfig = LocalAIConfig.Load(localAiConfigPath);
        _localAi = new LocalAIClient(_localAiConfig);
    }

    public async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        try { await _localAi.EnsureModelAvailableAsync(cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw CreateUnavailableException(); }
        catch (HttpRequestException exception) { throw CreateUnavailableException(exception); }
    }

    public async Task<GameVisionResult> ReadAsync(string readerName, CancellationToken cancellationToken = default)
    {
        if (!_config.Readers.TryGetValue(readerName, out GameVisionReaderConfig? reader))
            throw new KeyNotFoundException($"GameVision reader '{readerName}' is not configured.");

        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        byte[] imageBytes = CapturePng(_config.ExecutableName, reader.Region);
        string response = await AnalyzeImageAsync(imageBytes, BuildPrompt(reader), cancellationToken,
            BuildResponseFormat(readerName, reader));
        return ParseResult(readerName, reader, capturedAt, response);
    }

    public async Task<GameVisionValue?> ReadValueAsync(string outputName, CancellationToken cancellationToken = default)
    {
        string readerName = FindReaderName(outputName);
        GameVisionResult result = await ReadAsync(readerName, cancellationToken);
        return result.Values.GetValueOrDefault(outputName);
    }

    public string GetReaderNameForOutput(string outputName) => FindReaderName(outputName);

    public async Task<GameVisionSnapshot> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, GameVisionValue>(StringComparer.OrdinalIgnoreCase);
        var failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string readerName in _config.Readers.Keys)
        {
            GameVisionResult result = await ReadAsync(readerName, cancellationToken);
            foreach ((string name, GameVisionValue value) in result.Values) values.Add(name, value);
            foreach ((string name, string failure) in result.Failures) failures.Add(name, failure);
        }
        return new GameVisionSnapshot { Values = values, Failures = failures };
    }

    public Bitmap CaptureReader(string readerName)
    {
        if (!_config.Readers.TryGetValue(readerName, out GameVisionReaderConfig? reader))
            throw new KeyNotFoundException($"GameVision reader '{readerName}' is not configured.");
        return CaptureRegion(_config.ExecutableName, reader.Region);
    }

    public Bitmap CaptureRegion(string executableName, ScreenRegion region) =>
        ForegroundWindowCapture.CaptureRegion(executableName, region);

    private string FindReaderName(string outputName)
    {
        if (string.IsNullOrWhiteSpace(outputName))
            throw new ArgumentException("Output name cannot be empty.", nameof(outputName));
        foreach ((string readerName, GameVisionReaderConfig reader) in _config.Readers)
            if (reader.Outputs.ContainsKey(outputName)) return readerName;
        throw new KeyNotFoundException($"GameVision output '{outputName}' is not configured.");
    }

    private static string BuildPrompt(GameVisionReaderConfig reader)
    {
        string outputs = string.Join(", ", reader.Outputs.Select(pair => $"\"{pair.Key}\" ({pair.Value.Type})"));
        string prompt = string.Join(Environment.NewLine,
            reader.Prompt.Where(line => !string.IsNullOrWhiteSpace(line))) +
            $"\nReturn one strict JSON object with these properties: {outputs}.";
        if (!RequiresNumericEvidence(reader))
            return prompt + "\nUse null for every unreadable value. Do not guess. No explanation or markdown.";

        return prompt +
            "\nFor each property, return an object containing readable and value." +
            "\nSet readable to true only when the complete value is clearly visible in the image." +
            "\nIf a value is absent, obscured, cropped, ambiguous, or unreadable, set readable to false and value to null." +
            "\nNever infer a value from the property name, surrounding UI, expected game mechanics, or prior knowledge." +
            "\nDo not guess. No explanation or markdown.";
    }

    private static object BuildResponseFormat(string readerName, GameVisionReaderConfig reader)
    {
        if (!RequiresNumericEvidence(reader))
        {
            var directProperties = reader.Outputs.ToDictionary(
                pair => pair.Key,
                pair => (object)new { type = new[] { JsonType(pair.Value.Type), "null" } });
            return CreateResponseFormat(readerName, new
            {
                type = "object",
                properties = directProperties,
                required = reader.Outputs.Keys.ToArray(),
                additionalProperties = false
            });
        }

        var properties = reader.Outputs.ToDictionary(
            pair => pair.Key,
            pair => (object)new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["readable"] = new { type = "boolean" },
                    ["value"] = new { type = new[] { JsonType(pair.Value.Type), "null" } }
                },
                required = new[] { "readable", "value" },
                additionalProperties = false
            });
        return CreateResponseFormat(readerName, new
        {
            type = "object",
            properties,
            required = reader.Outputs.Keys.ToArray(),
            additionalProperties = false
        });
    }

    private static object CreateResponseFormat(string readerName, object schema) => new
    {
        type = "json_schema",
        json_schema = new
        {
            name = Regex.Replace(readerName.ToLowerInvariant(), "[^a-z0-9_-]", "_") + "_result",
            strict = true,
            schema
        }
    };

    private static bool RequiresNumericEvidence(GameVisionReaderConfig reader) =>
        reader.Outputs.Values.Any(output =>
            output.Type is GameVisionValueType.Integer or GameVisionValueType.Decimal);

    private static string JsonType(GameVisionValueType type) => type switch
    {
        GameVisionValueType.Text => "string",
        GameVisionValueType.Integer => "integer",
        GameVisionValueType.Decimal => "number",
        GameVisionValueType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static GameVisionResult ParseResult(string readerName, GameVisionReaderConfig reader,
        DateTimeOffset capturedAt, string response)
    {
        string json = Regex.Replace(response.Trim(), @"^```(?:json)?\s*|\s*```$", string.Empty);
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException exception)
        { throw new InvalidDataException($"Local AI returned invalid JSON for reader '{readerName}': {response}", exception); }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Local AI did not return an object for reader '{readerName}'.");
            bool requiresEvidence = RequiresNumericEvidence(reader);
            JsonElement outputResults = document.RootElement;
            var values = new Dictionary<string, GameVisionValue>(StringComparer.OrdinalIgnoreCase);
            var failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string name, GameVisionOutputConfig output) in reader.Outputs)
            {
                if (!TryGetProperty(outputResults, name, out JsonElement result))
                { failures[name] = "The model did not return this output."; continue; }
                if (!requiresEvidence)
                {
                    if (result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    { failures[name] = "The value was unreadable."; continue; }
                    try
                    {
                        object directValue = ParseValue(name, output.Type, result);
                        values[name] = new GameVisionValue(name, output.Type, directValue, readerName, capturedAt);
                    }
                    catch (InvalidDataException exception) { failures[name] = exception.Message; }
                    continue;
                }
                if (result.ValueKind != JsonValueKind.Object)
                { failures[name] = "The model returned an invalid readability result."; continue; }
                if (!TryGetProperty(result, "readable", out JsonElement readableElement) ||
                    readableElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                { failures[name] = "The model did not return a valid readable flag."; continue; }
                if (!TryGetProperty(result, "value", out JsonElement valueElement))
                { failures[name] = "The model did not return a value property."; continue; }

                bool readable = readableElement.GetBoolean();
                bool hasValue = valueElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
                if (!readable)
                {
                    failures[name] = hasValue
                        ? "The model marked the output unreadable but also returned a value."
                        : "The model marked the output as unreadable.";
                    continue;
                }
                if (!hasValue)
                {
                    failures[name] = "The model marked the output readable but returned no value.";
                    continue;
                }

                try
                {
                    object value = ParseValue(name, output.Type, valueElement);
                    ValidateConfiguredValue(name, output, value);
                    values[name] = new GameVisionValue(name, output.Type, value, readerName, capturedAt);
                }
                catch (InvalidDataException exception) { failures[name] = exception.Message; }
            }
            return new GameVisionResult { ReaderName = readerName, CapturedAt = capturedAt, Values = values, Failures = failures };
        }
    }

    private static object ParseValue(string name, GameVisionValueType type, JsonElement element) => type switch
    {
        GameVisionValueType.Text when element.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(element.GetString()) &&
            !string.Equals(element.GetString(), "UNKNOWN", StringComparison.OrdinalIgnoreCase) =>
            Regex.Replace(element.GetString()!.Trim(), @"\s+", " "),
        GameVisionValueType.Integer when element.TryGetInt64(out long integer) => integer,
        GameVisionValueType.Decimal when element.TryGetDecimal(out decimal number) => number,
        GameVisionValueType.Boolean when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        _ => throw new InvalidDataException($"Output '{name}' is not a valid {type}.")
    };

    private static void ValidateConfiguredValue(
        string name,
        GameVisionOutputConfig output,
        object value)
    {
        if (output.Type == GameVisionValueType.Integer)
        {
            long integer = (long)value;
            int digits = integer == 0
                ? 1
                : Math.Abs((decimal)integer).ToString("0", System.Globalization.CultureInfo.InvariantCulture).Length;
            if (output.MinimumDigits is int minimumDigits && digits < minimumDigits)
                throw new InvalidDataException($"Output '{name}' has {digits} digits; at least {minimumDigits} are required.");
            if (output.MaximumDigits is int maximumDigits && digits > maximumDigits)
                throw new InvalidDataException($"Output '{name}' has {digits} digits; at most {maximumDigits} are allowed.");
        }

        if (output.Type is GameVisionValueType.Integer or GameVisionValueType.Decimal)
        {
            decimal number = output.Type == GameVisionValueType.Integer ? (long)value : (decimal)value;
            if (output.Minimum is decimal minimum && number < minimum)
                throw new InvalidDataException($"Output '{name}' is below its configured minimum of {minimum}.");
            if (output.Maximum is decimal maximum && number > maximum)
                throw new InvalidDataException($"Output '{name}' is above its configured maximum of {maximum}.");
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = property.Value; return true; }
        value = default;
        return false;
    }

    private static byte[] CapturePng(string executableName, ScreenRegion region)
    {
        using Bitmap capture = ForegroundWindowCapture.CaptureRegion(executableName, region);
        using var stream = new MemoryStream();
        capture.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private async Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt,
        CancellationToken cancellationToken, object responseFormat)
    {
        try { return await _localAi.AnalyzeImageAsync(imageBytes, prompt,
            cancellationToken: cancellationToken, responseFormat: responseFormat); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw CreateUnavailableException(); }
        catch (HttpRequestException exception) { throw CreateUnavailableException(exception); }
    }

    private InvalidOperationException CreateUnavailableException(Exception? innerException = null) => new(
        $"Local AI is unavailable at '{_localAiConfig.BaseUrl}'. Start LM Studio, enable its local server, and load model '{_localAiConfig.Model}'.", innerException);

    public void Dispose() => _localAi.Dispose();
}
