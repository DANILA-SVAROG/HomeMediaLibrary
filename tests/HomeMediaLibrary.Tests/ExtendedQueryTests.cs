using System.Globalization;
using HomeMediaLibrary.Core.Filtering;
using HomeMediaLibrary.Core.Models;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class ExtendedQueryTests
{
    [Test]
    public void EveryCombinationOfFiltersAndSortsMatchesReferenceSelection()
    {
        var items = new List<MediaItem>();
        for (int i = 0; i < 48; i++)
        {
            var data = TestData.Details((i % 2 == 0 ? "Звезда " : "Альфа ") + i,
                i % 3 == 0 ? "Анна" : "Борис", i % 4 == 0 ? null : 2000 + i % 3,
                i % 5 == 0 ? WatchStatus.Watched : WatchStatus.Planned, i % 7 == 0);
            data.Genres = i % 3 == 0 ? new() { "Драма", "Комедия" } : new() { "Фантастика" };
            items.Add(i % 2 == 0 ? new Movie(Guid.NewGuid(), data) : new Series(Guid.NewGuid(), data));
        }
        int combinations = 0;
        foreach (string search in new[] { "", " звЕЗ ", "АННА", "Бор", "нет", "Альфа" })
        foreach (string? genre in new[] { null, "дРАМа", "Фантастика", "Нет жанра" })
        foreach (int? year in new int?[] { null, 2000, 2002, 1900 })
        foreach (MediaKind? kind in new MediaKind?[] { null, MediaKind.Movie, MediaKind.Series })
        foreach (WatchStatus? status in new WatchStatus?[] { null, WatchStatus.Planned, WatchStatus.Watched })
        foreach (bool? purchased in new bool?[] { null, false, true })
        foreach (LibrarySort sort in Enum.GetValues<LibrarySort>())
        {
            var expected = new List<MediaItem>();
            foreach (var item in items)
            {
                string text = search.Trim();
                if (text.Length > 0 && item.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
                    && item.Author.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (genre is not null && !item.Genres.Any(value => value.Equals(genre, StringComparison.OrdinalIgnoreCase))) continue;
                if (year.HasValue && item.Year != year) continue;
                if (kind == MediaKind.Movie && item is not Movie || kind == MediaKind.Series && item is not Series) continue;
                if (status.HasValue && item.Status != status || purchased.HasValue && item.Purchased != purchased) continue;
                expected.Add(item);
            }
            expected.Sort((left, right) => Compare(left, right, sort));
            var actual = LibraryQuery.Apply(items, new() { Search = search, Genre = genre, Year = year,
                Kind = kind, Status = status, Purchased = purchased, Sort = sort });
            CollectionAssert.AreEqual(expected.Select(item => item.Id), actual.Select(item => item.Id),
                $"Поиск={search}, жанр={genre}, год={year}, тип={kind}, статус={status}, покупка={purchased}, сортировка={sort}");
            combinations++;
        }
        TestContext.WriteLine($"Проверено сочетаний: {combinations}.");
        Assert.AreEqual(15552, combinations);
        Assert.AreEqual(48, items.Count);
    }

    private static int Compare(MediaItem left, MediaItem right, LibrarySort sort)
    {
        int result;
        if (sort is LibrarySort.YearAscending or LibrarySort.YearDescending)
        {
            if (left.Year.HasValue != right.Year.HasValue) return left.Year.HasValue ? -1 : 1;
            result = Nullable.Compare(left.Year, right.Year);
            if (sort == LibrarySort.YearDescending) result = -result;
        }
        else
        {
            bool author = sort is LibrarySort.AuthorAscending or LibrarySort.AuthorDescending;
            result = StringComparer.CurrentCultureIgnoreCase.Compare(author ? left.Author : left.Title, author ? right.Author : right.Title);
            if (sort is LibrarySort.AuthorDescending or LibrarySort.TitleDescending) result = -result;
        }
        if (result == 0) result = StringComparer.CurrentCultureIgnoreCase.Compare(left.Title, right.Title);
        return result == 0 ? left.Id.CompareTo(right.Id) : result;
    }

    [TestCase("ru-RU")]
    [TestCase("en-US")]
    [TestCase("tr-TR")]
    public void SearchRemainsCaseInsensitiveAndSortUsesCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            MediaItem[] items = { TestData.Movie("Istanbul"), TestData.Series("ёж"), TestData.Movie("Яблоко"), TestData.Movie("Альфа") };
            Assert.AreEqual("Istanbul", LibraryQuery.Apply(items, new() { Search = "ISTANBUL" }).Single().Title);
            CollectionAssert.AreEqual(items.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase), LibraryQuery.Apply(items, new()));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
