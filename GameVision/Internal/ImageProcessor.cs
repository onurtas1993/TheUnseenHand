using System.Drawing;
using System.Drawing.Drawing2D;

namespace GameVision.Internal;

internal static class ImageProcessor
{
    internal static Bitmap Crop(Bitmap source, ScreenRegion region)
    {
        region = ResolveRegion(source, region);

        if (region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
            region.X + region.Width > source.Width || region.Y + region.Height > source.Height)
            throw new InvalidOperationException("A configured OCR region falls outside the game client area.");

        return source.Clone(
            new Rectangle(region.X, region.Y, region.Width, region.Height),
            source.PixelFormat);
    }

    internal static Bitmap PrepareVital(Bitmap source)
    {
        const int scale = 4;
        const int padding = 20;

        int scaledWidth = source.Width * scale;
        int scaledHeight = source.Height * scale;

        // 1. Enlarge the tiny game text.
        var enlarged = new Bitmap(
            scaledWidth,
            scaledHeight,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using (var graphics = Graphics.FromImage(enlarged))
        {
            graphics.Clear(Color.White);

            graphics.InterpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            graphics.PixelOffsetMode =
                System.Drawing.Drawing2D.PixelOffsetMode.Half;

            graphics.DrawImage(
                source,
                new Rectangle(0, 0, scaledWidth, scaledHeight),
                new Rectangle(0, 0, source.Width, source.Height),
                GraphicsUnit.Pixel);
        }

        // 2. Convert to high-contrast black/white.
        var processed = new Bitmap(
            scaledWidth + (padding * 2),
            scaledHeight + (padding * 2),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using (var graphics = Graphics.FromImage(processed))
        {
            graphics.Clear(Color.White);
        }

        for (int y = 0; y < enlarged.Height; y++)
        {
            for (int x = 0; x < enlarged.Width; x++)
            {
                Color pixel = enlarged.GetPixel(x, y);

                int gray =
                    (pixel.R * 299 +
                     pixel.G * 587 +
                     pixel.B * 114) / 1000;

                // Anything sufficiently bright becomes text.
                Color output =
                    gray >= 140
                        ? Color.Black
                        : Color.White;

                processed.SetPixel(
                    x + padding,
                    y + padding,
                    output);
            }
        }

        enlarged.Dispose();

        return processed;
    }

    internal static ScreenRegion ResolveRegion(
    Bitmap frame,
    ScreenRegion region)
    {
        int x;
        int y;

        switch (region.Anchor)
        {
            case ScreenAnchor.TopLeft:
                x = region.X;
                y = region.Y;
                break;

            case ScreenAnchor.TopCenter:
                x = (frame.Width / 2) + region.X;
                y = region.Y;
                break;

            case ScreenAnchor.TopRight:
                x = frame.Width + region.X;
                y = region.Y;
                break;

            case ScreenAnchor.Center:
                x = (frame.Width / 2) + region.X;
                y = (frame.Height / 2) + region.Y;
                break;

            case ScreenAnchor.BottomLeft:
                x = region.X;
                y = frame.Height + region.Y;
                break;

            case ScreenAnchor.BottomCenter:
                x = (frame.Width / 2) + region.X;
                y = frame.Height + region.Y;
                break;

            case ScreenAnchor.BottomRight:
                x = frame.Width + region.X;
                y = frame.Height + region.Y;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(region.Anchor));
        }

        return new ScreenRegion
        {
            Anchor = ScreenAnchor.TopLeft,
            X = x,
            Y = y,
            Width = region.Width,
            Height = region.Height
        };
    }
}
