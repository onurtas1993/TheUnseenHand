using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using GameVision;
using LocalAIAdapter;

namespace TheUnseenHand.Services;

public sealed class MobRecognitionService : IMobRecognitionService, IDisposable
{
    private const string Prompt = """
Return only the text visible in this image.

Rules:
- No explanation.
- No markdown.
- No quotes.
- Preserve spaces.
- If no text is readable, return UNKNOWN.
""";

    private readonly GameVisionReader _vision;
    private readonly LocalAIClient _localAi;
    private readonly string _endpoint;

    public MobRecognitionService()
    {
        string baseDirectory = AppContext.BaseDirectory;
        _vision = new GameVisionReader(Path.Combine(baseDirectory, "gamevision.json"));
        LocalAIConfig config = LocalAIConfig.Load(
            Path.Combine(baseDirectory, "localai.json"));
        _endpoint = config.BaseUrl;
        _localAi = new LocalAIClient(config);
    }

    public async Task<string> RecognizeCurrentAsync(
        CancellationToken cancellationToken)
    {
        byte[] pngBytes = CaptureCurrent();

        string response;
        try
        {
            response = await _localAi.AnalyzeImageAsync(
                pngBytes,
                Prompt,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            throw new InvalidOperationException(
                $"The Local AI model did not respond before the timeout. " +
                $"Check that LM Studio and a vision model are running at {_endpoint}.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                $"The Local AI model is not accessible at {_endpoint}. " +
                "Start LM Studio, load the configured vision model, and enable its local server.",
                exception);
        }

        string name = NormalizeName(response);
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "UNKOWN", StringComparison.OrdinalIgnoreCase))
        {
            name = string.Empty;
        }

        return name;
    }

    public async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _localAi.EnsureModelAvailableAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"The Local AI model is not accessible at {_endpoint}. " +
                "Start LM Studio, load the configured vision model, and enable its local server.",
                exception);
        }
    }

    private byte[] CaptureCurrent()
    {
        using Bitmap capture = _vision.CaptureMobRegion();
        using var stream = new MemoryStream();
        capture.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public static string NormalizeName(string value)
    {
        string normalized = value.Trim().Trim('"', '\'', '`');
        normalized = Regex.Replace(normalized, @"^```\w*\s*|\s*```$", string.Empty);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    public void Dispose()
    {
        _localAi.Dispose();
    }
}
