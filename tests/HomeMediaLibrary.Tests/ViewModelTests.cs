using System.IO;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.ViewModels;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class ViewModelTests
{
    private MemoryRepository repo = null!;
    private LibraryController library = null!;
    private LibraryViewModel model = null!;
    [SetUp]
    public void Setup()
    {
        repo = new MemoryRepository { Data = new() { TestData.Movie(), TestData.Series() } };
        library = new LibraryController(repo, Path.Combine(Path.GetTempPath(), "library.json"));
        library.Load();
        model = new LibraryViewModel(library);
    }
    [TearDown]
    public void Cleanup() => model.Dispose();

    [Test]
    public void ResetRestoresAllFiltersAndSort()
    {
        model.Search = "Начало";
        model.SelectedYear = model.Years.Single(option => Equals(option.Value, 2010));
        model.SelectedKind = model.Kinds[1];
        model.SelectedStatus = model.Statuses[1];
        model.SelectedPurchase = model.Purchases[2];
        model.SelectedSort = model.Sorts[5];
        Assert.AreEqual(1, model.VisibleItems.Count);
        model.ResetFilters();
        Assert.AreEqual(2, model.VisibleItems.Count);
        Assert.IsEmpty(model.Search);
        Assert.IsNull(model.SelectedYear!.Value);
        Assert.IsNull(model.SelectedKind!.Value);
        Assert.IsNull(model.SelectedStatus!.Value);
        Assert.IsNull(model.SelectedPurchase!.Value);
        Assert.AreSame(model.Sorts[0], model.SelectedSort);
    }

    [Test]
    public void MarkChangeImmediatelyReappliesFilters()
    {
        var item = library.Items[0];
        model.SelectedStatus = model.Statuses[1];
        model.SelectedItem = item;
        library.ChangeMarks(item.Id, WatchStatus.Watched, false);
        Assert.IsFalse(model.VisibleItems.Contains(item));
        Assert.IsNull(model.SelectedItem);
        model.SelectedStatus = model.Statuses[2];
        Assert.AreSame(item, model.VisibleItems.Single());
    }

    [Test]
    public void OptionsFollowAddsEditsAndDeletes()
    {
        var data = TestData.Details("Новая запись", year: 1995);
        data.Genres = new() { "Комедия" };
        var item = new Movie(Guid.NewGuid(), data);
        library.Add(item);
        Assert.IsTrue(model.Genres.Any(option => Equals(option.Value, "Комедия")));
        Assert.IsTrue(model.Years.Any(option => Equals(option.Value, 1995)));
        library.Delete(item.Id);
        Assert.IsFalse(model.Genres.Any(option => Equals(option.Value, "Комедия")));
        Assert.IsFalse(model.Years.Any(option => Equals(option.Value, 1995)));
    }

    [Test]
    public void SelectionAndImdbAvailabilityFollowChosenRecord()
    {
        Assert.IsFalse(model.CanEditSelection);
        var film = library.Items[0];
        var data = film.GetDetails();
        data.ImdbUrl = "https://www.imdb.com/title/tt1375666/";
        var changed = film.Copy(data);
        library.Update(changed);
        model.SelectedItem = changed;
        Assert.IsTrue(model.CanOpenImdb);
        Assert.IsTrue(model.CanEditSelection);
        model.Search = "несуществующая запись";
        Assert.IsTrue(model.IsEmpty);
        Assert.IsFalse(model.CanOpenImdb);
        StringAssert.Contains("ничего не найдено", model.EmptyText);
    }
}
