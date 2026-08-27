using System.Drawing;

namespace GameVision;

public sealed class RegionReadResult : IDisposable
{
    public Bitmap Preview { get; }
    public string Text { get; }

    public RegionReadResult(Bitmap preview, string text)
    {
        Preview = preview;
        Text = text;
    }

    public void Dispose()
    {
        Preview.Dispose();
    }
}