using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class EdgeCoverTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-edge-cover-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);

    [TestCase(1, 1)]
    [TestCase(10, 1000)]
    [TestCase(1000, 10)]
    [TestCase(800, 1200)]
    [TestCase(5000, 1)]
    public void PreviewIsLimitedInBothDirectionsAndNeverUpscales(int width, int height)
    {
        string path = Path.Combine(folder, "Нестандартная обложка.PNG");
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        Assert.DoesNotThrow(() => CoverStore.ValidateSource(path));
        var preview = CoverStore.LoadPreview(path) as BitmapSource;
        Assert.IsNotNull(preview);
        Assert.Multiple(() =>
        {
            Assert.LessOrEqual(preview!.PixelWidth, Math.Min(width, 360));
            Assert.LessOrEqual(preview.PixelHeight, Math.Min(height, 540));
            Assert.IsTrue(preview.IsFrozen);
        });
        File.Delete(path);
        Assert.Greater(preview!.PixelWidth, 0);
    }

    [Test]
    public void LockedSourceAndBlockedDestinationLeaveNoImportedCover()
    {
        string source = WindowTests.MakeCover(folder);
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var store = new CoverStore(paths);
        using (var locked = File.Open(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => store.Import(source));
            Assert.AreSame(CoverStore.DefaultCover, CoverStore.LoadPreview(source));
        }
        Assert.IsFalse(Directory.Exists(paths.CoversDirectory));
        Directory.CreateDirectory(paths.DataDirectory);
        File.WriteAllText(paths.CoversDirectory, "Папка занята файлом");
        Assert.Catch<IOException>(() => store.Import(source));
        Assert.IsTrue(File.Exists(source));
    }
}
