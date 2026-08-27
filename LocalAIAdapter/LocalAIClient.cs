using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalAIAdapter;

public sealed class LocalAIClient : IDisposable
{
    private readonly LocalAIConfig _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LocalAIClient(string configPath = "localai.json")
        : this(LocalAIConfig.Load(configPath), null)
    {
    }

    public LocalAIClient(LocalAIConfig config, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }
    }
    public string AnalyzeImage(
    byte[] imageBytes,
    string prompt,
    string mimeType = "image/png")
    {
        return AnalyzeImageAsync(
            imageBytes,
            prompt,
            mimeType,
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<string> AnalyzeImageAsync(
        byte[] imageBytes,
        string prompt,
        string mimeType = "image/png",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
            throw new ArgumentException("Image cannot be empty.", nameof(imageBytes));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type cannot be empty.", nameof(mimeType));

        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";

        var request = new
        {
            model = _config.Model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = prompt
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            },
            temperature = _config.Temperature,
            max_tokens = _config.MaxTokens,
            stream = false
        };

        var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";

        using var response = await _httpClient.PostAsJsonAsync(
            endpoint,
            request,
            cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LM Studio returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);

        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("LM Studio response did not contain any choices.");
        }

        var message = choices[0].GetProperty("message");

        if (!message.TryGetProperty("content", out var content))
            throw new InvalidOperationException("LM Studio response did not contain message content.");

        return content.GetString()?.Trim() ?? string.Empty;
    }

    public async Task<string> AnalyzeImageAsync(
        string imagePath,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image was not found: {imagePath}");

        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var mimeType = GetMimeType(imagePath);

        return await AnalyzeImageAsync(bytes, prompt, mimeType, cancellationToken);
    }

    private static string GetMimeType(string imagePath)
    {
        return Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
