# Vision.Inference

Minimal .NET class library for forwarding image and prompt requests to a local LM Studio server using its OpenAI-compatible Chat Completions endpoint.

## Requirements

- LM Studio running with Local Server enabled.
- A vision/OCR model loaded in LM Studio.
- `localai.json` copied beside the consuming application's executable.

No LLamaSharp, GGUF loading, CUDA, Vulkan, or model files are handled by this library.

## Usage

```csharp
using Vision.Inference;

using var inference = new InferenceClient("localai.json");

var result = await inference.AnalyzeImageAsync(
    imageBytes,
    "Read only the mob name. Return only the exact text.");
```

`imageBytes` should contain PNG, JPEG, or WebP data. The default overload assumes PNG; pass the MIME type explicitly if needed.

`InferenceConfig.Load()` validates and loads `localai.json`. `InferenceClient`
owns its internally created `HttpClient`, supports model-availability checks,
and should be disposed after use. A caller-supplied `HttpClient` remains owned
by the caller.
