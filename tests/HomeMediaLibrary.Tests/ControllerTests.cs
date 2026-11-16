using System.IO;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class ControllerTests
{
    private static LibraryController Create(MemoryRepository repo)
    {
        var controller = new LibraryController(repo, Path.Combine(Path.GetTempPath(), "test-library.json"));
        controller.Load();
        return controller;
    }

    [Test]
    public void AddEditAndDeleteAlwaysSaveBeforeChangingCollection()
    {
        var repository = new MemoryRepository();
        var controller = Create(repository);
        var film = TestData.Movie();
        controller.Add(film);
        var details = film.GetDetails();
        details.Description = "Новое описание";
        controller.Update(film.Copy(details));
        Assert.AreEqual("Новое описание", controller.Items[0].Description);
        Assert.AreEqual(film.Id, controller.Items[0].Id);
        controller.Delete(film.Id);
        Assert.IsEmpty(controller.Items);
        Assert.AreEqual(3, repository.Saved);
    }

    [Test]
    public void SaveFailureDoesNotChangeItemsOrMarks()
    {
        var film = TestData.Movie();
        var repository = new MemoryRepository { Data = new() { film }, FailSave = true };
        var controller = Create(repository);
        Assert.Throws<IOException>(() => controller.Add(TestData.Series()));
        Assert.Throws<IOException>(() => controller.Delete(film.Id));
        Assert.Throws<IOException>(() => controller.Update(film.Copy(TestData.Details("Другое название"))));
        Assert.Throws<IOException>(() => controller.ChangeMarks(film.Id, WatchStatus.Watched, true));
        Assert.AreEqual(1, controller.Items.Count);
        Assert.AreSame(film, controller.Items[0]);
        Assert.AreEqual(WatchStatus.Planned, film.Status);
        Assert.IsFalse(film.Purchased);
    }

    [Test]
    public void FailureToLoadBlocksChangesUntilSuccessfulRetry()
    {
        var repository = new MemoryRepository { FailLoad = true };
        var controller = new LibraryController(repository, "library.json");
        Assert.Throws<IOException>(() => controller.Load());
        Assert.IsFalse(controller.IsReady);
        Assert.Throws<InvalidOperationException>(() => controller.Add(TestData.Movie()));
        Assert.Throws<InvalidOperationException>(() => controller.Export("export.json"));
        repository.FailLoad = false;
        controller.Load();
        controller.Add(TestData.Movie());
        Assert.IsTrue(controller.IsReady);
    }

    [Test]
    public void UnknownAndDuplicateIdsAreRejectedBeforeSave()
    {
        var film = TestData.Movie();
        var repository = new MemoryRepository { Data = new() { film } };
        var controller = Create(repository);
        Assert.Throws<ArgumentException>(() => controller.Add(film));
        Assert.Throws<ArgumentException>(() => controller.Delete(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => controller.Update(TestData.Movie()));
        Assert.AreEqual(0, repository.Saved);
    }

    [Test]
    public void ExportAlwaysContainsWholeLibrary()
    {
        var repository = new MemoryRepository { Data = new() { TestData.Movie(), TestData.Series() } };
        var controller = Create(repository);
        controller.Export("export.json");
        Assert.AreEqual(2, repository.Data.Count);
        Assert.Throws<ArgumentException>(() => controller.Export(controller.FilePath));
    }

    [Test]
    public void BrokenJsonCannotBeOverwrittenByEmptyCollection()
    {
        string folder = Directory.CreateTempSubdirectory("media-broken-").FullName;
        try
        {
            string path = Path.Combine(folder, "library.json");
            File.WriteAllText(path, "{ повреждённый файл");
            var controller = new LibraryController(new JsonLibraryRepository(), path);
            Assert.Catch(() => controller.Load());
            Assert.Throws<InvalidOperationException>(() => controller.Add(TestData.Movie()));
            Assert.AreEqual("{ повреждённый файл", File.ReadAllText(path));
        }
        finally { Directory.Delete(folder, true); }
    }
}
