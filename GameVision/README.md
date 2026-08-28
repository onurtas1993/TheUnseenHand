# GameVision

Small Windows class library for reading Knight Online HUD state from the currently focused game window.

## Public API

```csharp
var vision = new GameVisionReader("gamevision.json");
var state = vision.ReadGameState();

var hp = state.PlayerHP;
var maxHp = state.PlayerMaxHP;
var mp = state.PlayerMP;
var maxMp = state.PlayerMaxMP;
```

`ReadGameState()` verifies that the focused window belongs to the executable configured in `gamevision.json`, captures its client area, reads the configured HP/MP rectangles, and returns one vitals snapshot.

Use `CaptureRegion()` when an external vision model needs the untouched pixels from a screen region. `CaptureMobRegion()` does the same using the configured executable and mob-name region. These methods only capture and crop; they do not preprocess the image or run Windows OCR.

## Configuration

`gamevision.json` contains absolute coordinates relative to the captured game client area. The included values are calibrated from the provided Knight Online view. There is no automatic coordinate scaling.

## OCR

Uses the OCR engine built into Windows. There is no Tesseract dependency, tessdata/model directory, downloader script, or bundled game screenshot.
