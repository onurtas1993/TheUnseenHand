using Vision.GameCapture;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Vision.RegionEditor;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void PreviewReaderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string readerName = GetReaderName();
            await PrepareCaptureAsync($"testing reader '{readerName}'");
            using var gameCapture = new GameCaptureReader();
            using Bitmap capture = gameCapture.CaptureReader(readerName);
            PreviewImage.Source = BitmapToImageSource(capture);
            GameCaptureResult result = await gameCapture.ReadAsync(readerName);
            ResultTextBox.Text = FormatResult(result.Values, result.Failures);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void ReadAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await PrepareCaptureAsync("reading all configured readers");
            using var gameCapture = new GameCaptureReader();
            GameCaptureSnapshot snapshot = await gameCapture.ReadAllAsync();
            PreviewImage.Source = null;
            ResultTextBox.Text = FormatResult(snapshot.Values, snapshot.Failures);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private string GetReaderName()
    {
        string readerName = ReaderNameTextBox.Text.Trim();
        return readerName.Length > 0
            ? readerName
            : throw new InvalidOperationException(
                "Enter a reader name exactly as configured in gamevision.json.");
    }

    private async Task PrepareCaptureAsync(string operation)
    {
        PreviewImage.Source = null;
        ResultTextBox.Text = $"Switch to the game... {operation} in 3 seconds.";
        await Task.Delay(3000);
    }

    private void ShowError(Exception exception)
    {
        PreviewImage.Source = null;
        ResultTextBox.Text = $"{exception.GetType().Name}: {exception.Message}";
    }

    private static string FormatResult(
        IReadOnlyDictionary<string, GameCaptureValue> values,
        IReadOnlyDictionary<string, string> failures)
    {
        var publishedValues = values.ToDictionary(
            pair => pair.Key,
            pair => new
            {
                type = pair.Value.Type.ToString(),
                value = pair.Value.Value,
                reader = pair.Value.ReaderName,
                capturedAt = pair.Value.CapturedAt
            },
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(
            new
            {
                values = publishedValues,
                failures
            },
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
