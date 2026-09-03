namespace Vision.GameCapture;

public sealed record GameCaptureValue(
    string Name,
    GameCaptureValueType Type,
    object Value,
    string ReaderName,
    DateTimeOffset CapturedAt)
{
    public string GetText() => Type == GameCaptureValueType.Text ? (string)Value : throw WrongType("Text");
    public long GetInteger() => Type == GameCaptureValueType.Integer ? (long)Value : throw WrongType("Integer");

    public decimal GetDecimal() => Type switch
    {
        GameCaptureValueType.Decimal => (decimal)Value,
        GameCaptureValueType.Integer => (long)Value,
        _ => throw WrongType("Decimal")
    };

    public bool GetBoolean() => Type == GameCaptureValueType.Boolean ? (bool)Value : throw WrongType("Boolean");
    private InvalidOperationException WrongType(string expected) => new($"Output '{Name}' is {Type}, not {expected}.");
}

public sealed class GameCaptureResult
{
    public required string ReaderName { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyDictionary<string, GameCaptureValue> Values { get; init; }
    public required IReadOnlyDictionary<string, string> Failures { get; init; }
}

public sealed class GameCaptureSnapshot
{
    public required IReadOnlyDictionary<string, GameCaptureValue> Values { get; init; }
    public required IReadOnlyDictionary<string, string> Failures { get; init; }
}
