using System.Drawing;
using GameVision.Internal;

namespace GameVision;

public sealed class GameVisionReader
{
    private readonly GameVisionConfig _config;
    private readonly Lazy<WindowsOcrReader> _ocr = new();

    public GameVisionReader(
        string configPath = "gamevision.json")
    {
        _config = GameVisionConfig.Load(configPath);
    }

    // Main production method
    public GameState ReadGameState()
    {
        using var frame =
            ForegroundWindowCapture.Capture(
                _config.ExecutableName);

        using var hpCrop =
            ImageProcessor.Crop(
                frame,
                _config.HpRegion);

        using var mpCrop =
            ImageProcessor.Crop(
                frame,
                _config.MpRegion);

        using var hpPrepared =
            ImageProcessor.PrepareVital(hpCrop);

        using var mpPrepared =
            ImageProcessor.PrepareVital(mpCrop);

        var hp =
            _ocr.Value.ReadVital(hpPrepared);

        var mp =
            _ocr.Value.ReadVital(mpPrepared);

        return new GameState
        {
            PlayerHP = hp.Current,
            PlayerMaxHP = hp.Maximum,

            PlayerMP = mp.Current,
            PlayerMaxMP = mp.Maximum
        };
    }

    // Tester: raw region
    public Bitmap CaptureRegion(
        string executableName,
        ScreenRegion region)
    {
        return ForegroundWindowCapture.CaptureRegion(
            executableName,
            region);
    }

    public Bitmap CaptureMobRegion()
    {
        return CaptureRegion(
            _config.ExecutableName,
            _config.MobNameRegion);
    }

    // Tester: raw region with Windows OCR
    public RegionReadResult ReadRegion(
        string executableName,
        ScreenRegion region)
    {
        using var crop = CaptureRegion(
            executableName,
            region);

        var preview =
            new Bitmap(crop);

        var text =
            _ocr.Value.Recognize(crop);

        return new RegionReadResult(
            preview,
            text);
    }

}
