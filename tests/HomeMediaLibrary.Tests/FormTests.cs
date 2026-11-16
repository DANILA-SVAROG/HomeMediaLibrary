using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.ViewModels;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class FormTests
{
    [TestCase("Year", "99")]
    [TestCase("Year", "0202")]
    [TestCase("Year", "+2020")]
    [TestCase("Year", "2020.0")]
    [TestCase("Year", "20201")]
    [TestCase("Duration", "0")]
    [TestCase("Duration", "1.5")]
    [TestCase("Duration", "-20")]
    [TestCase("Duration", "много")]
    [TestCase("Seasons", "0")]
    [TestCase("Seasons", "2147483648")]
    public void InvalidInputCannotBecomeRecord(string field, string text)
    {
        var input = new MediaFormInput { Title = "Фильм" };
        typeof(MediaFormInput).GetProperty(field)!.SetValue(input, text);
        if (field == "Seasons") input.Kind = 1;
        Assert.Throws<ArgumentException>(() => input.CreateItem(Guid.NewGuid()));
    }

    [Test]
    public void EmptyOptionalFieldsWorkAndHiddenFieldIsIgnored()
    {
        var film = (Movie)new MediaFormInput { Title = "  Название  ", Seasons = "не число" }.CreateItem(Guid.NewGuid());
        Assert.AreEqual("Название", film.Title);
        Assert.IsNull(film.DurationMinutes);
        var series = (Series)new MediaFormInput { Title = "Сериал", Kind = 1, Duration = "не число" }.CreateItem(Guid.NewGuid());
        Assert.IsNull(series.Seasons);
    }

    [Test]
    public void GenresSplitByCommaOrSemicolonAndTypeKeepsId()
    {
        Guid id = Guid.NewGuid();
        var series = new MediaFormInput { Title = "Сериал", Kind = 1, Year = "2020", Genres = "Драма, Детектив; драма",
            Seasons = "3", Status = 1, Purchased = true }.CreateItem(id);
        Assert.AreEqual(id, series.Id);
        CollectionAssert.AreEqual(new[] { "Драма", "Детектив" }, series.Genres);
        Assert.IsTrue(series.Purchased);
        Assert.AreEqual(WatchStatus.Watched, series.Status);
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(3, 0)]
    public void UnknownTypeAndStatusAreRejected(int kind, int status)
        => Assert.Throws<ArgumentException>(() => new MediaFormInput { Title = "Кино", Kind = kind, Status = status }.CreateItem(Guid.NewGuid()));
}
