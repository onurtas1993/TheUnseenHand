using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Vision.Inference;

public sealed class InferenceClient : IDisposable
{
    private readonly InferenceConfig _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public InferenceClient(string configPath = "localai.json")
        : this(InferenceConfig.Load(configPath), null)
    {
    }

    public InferenceClient(InferenceConfig config, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateHttpClient(config);
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
        CancellationToken cancellationToken = default,
        object? responseFormat = null)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
            throw new ArgumentException("Image cannot be empty.", nameof(imageBytes));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type cannot be empty.", nameof(mimeType));

        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";

        var request = new Dictionary<string, object?>
        {
            ["model"] = _config.Model,
            ["messages"] = new object[]
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
            ["temperature"] = _config.Temperature,
            ["max_tokens"] = _config.MaxTokens,
            ["stream"] = false
        };

        if (responseFormat is not null)
            request["response_format"] = responseFormat;

        var endpoint = BuildEndpoint(_config.BaseUrl, "chat/completions");

        byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(requestBytes),
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        requestMessage.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };

        requestMessage.Headers.ExpectContinue = false;

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
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

    public async Task EnsureModelAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        Uri endpoint = BuildEndpoint(_config.BaseUrl, "models");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LM Studio returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {responseText}");
        }

        using JsonDocument document = JsonDocument.Parse(responseText);
        if (!document.RootElement.TryGetProperty("data", out JsonElement models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("LM Studio did not return a valid model list.");
        }

        bool found = models.EnumerateArray().Any(model =>
            model.TryGetProperty("id", out JsonElement id) &&
            string.Equals(id.GetString(), _config.Model, StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            throw new InvalidOperationException(
                $"The configured Local AI model '{_config.Model}' is not loaded in LM Studio.");
        }
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

    private static HttpClient CreateHttpClient(InferenceConfig config)
    {
        var handler = new SocketsHttpHandler();

        if (Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out Uri? baseUri) &&
            baseUri.IsLoopback)
        {
            handler.UseProxy = false;
        }

        return new HttpClient(handler, disposeHandler: true);
    }

    private static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        var builder = new UriBuilder(
            $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}");

        if (string.Equals(
                builder.Host,
                "localhost",
                StringComparison.OrdinalIgnoreCase))
        {
            builder.Host = "127.0.0.1";
        }

        return builder.Uri;
    }
}
