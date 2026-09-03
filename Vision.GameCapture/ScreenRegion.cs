namespace Vision.GameCapture;

public sealed class ScreenRegion
{
    public ScreenAnchor Anchor { get; set; } = ScreenAnchor.TopLeft;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}