# GameVision

Windows vision boundary for capturing configured game regions and reading them with a LocalAIAdapter vision model.

## Public API

```csharp
using var vision = new GameVisionReader("gamevision.json", "localai.json");

await vision.EnsureAvailableAsync();
var vitals = await vision.ReadAsync("PlayerVitals");
var hp = vitals.Values["PlayerHP"].GetInteger();

var mob = await vision.ReadValueAsync("CurrentMob");
var allVisibleValues = await vision.ReadAllAsync();
```

`ReadAsync()` reads one configured region and publishes every successfully parsed output. `ReadValueAsync()` locates an output by its globally unique name. `ReadAllAsync()` reads every configured reader. Every operation verifies that the focused window belongs to the configured executable before capturing pixels and sending the PNG to LocalAIAdapter.

`CaptureReader()` and `CaptureRegion()` expose untouched cropped pixels for diagnostics. They do not resize, threshold, sharpen, or otherwise preprocess the image.

## Configuration

`gamevision.json` contains a `Readers` object. Each freely named reader defines a region, prompt lines, and one or more freely named outputs. Output names must be unique across the file. Supported output types are `Text`, `Integer`, `Decimal`, and `Boolean`. Numeric outputs may optionally define `Minimum` and `Maximum`; integer outputs may additionally define `MinimumDigits` and `MaximumDigits`. These constraints are validated after recognition and are not disclosed to the model. Coordinates are relative to the captured game client area and are not automatically scaled. `localai.json` selects the Local AI endpoint and vision model.

The response schema is generated from `Outputs`. GameVision makes one model request per reader. The model must return a `readable` decision and a typed `value` for numeric outputs. A value is published only when it is readable, present, correctly typed, and passes its optional configured validators. Unreadable, contradictory, type-invalid, or out-of-range results are reported in `Failures` and omitted from `Values`.

## Recognition

All text and numeric recognition is performed by the configured Local AI vision model. GameVision contains no Windows OCR or custom pixel-recognition pipeline.
