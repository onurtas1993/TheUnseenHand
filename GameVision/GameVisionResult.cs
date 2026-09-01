namespace GameVision;

public sealed record GameVisionValue(string Name, GameVisionValueType Type, object Value,
    string ReaderName, DateTimeOffset CapturedAt)
{
    public string GetText() => Type == GameVisionValueType.Text ? (string)Value : throw WrongType("Text");
    public long GetInteger() => Type == GameVisionValueType.Integer ? (long)Value : throw WrongType("Integer");
    public decimal GetDecimal() => Type switch
    {
        GameVisionValueType.Decimal => (decimal)Value,
        GameVisionValueType.Integer => (long)Value,
        _ => throw WrongType("Decimal")
    };
    public bool GetBoolean() => Type == GameVisionValueType.Boolean ? (bool)Value : throw WrongType("Boolean");
    private InvalidOperationException WrongType(string expected) => new($"Output '{Name}' is {Type}, not {expected}.");
}

public sealed class GameVisionResult
{
    public required string ReaderName { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyDictionary<string, GameVisionValue> Values { get; init; }
    public required IReadOnlyDictionary<string, string> Failures { get; init; }
}

public sealed class GameVisionSnapshot
{
    public required IReadOnlyDictionary<string, GameVisionValue> Values { get; init; }
    public required IReadOnlyDictionary<string, string> Failures { get; init; }
}
