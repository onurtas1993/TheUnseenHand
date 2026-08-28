# GameVision

Windows vision boundary for capturing configured game regions and reading them with a LocalAIAdapter vision model.

## Public API

```csharp
using var vision = new GameVisionReader("gamevision.json", "localai.json");

await vision.EnsureAvailableAsync();
var state = await vision.ReadVitalsAsync();
var mobName = await vision.ReadMobNameAsync();

var hp = state.PlayerHP;
var maxHp = state.PlayerMaxHP;
var mp = state.PlayerMP;
var maxMp = state.PlayerMaxMP;
```

`ReadVitalsAsync()` and `ReadMobNameAsync()` verify that the focused window belongs to the configured executable, capture the relevant raw pixels, and send the PNG directly to LocalAIAdapter. GameVision owns both the screen-region configuration and the prompts/output parsing.

`CaptureRegion()`, `CaptureVitalsRegion()`, and `CaptureMobRegion()` expose the untouched cropped pixels for diagnostics. They do not resize, threshold, sharpen, or otherwise preprocess the image.

## Configuration

`gamevision.json` contains absolute coordinates relative to the captured game client area. `localai.json` selects the Local AI endpoint and vision model. There is no automatic coordinate scaling.

## Recognition

All text and numeric recognition is performed by the configured Local AI vision model. GameVision contains no Windows OCR or custom pixel-recognition pipeline.
