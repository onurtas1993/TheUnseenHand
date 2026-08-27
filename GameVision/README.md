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
var mob = state.CurrentMob;
```

`ReadGameState()` verifies that the focused window belongs to the executable configured in `gamevision.json`, captures its client area, crops the configured HP/MP/mob-name rectangles, and returns one state snapshot.

## Configuration

`gamevision.json` contains absolute coordinates relative to the captured game client area. The included values are calibrated from the provided Knight Online view. There is no automatic coordinate scaling.

## OCR

Uses the OCR engine built into Windows. There is no Tesseract dependency, tessdata/model directory, downloader script, or bundled game screenshot.
