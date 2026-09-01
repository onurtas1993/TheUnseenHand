namespace TheUnseenHand.Models;

public sealed class MacroCondition
{
    public string Source { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.LessThan;
    public string Value { get; set; } = string.Empty;
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
