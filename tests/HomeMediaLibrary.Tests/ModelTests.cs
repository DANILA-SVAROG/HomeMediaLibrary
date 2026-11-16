using HomeMediaLibrary.Core.Models;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class ModelTests
{
    [Test]
    public void FilmAndSeriesUseCommonContractAndDifferentInfo()
    {
        MediaItem film = TestData.Movie();
        MediaItem series = TestData.Series();
        Assert.AreEqual("Фильм", film.TypeName);
        Assert.AreEqual("148 мин.", film.GetShortInfo());
        Assert.AreEqual("Сезонов: 3", series.GetShortInfo());
        Assert.AreEqual(WatchStatus.Planned, film.Status);
        Assert.IsFalse(film.Purchased);
    }

    [Test]
    public void ConstructorTrimsFieldsAndCopiesGenres()
    {
        var data = TestData.Details("  Фильм  ", " Автор ");
        data.Genres = new() { " Драма ", "драма", "", " Комедия" };
        var film = new Movie(Guid.NewGuid(), data);
        data.Genres.Clear();
        Assert.AreEqual("Фильм", film.Title);
        Assert.AreEqual("Автор", film.Author);
        CollectionAssert.AreEqual(new[] { "Драма", "Комедия" }, film.Genres);
        Assert.IsTrue(film.GetShortInfo().Contains("не указана"));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void EmptyTitlesAreRejected(string title)
    {
        Assert.Throws<ArgumentException>(() => new Movie(Guid.NewGuid(), TestData.Details(title)));
    }

    [TestCase(999)]
    [TestCase(10000)]
    [TestCase(0)]
    [TestCase(-2020)]
    public void IncorrectYearsAreRejected(int year)
        => Assert.Throws<ArgumentException>(() => TestData.Movie(year: year));

    [TestCase(1000)]
    [TestCase(9999)]
    public void FourDigitYearsAreAllowed(int year) => Assert.AreEqual(year, TestData.Movie(year: year).Year);

    [TestCase(-1)]
    [TestCase(0)]
    public void InvalidDurationAndSeasonCountAreRejected(int value)
    {
        Assert.Throws<ArgumentException>(() => new Movie(Guid.NewGuid(), TestData.Details("Кино"), value));
        Assert.Throws<ArgumentException>(() => new Series(Guid.NewGuid(), TestData.Details("Кино"), value));
    }

    [Test]
    public void OptionalFieldsAndNumbersCanBeEmpty()
    {
        var movie = new Movie(Guid.NewGuid(), new MediaDetails { Title = "Фильм" });
        var series = new Series(Guid.NewGuid(), new MediaDetails { Title = "Сериал" });
        Assert.IsNull(movie.Year);
        Assert.IsNull(movie.DurationMinutes);
        Assert.IsNull(series.Seasons);
        Assert.IsEmpty(movie.Genres);
    }

    [Test]
    public void MarksAreIndependentAndNotifyBindings()
    {
        var film = TestData.Movie();
        var changed = new List<string?>();
        film.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        film.SetMarks(WatchStatus.Planned, true);
        Assert.IsTrue(film.Purchased);
        Assert.AreEqual(WatchStatus.Planned, film.Status);
        film.SetMarks(WatchStatus.Watched, true);
        CollectionAssert.AreEquivalent(new[] { "Purchased", "PurchaseText", "Status", "StatusText" }, changed);
        film.SetMarks(WatchStatus.Watched, false);
        Assert.AreEqual(WatchStatus.Watched, film.Status);
    }

    [TestCase("https://www.imdb.com/title/tt1375666/", true)]
    [TestCase("https://imdb.com/title/tt1/", true)]
    [TestCase("HTTPS://WWW.IMDB.COM/title/tt1/", true)]
    [TestCase("http://imdb.com/title/tt1/", false)]
    [TestCase("https://imdb.com.evil.example/title/", false)]
    [TestCase("https://user@imdb.com/title/", false)]
    [TestCase("https://imdb.com:8080/title/", false)]
    [TestCase("https://m.imdb.com/title/", false)]
    [TestCase("file:///C:/film.html", false)]
    [TestCase("не ссылка", false)]
    public void OnlyHttpsImdbAddressesAreAccepted(string url, bool valid)
        => Assert.AreEqual(valid, MediaValidation.IsImdbUrl(url));

    [Test]
    public void InvalidStatusAndIdAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Movie(Guid.Empty, TestData.Details("Фильм")));
        Assert.Throws<ArgumentException>(() => TestData.Movie(status: (WatchStatus)100));
        Assert.Throws<ArgumentException>(() => TestData.Movie().SetMarks((WatchStatus)100, true));
    }
}
