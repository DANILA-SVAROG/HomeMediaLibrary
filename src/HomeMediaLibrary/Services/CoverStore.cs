using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HomeMediaLibrary.Services;

public class CoverStore
{
    private readonly AppPaths paths;
    public static ImageSource DefaultCover { get; } = CreateDefaultCover();

    public CoverStore(AppPaths paths) => this.paths = paths;

    public string Import(string source)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png")
            throw new ArgumentException("Для обложки выберите файл JPEG или PNG.");
        byte[] bytes = File.ReadAllBytes(source);
        using (var stream = new MemoryStream(bytes))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder is not PngBitmapDecoder and not JpegBitmapDecoder || decoder.Frames.Count == 0)
                throw new ArgumentException("Файл не является изображением JPEG или PNG.");
        }
        Directory.CreateDirectory(paths.CoversDirectory);
        string target = Path.Combine(paths.CoversDirectory, Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(target, bytes);
        return target;
    }

    public static ImageSource LoadPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return DefaultCover;
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 360;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
            or FileFormatException or ArgumentException or FormatException)
        {
            return DefaultCover;
        }
    }

    // Удаляется только новая копия, которая не попала в коллекцию.
    public void RemoveUnusedImport(string path)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) == Path.GetFullPath(paths.CoversDirectory) && File.Exists(path))
            File.Delete(path);
    }

    private static ImageSource CreateDefaultCover()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(229, 234, 239)), null, new Rect(0, 0, 220, 330));
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(123, 141, 158)), 4);
            dc.DrawRoundedRectangle(null, pen, new Rect(65, 90, 90, 75), 7, 7);
            dc.DrawGeometry(pen.Brush, null, Geometry.Parse("M95,108 L95,148 L128,128 Z"));
            var text = new FormattedText("Нет обложки", CultureInfo.GetCultureInfo("ru-RU"),
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 18, pen.Brush, 1);
            dc.DrawText(text, new Point((220 - text.Width) / 2, 190));
        }
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
