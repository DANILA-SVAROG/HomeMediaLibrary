using System.IO;
using System.Text;
using System.Text.Json;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class RepositoryTests
{
    private string folder = null!;
    private string path = null!;
    [SetUp]
    public void Setup()
    {
        folder = Directory.CreateTempSubdirectory("medialibrary-json-").FullName;
        path = Path.Combine(folder, "library.json");
    }
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);

    [Test]
    public void MixedCollectionRestoresEveryFieldAndType()
    {
        var film = TestData.Movie(status: WatchStatus.Watched, purchased: true);
        var details = film.GetDetails();
        details.ImdbUrl = "https://www.imdb.com/title/tt1375666/";
        details.CoverPath = Path.Combine(folder, "Обложка.png");
        film = (Movie)film.Copy(details);
        var series = TestData.Series();
        var repo = new JsonLibraryRepository();
        repo.Save(new MediaItem[] { film, series }, path);
        var loaded = repo.Load(path);
        Assert.IsInstanceOf<Movie>(loaded[0]);
        Assert.IsInstanceOf<Series>(loaded[1]);
        Assert.AreEqual(film.Id, loaded[0].Id);
        Assert.AreEqual(film.Title, loaded[0].Title);
        Assert.AreEqual(film.Author, loaded[0].Author);
        Assert.AreEqual(film.Year, loaded[0].Year);
        CollectionAssert.AreEqual(film.Genres, loaded[0].Genres);
        Assert.AreEqual(film.Description, loaded[0].Description);
        Assert.AreEqual(film.ImdbUrl, loaded[0].ImdbUrl);
        Assert.AreEqual(film.CoverPath, loaded[0].CoverPath);
        Assert.AreEqual(WatchStatus.Watched, loaded[0].Status);
        Assert.IsTrue(loaded[0].Purchased);
        Assert.AreEqual(148, ((Movie)loaded[0]).DurationMinutes);
        Assert.AreEqual(3, ((Series)loaded[1]).Seasons);
        string json = File.ReadAllText(path, Encoding.UTF8);
        StringAssert.Contains("Начало", json);
        StringAssert.Contains("\n", json);
        StringAssert.DoesNotContain("\\u041", json);
    }

    [Test]
    public void EmptyAndMissingLibrariesAreSupported()
    {
        var repo = new JsonLibraryRepository();
        Assert.IsEmpty(repo.Load(path));
        repo.Save(Array.Empty<MediaItem>(), path);
        Assert.IsEmpty(repo.Load(path));
    }

    [TestCase("")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"Version\":2,\"Items\":[]}")]
    [TestCase("{\"Version\":1,\"Items\":[null]}")]
    [TestCase("{\"Version\":1,\"Items\":[{\"$type\":\"Unknown\",\"Id\":\"00000000-0000-0000-0000-000000000001\",\"Title\":\"Кино\"}]}")]
    [TestCase("{\"Version\":1,\"Items\":[{\"$type\":\"Movie\",\"Id\":\"not-id\",\"Title\":\"Кино\"}]}")]
    [TestCase("{\"Version\":1,\"Items\":[{\"$type\":\"Movie\",\"Id\":\"00000000-0000-0000-0000-000000000001\",\"Title\":\"\",\"DurationMinutes\":10}]}")]
    [TestCase("{\"Version\":1,\"Items\":[{\"$type\":\"Series\",\"Id\":\"00000000-0000-0000-0000-000000000001\",\"Title\":\"Кино\",\"Seasons\":0}]}")]
    public void DamagedFileIsRejectedAndNotRewritten(string json)
    {
        File.WriteAllText(path, json);
        Assert.Catch<JsonException>(() => new JsonLibraryRepository().Load(path));
        Assert.AreEqual(json, File.ReadAllText(path));
    }

    [Test]
    public void RepeatedIdsAndNullEntriesCannotBeSaved()
    {
        var repo = new JsonLibraryRepository();
        var film = TestData.Movie();
        Assert.Throws<JsonException>(() => repo.Save(new[] { film, film }, path));
        Assert.Throws<JsonException>(() => repo.Save(new MediaItem[] { null! }, path));
        Assert.IsFalse(File.Exists(path));
    }

    [Test]
    public void RepeatedIdsInFileAreRejected()
    {
        var repo = new JsonLibraryRepository();
        repo.Save(new[] { TestData.Movie() }, path);
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        node["Items"]!.AsArray().Add(node["Items"]![0]!.DeepClone());
        File.WriteAllText(path, node.ToJsonString());
        Assert.Throws<JsonException>(() => repo.Load(path));
    }

    [Test]
    public void AtomicReplacementKeepsOldFileWhenTargetIsLocked()
    {
        var repo = new JsonLibraryRepository();
        repo.Save(new[] { TestData.Movie() }, path);
        string original = File.ReadAllText(path);
        using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Catch<IOException>(() => repo.Save(new[] { TestData.Series() }, path));
        Assert.AreEqual(original, File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(folder).Length);
        repo.Save(new[] { TestData.Series() }, path);
        Assert.IsInstanceOf<Series>(repo.Load(path)[0]);
    }
}
