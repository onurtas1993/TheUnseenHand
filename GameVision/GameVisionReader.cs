using System.Drawing;
using GameVision.Internal;

namespace GameVision;

public sealed class GameVisionReader
{
    private readonly GameVisionConfig _config;
    private readonly WindowsOcrReader _ocr = new();

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

        using var mobCrop =
            ImageProcessor.Crop(
                frame,
                _config.MobNameRegion);

        using var hpPrepared =
            ImageProcessor.PrepareVital(hpCrop);

        using var mpPrepared =
            ImageProcessor.PrepareVital(mpCrop);

        using var mobPrepared =
            ImageProcessor.PrepareMobName(mobCrop);

        var hp =
            _ocr.ReadVital(hpPrepared);

        var mp =
            _ocr.ReadVital(mpPrepared);

        var mob =
            _ocr.ReadMobName(mobPrepared);

        return new GameState
        {
            PlayerHP = hp.Current,
            PlayerMaxHP = hp.Maximum,

            PlayerMP = mp.Current,
            PlayerMaxMP = mp.Maximum,

            CurrentMob = mob
        };
    }

    // Tester: raw region
    public RegionReadResult ReadRegion(
        string executableName,
        ScreenRegion region)
    {
        using var frame =
            ForegroundWindowCapture.Capture(
                executableName);

        using var crop =
            ImageProcessor.Crop(
                frame,
                region);

        var preview =
            new Bitmap(crop);

        var text =
            _ocr.Recognize(crop);

        return new RegionReadResult(
            preview,
            text);
    }

    // Tester: processed mob region
    public RegionReadResult ReadMobRegion(
        string executableName,
        ScreenRegion region)
    {
        using var frame =
            ForegroundWindowCapture.Capture(
                executableName);

        using var crop =
            ImageProcessor.Crop(
                frame,
                region);

        using var prepared =
            ImageProcessor.PrepareMobName(crop);

        var preview =
            new Bitmap(prepared);

        var text =
            _ocr.Recognize(prepared);

        return new RegionReadResult(
            preview,
            text);
    }
}