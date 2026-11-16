using HomeMediaLibrary.Core.Filtering;
using HomeMediaLibrary.Core.Models;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class QueryTests
{
    private List<MediaItem> items = null!;
    [SetUp]
    public void Setup()
    {
        items = new() { TestData.Movie("Бета", "Яков", 2010, WatchStatus.Watched, true),
            TestData.Series("Альфа", "Анна", 2020), TestData.Movie("Гамма", "Борис", null) };
    }

    [TestCase("бЕт", "Бета")]
    [TestCase("аНН", "Альфа")]
    [TestCase(" Борис ", "Гамма")]
    public void SearchUsesTitleOrAuthorIgnoringCase(string text, string expected)
        => Assert.AreEqual(expected, LibraryQuery.Apply(items, new() { Search = text }).Single().Title);

    [Test]
    public void AllFiltersWorkTogether()
    {
        var filter = new LibraryFilter { Search = "як", Genre = "дРАМа", Year = 2010,
            Kind = MediaKind.Movie, Status = WatchStatus.Watched, Purchased = true };
        Assert.AreEqual("Бета", LibraryQuery.Apply(items, filter).Single().Title);
        filter.Purchased = false;
        Assert.IsEmpty(LibraryQuery.Apply(items, filter));
        Assert.AreEqual(3, LibraryQuery.Apply(items, new()).Count);
    }

    [TestCase(LibrarySort.TitleAscending, "Альфа,Бета,Гамма")]
    [TestCase(LibrarySort.TitleDescending, "Гамма,Бета,Альфа")]
    [TestCase(LibrarySort.AuthorAscending, "Альфа,Гамма,Бета")]
    [TestCase(LibrarySort.AuthorDescending, "Бета,Гамма,Альфа")]
    [TestCase(LibrarySort.YearAscending, "Бета,Альфа,Гамма")]
    [TestCase(LibrarySort.YearDescending, "Альфа,Бета,Гамма")]
    public void SortUsesRequestedFieldAndDirection(LibrarySort sort, string expected)
        => Assert.AreEqual(expected, string.Join(",", LibraryQuery.Apply(items, new() { Sort = sort }).Select(item => item.Title)));

    [Test]
    public void SortAppliesAfterFilteringAndKeepsSourceUnchanged()
    {
        var result = LibraryQuery.Apply(items, new() { Kind = MediaKind.Movie, Sort = LibrarySort.TitleDescending });
        CollectionAssert.AreEqual(new[] { "Гамма", "Бета" }, result.Select(item => item.Title));
        CollectionAssert.AreEqual(new[] { "Бета", "Альфа", "Гамма" }, items.Select(item => item.Title));
    }

    [Test]
    public void PurchaseFilterDoesNotImplyWatchedStatus()
    {
        var film = TestData.Movie(purchased: true);
        var result = LibraryQuery.Apply(new[] { film }, new() { Status = WatchStatus.Planned, Purchased = true });
        Assert.AreSame(film, result.Single());
    }
}
