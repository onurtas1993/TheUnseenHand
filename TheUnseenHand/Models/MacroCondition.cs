namespace TheUnseenHand.Models;

public sealed class MacroCondition
{
    public ConditionSource Source { get; set; } = ConditionSource.PlayerHP;
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.LessThan;
    public string Value { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new();
}

public enum ConditionSource
{
    PlayerHP,
    PlayerMaxHP,
    PlayerHPPercent,
    PlayerMP,
    PlayerMaxMP,
    PlayerMPPercent,
    CurrentMob
}

public enum ComparisonOperator
{
    Equals,
    NotEquals,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual
}
