using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace GameVision.Internal;

internal sealed class WindowsOcrReader
{
    private readonly OcrEngine _engine;

    public WindowsOcrReader()
    {
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "Windows OCR is not available for the current Windows language configuration.");
    }

    public (int Current, int Maximum) ReadVital(Bitmap bitmap)
    {
        var text = Recognize(bitmap);
        var match = Regex.Match(text, @"(?<current>\d+)\s*[/\\]\s*(?<max>\d+)");

        if (!match.Success)
            throw new InvalidOperationException($"Could not recognize HP/MP value from OCR text '{text}'.");

        return (
            int.Parse(match.Groups["current"].Value),
            int.Parse(match.Groups["max"].Value));
    }

    public string ReadMobName(Bitmap bitmap)
    {
        var text = Recognize(bitmap);
        return Regex.Replace(text, @"[^A-Za-z0-9 '\-]", " ")
            .Replace("  ", " ")
            .Trim();
    }

    internal string Recognize(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var output = randomAccessStream.GetOutputStreamAt(0))
        using (var writer = new DataWriter(output))
        {
            writer.WriteBytes(stream.ToArray());
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.FlushAsync().AsTask().GetAwaiter().GetResult();
        }

        randomAccessStream.Seek(0);
        var decoder = BitmapDecoder.CreateAsync(randomAccessStream).AsTask().GetAwaiter().GetResult();
        using var softwareBitmap = decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied).AsTask().GetAwaiter().GetResult();

        var result = _engine.RecognizeAsync(softwareBitmap).AsTask().GetAwaiter().GetResult();
        return result.Text?.Trim() ?? string.Empty;
    }
}
