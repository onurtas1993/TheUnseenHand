namespace GameVision;

public sealed class GameState
{
    public int PlayerHP { get; init; }
    public int PlayerMaxHP { get; init; }
    public int PlayerMP { get; init; }
    public int PlayerMaxMP { get; init; }
    public string CurrentMob { get; init; } = string.Empty;

    public double PlayerHPPercent => PlayerMaxHP <= 0 ? 0 : PlayerHP * 100.0 / PlayerMaxHP;
    public double PlayerMPPercent => PlayerMaxMP <= 0 ? 0 : PlayerMP * 100.0 / PlayerMaxMP;
}
