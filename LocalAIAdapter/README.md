# LocalAIAdapter

Minimal .NET class library for forwarding image + prompt requests to a local LM Studio server using its OpenAI-compatible Chat Completions endpoint.

## Requirements

- LM Studio running with Local Server enabled.
- A vision/OCR model loaded in LM Studio.
- `localai.json` copied beside the consuming application's executable.

No LLamaSharp, GGUF loading, CUDA, Vulkan, or model files are handled by this library.

## Usage

```csharp
using LocalAIAdapter;

using var ai = new LocalAIClient("localai.json");

var result = await ai.AnalyzeImageAsync(
    imageBytes,
    "Read only the mob name. Return only the exact text.");
```

`imageBytes` should contain PNG, JPEG, or WebP data. The default overload assumes PNG; pass the MIME type explicitly if needed.
