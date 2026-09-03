# Vision.GameCapture

Windows vision boundary for capturing configured game regions and reading them through `Vision.Inference`.

## Public API

```csharp
using Vision.GameCapture;

using var capture = new GameCaptureReader("gamevision.json", "localai.json");

await capture.EnsureAvailableAsync();
var vitals = await capture.ReadAsync("PlayerVitals");
var hp = vitals.Values["PlayerHP"].GetInteger();

var mob = await capture.ReadValueAsync("CurrentMob");
var allVisibleValues = await capture.ReadAllAsync();
```

`ReadAsync()` reads one configured region and publishes every successfully parsed output. `ReadValueAsync()` locates an output by its globally unique name. `ReadAllAsync()` reads every configured reader. Every operation verifies that the focused window belongs to the configured executable before capturing pixels and sending the PNG to `Vision.Inference`.

`CaptureReader()` and `CaptureRegion()` expose untouched cropped pixels for diagnostics. They do not resize, threshold, sharpen, or otherwise preprocess the image.

## Configuration

`GameCaptureConfig` loads `gamevision.json`, whose `Readers` object maps names to `GameCaptureReaderConfig` entries. Each reader defines a region, prompt lines, and one or more `GameCaptureOutputConfig` entries. Output names must be unique across the file. `GameCaptureValueType` supports `Text`, `Integer`, `Decimal`, and `Boolean`. Numeric outputs may optionally define `Minimum` and `Maximum`; integer outputs may additionally define `MinimumDigits` and `MaximumDigits`. These constraints are validated after recognition and are not disclosed to the model. Coordinates are relative to the captured game client area and are not automatically scaled. `localai.json` supplies the `Vision.Inference` configuration.

The response schema is generated from `Outputs`. `GameCaptureReader` makes one model request per configured reader and returns a `GameCaptureResult`; `ReadAllAsync()` returns a `GameCaptureSnapshot`. The model must return a `readable` decision and a typed `value` for numeric outputs. A `GameCaptureValue` is published only when it is readable, present, correctly typed, and passes its optional validators. Unreadable, contradictory, type-invalid, or out-of-range results are reported in `Failures` and omitted from `Values`.

## Recognition

All text and numeric recognition is performed by the configured local vision model. `Vision.GameCapture` contains no Windows OCR or custom pixel-recognition pipeline.
