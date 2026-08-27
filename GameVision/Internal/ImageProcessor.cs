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
    internal static Bitmap PrepareMobName(Bitmap source)
    {
        const int scale = 5;
        const int padding = 25;

        const int morphologyRadius = 3;
        const int minimumResponse = 18;

        int width = source.Width;
        int height = source.Height;

        var gray = new int[width, height];

        // 1. Grayscale
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color p = source.GetPixel(x, y);

                gray[x, y] =
                    (p.R * 299 +
                     p.G * 587 +
                     p.B * 114) / 1000;
            }
        }

        // 2. Grayscale dilation
        var dilated = new int[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int maximum = 0;

                int left =
                    Math.Max(0, x - morphologyRadius);

                int right =
                    Math.Min(width - 1, x + morphologyRadius);

                int top =
                    Math.Max(0, y - morphologyRadius);

                int bottom =
                    Math.Min(height - 1, y + morphologyRadius);

                for (int yy = top; yy <= bottom; yy++)
                {
                    for (int xx = left; xx <= right; xx++)
                    {
                        if (gray[xx, yy] > maximum)
                        {
                            maximum = gray[xx, yy];
                        }
                    }
                }

                dilated[x, y] = maximum;
            }
        }

        // 3. Grayscale erosion -> closing
        var closed = new int[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int minimum = 255;

                int left =
                    Math.Max(0, x - morphologyRadius);

                int right =
                    Math.Min(width - 1, x + morphologyRadius);

                int top =
                    Math.Max(0, y - morphologyRadius);

                int bottom =
                    Math.Min(height - 1, y + morphologyRadius);

                for (int yy = top; yy <= bottom; yy++)
                {
                    for (int xx = left; xx <= right; xx++)
                    {
                        if (dilated[xx, yy] < minimum)
                        {
                            minimum = dilated[xx, yy];
                        }
                    }
                }

                closed[x, y] = minimum;
            }
        }

        // 4. Black-hat response
        var response = new int[width, height];

        int strongestResponse = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int value =
                    closed[x, y] - gray[x, y];

                if (value < 0)
                    value = 0;

                response[x, y] = value;

                if (value > strongestResponse)
                {
                    strongestResponse = value;
                }
            }
        }

        // 5. Render as dark text on white.
        // Do not binarize and do not thicken.
        using var prepared = new Bitmap(
            width,
            height,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using (Graphics g = Graphics.FromImage(prepared))
        {
            g.Clear(Color.White);
        }

        if (strongestResponse > 0)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = response[x, y];

                    if (r < minimumResponse)
                        continue;

                    double normalized =
                        (r - minimumResponse) /
                        (double)Math.Max(
                            1,
                            strongestResponse - minimumResponse);

                    normalized =
                        Math.Clamp(
                            normalized,
                            0.0,
                            1.0);

                    normalized =
                        Math.Pow(
                            normalized,
                            0.65);

                    int value =
                        210 -
                        (int)(normalized * 210);

                    value =
                        Math.Clamp(
                            value,
                            0,
                            210);

                    prepared.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            value,
                            value,
                            value));
                }
            }
        }

        // 6. Upscale
        int scaledWidth =
            width * scale;

        int scaledHeight =
            height * scale;

        var result = new Bitmap(
            scaledWidth + padding * 2,
            scaledHeight + padding * 2,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using (Graphics g = Graphics.FromImage(result))
        {
            g.Clear(Color.White);

            g.InterpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            g.PixelOffsetMode =
                System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            g.DrawImage(
                prepared,
                new Rectangle(
                    padding,
                    padding,
                    scaledWidth,
                    scaledHeight),
                new Rectangle(
                    0,
                    0,
                    width,
                    height),
                GraphicsUnit.Pixel);
        }

        return result;
    }
    private static int CountDarkNeighbors(
        bool[,] darkMask,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius)
    {
        int count = 0;

        int left =
            Math.Max(0, centerX - radius);

        int right =
            Math.Min(width - 1, centerX + radius);

        int top =
            Math.Max(0, centerY - radius);

        int bottom =
            Math.Min(height - 1, centerY + radius);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (x == centerX && y == centerY)
                    continue;

                if (darkMask[x, y])
                    count++;
            }
        }

        return count;
    }
    private static bool HasDarkNeighbor(
        bool[,] darkMask,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius)
    {
        int left = Math.Max(0, centerX - radius);
        int right = Math.Min(width - 1, centerX + radius);

        int top = Math.Max(0, centerY - radius);
        int bottom = Math.Min(height - 1, centerY + radius);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (darkMask[x, y])
                    return true;
            }
        }

        return false;
    }

    private static void RemoveSmallComponents(
        bool[,] mask,
        int width,
        int height,
        int minimumSize)
    {
        var visited = new bool[width, height];

        for (int startY = 0; startY < height; startY++)
        {
            for (int startX = 0; startX < width; startX++)
            {
                if (!mask[startX, startY] ||
                    visited[startX, startY])
                {
                    continue;
                }

                var component =
                    new List<(int X, int Y)>();

                var queue =
                    new Queue<(int X, int Y)>();

                queue.Enqueue((startX, startY));
                visited[startX, startY] = true;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    component.Add(current);

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = current.X + dx;
                            int ny = current.Y + dy;

                            if (nx < 0 ||
                                ny < 0 ||
                                nx >= width ||
                                ny >= height)
                            {
                                continue;
                            }

                            if (visited[nx, ny] ||
                                !mask[nx, ny])
                            {
                                continue;
                            }

                            visited[nx, ny] = true;

                            queue.Enqueue((nx, ny));
                        }
                    }
                }

                if (component.Count >= minimumSize)
                    continue;

                foreach (var pixel in component)
                {
                    mask[pixel.X, pixel.Y] = false;
                }
            }
        }
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
