using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class FileSafetyTests
{
    private string folder = null!;
    private string path = null!;
    [SetUp]
    public void Setup()
    {
        folder = Directory.CreateTempSubdirectory("medialibrary-safety-").FullName;
        path = Path.Combine(folder, "library.json");
    }
    [TearDown]
    public void Cleanup()
    {
        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(folder, true);
    }

    [TestCase("Year", "999")]
    [TestCase("Year", "10000")]
    [TestCase("Year", "1.5")]
    [TestCase("Year", "\"2010\"")]
    [TestCase("Title", "null")]
    [TestCase("Title", "\"   \"")]
    [TestCase("Title", "123")]
    [TestCase("Genres", "null")]
    [TestCase("Genres", "[null]")]
    [TestCase("Genres", "[123]")]
    [TestCase("Genres", "\"Драма\"")]
    [TestCase("Status", "\"Unknown\"")]
    [TestCase("Status", "0")]
    [TestCase("Status", "null")]
    [TestCase("Purchased", "\"true\"")]
    [TestCase("Purchased", "null")]
    [TestCase("DurationMinutes", "-1")]
    [TestCase("DurationMinutes", "0")]
    [TestCase("DurationMinutes", "2147483648")]
    [TestCase("DurationMinutes", "1.5")]
    [TestCase("DurationMinutes", "\"10\"")]
    [TestCase("Id", "\"00000000-0000-0000-0000-000000000000\"")]
    [TestCase("Id", "null")]
    [TestCase("$type", "null")]
    [TestCase("$type", "123")]
    [TestCase("ImdbUrl", "\"javascript:alert(1)\"")]
    [TestCase("ImdbUrl", "\"https://imdb.com.evil.example/title/tt1/\"")]
    public void InvalidRecordFieldsAreRejectedWithoutChangingTheFile(string field, string value)
    {
        var repo = new JsonLibraryRepository(); repo.Save(new[] { TestData.Movie() }, path);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        root["Items"]![0]![field] = JsonNode.Parse(value);
        File.WriteAllText(path, root.ToJsonString());
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Catch<JsonException>(() => repo.Load(path));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
    }

    [Test]
    public void MissingDiscriminatorIdAndTitleAreRejected()
    {
        foreach (string field in new[] { "$type", "Id", "Title" })
        {
            new JsonLibraryRepository().Save(new[] { TestData.Movie() }, path);
            var root = JsonNode.Parse(File.ReadAllText(path))!;
            root["Items"]![0]!.AsObject().Remove(field);
            File.WriteAllText(path, root.ToJsonString());
            Assert.Catch<JsonException>(() => new JsonLibraryRepository().Load(path), field);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BomFilesLoadAndResaveAsUtf8(bool utf16)
    {
        var repo = new JsonLibraryRepository(); repo.Save(new[] { TestData.Movie() }, path);
        File.WriteAllText(path, File.ReadAllText(path), utf16 ? Encoding.Unicode : new UTF8Encoding(true));
        var loaded = repo.Load(path); repo.Save(loaded, path);
        Assert.AreEqual("Начало", loaded.Single().Title);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual((byte)'{', bytes[0]);
        StringAssert.Contains("Начало", new UTF8Encoding(false, true).GetString(bytes));
    }

    [Test]
    public void BrokenUtf8IsRejectedInsteadOfSilentlyChangingText()
    {
        new JsonLibraryRepository().Save(new[] { TestData.Movie("ABC") }, path);
        byte[] bytes = File.ReadAllBytes(path);
        int position = Encoding.UTF8.GetString(bytes).IndexOf("ABC", StringComparison.Ordinal);
        bytes[position] = 0xFF;
        File.WriteAllBytes(path, bytes);
        Assert.Catch<JsonException>(() => new JsonLibraryRepository().Load(path));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
    }

    [Test]
    public void ReadOnlyLibraryKeepsBothFileAndCollectionOnSaveFailure()
    {
        var library = new LibraryController(new JsonLibraryRepository(), path);
        library.Load(); library.Add(TestData.Movie());
        byte[] original = File.ReadAllBytes(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        Assert.Catch<UnauthorizedAccessException>(() => library.Add(TestData.Series()));
        Assert.AreEqual(1, library.Items.Count);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        Assert.AreEqual(1, Directory.GetFiles(folder).Length);
        File.SetAttributes(path, FileAttributes.Normal);
        library.Add(TestData.Series());
        Assert.AreEqual(2, new JsonLibraryRepository().Load(path).Count);
    }

    [Test]
    public void ExportFailureLeavesWorkingLibraryAndDestinationIntact()
    {
        var library = new LibraryController(new JsonLibraryRepository(), path);
        library.Load(); library.Add(TestData.Movie());
        string target = Path.Combine(folder, "export.json");
        File.WriteAllText(target, "Старый экспорт");
        byte[] original = File.ReadAllBytes(path);
        using (var locked = File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Catch<IOException>(() => library.Export(target));
        Assert.AreEqual("Старый экспорт", File.ReadAllText(target));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        Assert.AreEqual(1, library.Items.Count);
        library.Export(target);
        Assert.AreEqual(1, new JsonLibraryRepository().Load(target).Count);
        Assert.AreEqual(2, Directory.GetFiles(folder).Length);
    }

    [Test]
    public void FailedLoadKeepsPreviousCollectionAndBlocksChangesUntilRetry()
    {
        var library = new LibraryController(new JsonLibraryRepository(), path);
        library.Load(); library.Add(TestData.Movie());
        string valid = File.ReadAllText(path);
        File.WriteAllText(path, "{bad");
        Assert.Catch<JsonException>(() => library.Load());
        Assert.AreEqual(1, library.Items.Count);
        Assert.IsFalse(library.IsReady);
        Assert.Throws<InvalidOperationException>(() => library.Delete(library.Items[0].Id));
        File.WriteAllText(path, valid); library.Load();
        Assert.IsTrue(library.IsReady);
        Assert.AreEqual(1, library.Items.Count);
    }

    [Test]
    public void TwoOpenLibrariesCannotSilentlyOverwriteEachOther()
    {
        var first = new LibraryController(new JsonLibraryRepository(), path);
        var second = new LibraryController(new JsonLibraryRepository(), path);
        first.Load(); second.Load(); first.Add(TestData.Movie("Из первого окна"));
        byte[] saved = File.ReadAllBytes(path);
        Assert.Catch<IOException>(() => second.Add(TestData.Series("Из второго окна")));
        Assert.IsEmpty(second.Items);
        CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
        second.Load(); second.Add(TestData.Series("Из второго окна"));
        Assert.AreEqual(2, new JsonLibraryRepository().Load(path).Count);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FileChangedOrDeletedOutsideProgramIsNotOverwritten(bool deleted)
    {
        var library = new LibraryController(new JsonLibraryRepository(), path);
        library.Load(); library.Add(TestData.Movie());
        if (deleted) File.Delete(path); else File.WriteAllText(path, "Повреждено другой программой");
        Assert.Catch<IOException>(() => library.ChangeMarks(library.Items[0].Id, WatchStatus.Watched, true));
        Assert.AreEqual(WatchStatus.Planned, library.Items[0].Status);
        Assert.IsFalse(library.Items[0].Purchased);
        if (deleted) Assert.IsFalse(File.Exists(path));
        else Assert.AreEqual("Повреждено другой программой", File.ReadAllText(path));
    }
}
