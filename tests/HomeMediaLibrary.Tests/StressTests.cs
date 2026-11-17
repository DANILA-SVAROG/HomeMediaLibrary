using System.Diagnostics;
using System.IO;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Filtering;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using HomeMediaLibrary.ViewModels;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class StressTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-stress-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);

    [Test]
    public void FiveHundredRandomChangesSurviveSavingExportAndRestart()
    {
        string path = Path.Combine(folder, "Данные 🎬", "library.json");
        string export = Path.Combine(folder, "export.json");
        var library = new LibraryController(new JsonLibraryRepository(), path);
        library.Load();
        var expected = new List<MediaItem>();
        var random = new Random(602511);
        for (int step = 0; step < 500; step++)
        {
            int action = expected.Count == 0 ? 0 : random.Next(5);
            int index = expected.Count == 0 ? 0 : random.Next(expected.Count);
            if (action == 0)
            {
                var data = TestData.Details($"Кино {step} 🎬 \"цитата\" \\ путь\nстрока", "Режиссёр 日本語", step % 2 == 0 ? 1000 : 9999);
                data.Description = new string('Я', 1000) + "\t\r\nКонец";
                data.Genres = new() { "Драма", " дРАМа ", "科幻" };
                data.ImdbUrl = "https://www.imdb.com/title/tt1234567/";
                MediaItem item = step % 2 == 0 ? new Movie(Guid.NewGuid(), data, int.MaxValue) : new Series(Guid.NewGuid(), data, null);
                library.Add(item); expected.Add(item);
            }
            else if (action == 1)
            {
                var old = expected[index];
                var data = old.GetDetails(); data.Title = "Новое название " + step; data.Year = null;
                MediaItem item = old is Movie ? new Series(old.Id, data, 5) : new Movie(old.Id, data, null);
                library.Update(item); expected[index] = item;
            }
            else if (action == 2)
            {
                var old = expected[index]; var data = old.GetDetails();
                data.Status = old.Status == WatchStatus.Planned ? WatchStatus.Watched : WatchStatus.Planned;
                data.Purchased = !old.Purchased;
                var copy = old.Copy(data);
                library.ChangeMarks(old.Id, data.Status, data.Purchased); expected[index] = copy;
            }
            else if (action == 3) { library.Delete(expected[index].Id); expected.RemoveAt(index); }
            else library.Export(export);
            AssertCollection(expected, new JsonLibraryRepository().Load(path));
            AssertCollection(expected, library.Items);
            if (step % 25 == 0)
            {
                library = new LibraryController(new JsonLibraryRepository(), path); library.Load();
                AssertCollection(expected, library.Items);
            }
        }
        library.Export(export);
        AssertCollection(expected, new JsonLibraryRepository().Load(export));
        Assert.IsEmpty(Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories));
        TestContext.WriteLine("500 изменений, 20 перезапусков, проверка всех полей после каждого действия.");
    }

    public static void AssertCollection(IEnumerable<MediaItem> expected, IEnumerable<MediaItem> actual)
    {
        CollectionAssert.AreEqual(expected.Select(Snapshot), actual.Select(Snapshot));
    }

    private static object Snapshot(MediaItem item) => new { Type = item.GetType(), item.Id, item.Title, item.Author,
        item.Year, Genres = string.Join("|", item.Genres), item.Description, item.ImdbUrl, item.CoverPath,
        item.Status, item.Purchased, Number = item is Movie movie ? movie.DurationMinutes : ((Series)item).Seasons };

    [Test]
    public void TenThousandRecordsSaveLoadAndFilterWithoutLosingData()
    {
        var items = Enumerable.Range(0, 10000).Select(i => (MediaItem)TestData.Movie("Фильм " + i, "Автор " + i % 50, 2000 + i % 25)).ToList();
        var watch = Stopwatch.StartNew();
        string path = Path.Combine(folder, "large.json");
        var repo = new JsonLibraryRepository(); repo.Save(items, path);
        var loaded = repo.Load(path);
        AssertCollection(items, loaded);
        for (int i = 0; i < 100; i++)
        {
            var result = LibraryQuery.Apply(loaded, new() { Year = 2000 + i % 25, Sort = (LibrarySort)(i % 6) });
            Assert.AreEqual(400, result.Count);
        }
        TestContext.WriteLine($"10 000 записей и 100 фильтраций: {watch.ElapsedMilliseconds} мс; JSON: {new FileInfo(path).Length} байт.");
    }

    [Test]
    public void LoadingLargeCollectionRefreshesBoundListOnlyOnce()
    {
        var repo = new MemoryRepository { Data = Enumerable.Range(0, 2000).Select(i => (MediaItem)TestData.Movie("Фильм " + i)).ToList() };
        var library = new LibraryController(repo, Path.Combine(folder, "library.json"));
        var model = new LibraryViewModel(library);
        int resets = 0;
        model.VisibleItems.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
        var watch = Stopwatch.StartNew();
        try
        {
            library.Load();
            Assert.AreEqual(2000, model.VisibleItems.Count);
            TestContext.WriteLine($"Загрузка модели: {watch.ElapsedMilliseconds} мс, обновлений списка: {resets}.");
            Assert.AreEqual(1, resets, "Полная перестройка списка для каждой загруженной записи замедляет запуск.");
        }
        finally { model.Dispose(); }
    }
}
