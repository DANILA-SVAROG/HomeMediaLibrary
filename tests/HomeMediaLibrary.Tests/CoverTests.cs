using System.IO;
using System.Threading;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class CoverTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-cover-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);

    [Test]
    public void ImageCopiesToDataFolderAndDoesNotHoldSourceOpen()
    {
        string source = WindowTests.MakeCover(folder);
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var store = new CoverStore(paths);
        string imported = store.Import(source);
        Assert.AreEqual(paths.CoversDirectory, Path.GetDirectoryName(imported));
        Assert.AreNotEqual(source, imported);
        File.Delete(source);
        Assert.AreNotSame(CoverStore.DefaultCover, CoverStore.LoadPreview(imported));
        File.Delete(imported);
        Assert.AreSame(CoverStore.DefaultCover, CoverStore.LoadPreview(imported));
    }

    [TestCase("")]
    [TestCase("missing.png")]
    public void MissingCoverUsesDefaultImage(string path)
        => Assert.AreSame(CoverStore.DefaultCover, CoverStore.LoadPreview(path));

    [Test]
    public void BrokenImageIsRejectedAndPreviewUsesDefault()
    {
        string path = Path.Combine(folder, "broken.png");
        File.WriteAllText(path, "не изображение");
        Assert.Catch(() => CoverStore.ValidateSource(path));
        Assert.AreSame(CoverStore.DefaultCover, CoverStore.LoadPreview(path));
        Assert.IsFalse(Directory.Exists(Path.Combine(folder, "data")));
    }

    [Test]
    public void UnsupportedExtensionIsRejectedBeforeCopy()
    {
        string path = WindowTests.MakeCover(folder, "fake.gif");
        var store = new CoverStore(new AppPaths(Path.Combine(folder, "data")));
        Assert.Throws<ArgumentException>(() => store.Import(path));
    }

    [Test]
    public void FailedLibrarySaveRemovesOnlyNewImportedCover()
    {
        string source = WindowTests.MakeCover(folder);
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var repo = new MemoryRepository { FailSave = true };
        var window = new MainWindow(paths, repo);
        try
        {
            Assert.Throws<IOException>(() => window.SaveItem(TestData.Movie(), source, false));
            Assert.IsEmpty(window.Library.Items);
            Assert.IsTrue(File.Exists(source));
            Assert.IsEmpty(Directory.GetFiles(paths.CoversDirectory));
            repo.FailSave = false;
            window.SaveItem(TestData.Movie(), source, false);
            Assert.AreEqual(1, window.Library.Items.Count);
            Assert.AreEqual(1, Directory.GetFiles(paths.CoversDirectory).Length);
        }
        finally { window.Close(); }
    }

    [Test]
    public void CancellingCoverSelectionDoesNotCopyFiles()
    {
        string source = WindowTests.MakeCover(folder);
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var editor = new EditorWindow(TestData.Movie());
        try
        {
            editor.SelectCover(source);
            Assert.IsFalse(Directory.Exists(paths.CoversDirectory));
            Assert.IsNull(editor.Result);
        }
        finally { editor.Close(); }
        Assert.IsTrue(File.Exists(source));
    }
}
