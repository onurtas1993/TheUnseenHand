using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameVision.Internal;
using LocalAIAdapter;

namespace GameVision;

public sealed class GameVisionReader : IDisposable
{
    private const string VitalsPrompt = """
Read the four numeric player-vitals values visible in this image.

Image layout:
- The top row is HP and is formatted as current HP / maximum HP.
- The bottom row is MP and is formatted as current MP / maximum MP.
- The slash separates the current value on its left from the maximum value on its right.
- Empty black space behind a partially depleted bar is normal. Read the printed digits, not the bar length.

Return one JSON object containing exactly these integer properties:
hp, maxHp, mp, maxMp

Rules:
- Enclose every property name in double quotes. This must be strict JSON, not a JavaScript object.
- Use integers only.
- No explanation.
- No markdown.
- Do not return placeholder or example values.
- If any of the four numbers is unreadable, return UNKNOWN instead of guessing.
""";

    private const string MobNamePrompt = """
Return only the exact mob name visible in this image.

Rules:
- No explanation.
- No markdown.
- No quotes.
- Preserve spaces.
- If no text is readable, return UNKNOWN.
""";

    private static readonly object VitalsResponseFormat = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "game_vitals",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    hp = new { type = "integer" },
                    maxHp = new { type = "integer" },
                    mp = new { type = "integer" },
                    maxMp = new { type = "integer" }
                },
                required = new[] { "hp", "maxHp", "mp", "maxMp" },
                additionalProperties = false
            }
        }
    };

    private readonly GameVisionConfig _config;
    private readonly LocalAIConfig _localAiConfig;
    private readonly LocalAIClient _localAi;

    public GameVisionReader(
        string configPath = "gamevision.json",
        string localAiConfigPath = "localai.json")
    {
        _config = GameVisionConfig.Load(configPath);
        _localAiConfig = LocalAIConfig.Load(localAiConfigPath);
        _localAi = new LocalAIClient(_localAiConfig);
    }

    public async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _localAi.EnsureModelAvailableAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public async Task<GameVitals> ReadVitalsAsync(
        CancellationToken cancellationToken = default)
    {
        byte[] imageBytes = CapturePng(
            _config.ExecutableName,
            _config.VitalsRegion);
        string response = await AnalyzeImageAsync(
            imageBytes,
            VitalsPrompt,
            cancellationToken,
            VitalsResponseFormat);

        VitalsResponse parsed = ParseVitals(response);
        ValidateVitals(parsed, response);

        return new GameVitals
        {
            PlayerHP = parsed.HP,
            PlayerMaxHP = parsed.MaxHP,
            PlayerMP = parsed.MP,
            PlayerMaxMP = parsed.MaxMP
        };
    }

    public async Task<string> ReadMobNameAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadMobNameAsync(
            _config.ExecutableName,
            _config.MobNameRegion,
            cancellationToken);
    }

    public async Task<string> ReadMobNameAsync(
        string executableName,
        ScreenRegion region,
        CancellationToken cancellationToken = default)
    {
        byte[] imageBytes = CapturePng(executableName, region);
        string response = await AnalyzeImageAsync(
            imageBytes,
            MobNamePrompt,
            cancellationToken);

        string name = NormalizeText(response);
        return string.Equals(name, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "UNKOWN", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : name;
    }

    public Bitmap CaptureRegion(string executableName, ScreenRegion region)
    {
        return ForegroundWindowCapture.CaptureRegion(executableName, region);
    }

    public Bitmap CaptureVitalsRegion()
    {
        return CaptureRegion(_config.ExecutableName, _config.VitalsRegion);
    }

    public Bitmap CaptureMobRegion()
    {
        return CaptureRegion(_config.ExecutableName, _config.MobNameRegion);
    }

    private static byte[] CapturePng(
        string executableName,
        ScreenRegion region)
    {
        using Bitmap capture =
            ForegroundWindowCapture.CaptureRegion(executableName, region);
        using var stream = new MemoryStream();
        capture.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private async Task<string> AnalyzeImageAsync(
        byte[] imageBytes,
        string prompt,
        CancellationToken cancellationToken,
        object? responseFormat = null)
    {
        try
        {
            return await _localAi.AnalyzeImageAsync(
                imageBytes,
                prompt,
                cancellationToken: cancellationToken,
                responseFormat: responseFormat);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    private InvalidOperationException CreateUnavailableException(
        Exception? innerException = null)
    {
        return new InvalidOperationException(
            $"Local AI is unavailable at '{_localAiConfig.BaseUrl}'. " +
            $"Start LM Studio, enable its local server, and load model " +
            $"'{_localAiConfig.Model}'.",
            innerException);
    }

    private static VitalsResponse ParseVitals(string response)
    {
        string json = response.Trim();

        if (string.Equals(json, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(json, "UNKOWN", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Local AI could not read all four vitals values. Raw response: " +
                response);
        }

        json = Regex.Replace(json, @"^```(?:json)?\s*|\s*```$", string.Empty);

        try
        {
            return JsonSerializer.Deserialize<VitalsResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Local AI returned an empty vitals result.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Local AI returned invalid vitals JSON: {response}",
                exception);
        }
    }

    private static void ValidateVitals(VitalsResponse vitals, string rawResponse)
    {
        if (vitals.HP < 0 || vitals.MaxHP <= 0 || vitals.HP > vitals.MaxHP ||
            vitals.MP < 0 || vitals.MaxMP <= 0 || vitals.MP > vitals.MaxMP)
        {
            throw new InvalidDataException(
                $"Local AI returned invalid vitals: " +
                $"HP {vitals.HP}/{vitals.MaxHP}, MP {vitals.MP}/{vitals.MaxMP}. " +
                $"Raw response: {rawResponse}");
        }
    }

    private static string NormalizeText(string value)
    {
        string normalized = value.Trim().Trim('"', '\'', '`');
        normalized = Regex.Replace(normalized, @"^```\w*\s*|\s*```$", string.Empty);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    public void Dispose()
    {
        _localAi.Dispose();
    }

    private sealed class VitalsResponse
    {
        public int HP { get; init; }
        public int MaxHP { get; init; }
        public int MP { get; init; }
        public int MaxMP { get; init; }
    }
}
