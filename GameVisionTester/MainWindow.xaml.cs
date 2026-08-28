using GameVision;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LocalAIAdapter;
using System.Drawing.Imaging;

namespace GameVisionTester;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void CaptureButton_Click(
       object sender,
       RoutedEventArgs e)
    {
        try
        {
            var selectedItem =
                (ComboBoxItem)AnchorComboBox.SelectedItem;

            var anchorName =
                selectedItem.Content.ToString()!;

            var anchor =
                Enum.Parse<ScreenAnchor>(anchorName);

            var region = new ScreenRegion
            {
                Anchor = anchor,
                X = int.Parse(XTextBox.Text),
                Y = int.Parse(YTextBox.Text),
                Width = int.Parse(WidthTextBox.Text),
                Height = int.Parse(HeightTextBox.Text)
            };

            ResultTextBox.Text =
                "Switch to the game... capturing in 3 seconds.";

            await Task.Delay(3000);

            var reader = new GameVisionReader();

            using var result = reader.ReadRegion(
                ExeTextBox.Text.Trim(),
                region);

            PreviewImage.Source =
                BitmapToImageSource(result.Preview);

            ResultTextBox.Text = result.Text;
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            ResultTextBox.Text = ex.Message;
        }
    }

    private static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();

        bitmap.Save(
            stream,
            System.Drawing.Imaging.ImageFormat.Png);

        stream.Position = 0;

        var image = new BitmapImage();

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }

    private async void TestGameStateButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        try
        {
            ResultTextBox.Text =
                "Switch to the game... reading in 3 seconds.";

            await Task.Delay(3000);

            var reader = new GameVisionReader();

            var state = reader.ReadGameState();

            ResultTextBox.Text =
                $"HP: {state.PlayerHP}/{state.PlayerMaxHP}    " +
                $"MP: {state.PlayerMP}/{state.PlayerMaxMP}";
        }
        catch (Exception ex)
        {
            ResultTextBox.Text = ex.Message;
        }
    }

    private async void TestLocalAIButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var selectedItem =
                (ComboBoxItem)AnchorComboBox.SelectedItem;

            var anchor =
                Enum.Parse<ScreenAnchor>(
                    selectedItem.Content.ToString()!);

            var region = new ScreenRegion
            {
                Anchor = anchor,
                X = int.Parse(XTextBox.Text),
                Y = int.Parse(YTextBox.Text),
                Width = int.Parse(WidthTextBox.Text),
                Height = int.Parse(HeightTextBox.Text)
            };

            ResultTextBox.Text =
                "Switch to the game... capturing in 3 seconds.";

            await Task.Delay(3000);

            var vision =
                new GameVisionReader();

            using var capture =
                vision.CaptureRegion(
                    ExeTextBox.Text.Trim(),
                    region);

            byte[] imageBytes;

            using (var stream = new MemoryStream())
            {
                capture.Save(
                    stream,
                    System.Drawing.Imaging.ImageFormat.Png);

                imageBytes =
                    stream.ToArray();
            }

            PreviewImage.Source =
                PngToImageSource(imageBytes);

            const string prompt = """
Return only the text visible in this image.

Rules:
- No explanation.
- No markdown.
- No quotes.
- Preserve spaces.
- If no text is readable, return UNKOWN.
""";

            using var ai =
                new LocalAIClient("localai.json");

            var result =
                await ai.AnalyzeImageAsync(
                    imageBytes,
                    prompt);

            ResultTextBox.Text = result;
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;

            ResultTextBox.Text =
                $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static BitmapImage PngToImageSource(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var image = new BitmapImage();

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }

}
