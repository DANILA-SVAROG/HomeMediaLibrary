using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class WindowRobustnessTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-window-extra-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);
    private static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);

    [Test]
    public void ConflictingWindowShowsReloadButtonAndRecoversWithoutLosingSavedRecord()
    {
        var paths = new AppPaths(folder);
        var first = new MainWindow(paths);
        var second = new MainWindow(paths);
        try
        {
            WindowTests.Show(second);
            first.SaveItem(TestData.Movie("Сохранено в первом окне"), null, false);
            Assert.Throws<LibraryConflictException>(() => second.SaveItem(TestData.Series(), null, false));
            WindowTests.Flush(second);
            Assert.IsTrue(second.Model.HasLoadError);
            Assert.IsFalse(Control<Button>(second, "AddButton").IsEnabled);
            Assert.IsTrue(Control<Button>(second, "ReloadButton").IsVisible);
            Assert.AreEqual(1, new JsonLibraryRepository().Load(paths.LibraryFile).Count);
            WindowTests.Render(second, "09-file-conflict");
            Control<Button>(second, "ReloadButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WindowTests.Flush(second);
            Assert.IsFalse(second.Model.HasLoadError);
            Assert.IsTrue(Control<Button>(second, "AddButton").IsEnabled);
            Assert.AreEqual("Сохранено в первом окне", second.Library.Items.Single().Title);
            second.SaveItem(TestData.Series(), null, false);
            Assert.AreEqual(2, new JsonLibraryRepository().Load(paths.LibraryFile).Count);
        }
        finally { first.Close(); second.Close(); }
    }

    [Test]
    public void EditingKeepsBoundGenreYearSearchSortAndSelection()
    {
        var paths = new AppPaths(folder);
        var repo = new MemoryRepository { Data = new() { TestData.Movie("Начало"), TestData.Movie("Другой фильм") } };
        var window = new MainWindow(paths, repo);
        try
        {
            WindowTests.Show(window);
            Control<TextBox>(window, "SearchBox").Text = "Начало";
            Control<ComboBox>(window, "GenreFilter").SelectedItem = window.Model.Genres.Single(option => Equals(option.Value, "Драма"));
            Control<ComboBox>(window, "YearFilter").SelectedItem = window.Model.Years.Single(option => Equals(option.Value, 2010));
            Control<ComboBox>(window, "SortFilter").SelectedIndex = 5;
            var original = window.Library.Items[0];
            window.Model.SelectedItem = original;
            var data = original.GetDetails(); data.Title = "Начало: изменено";
            window.SaveItem(original.Copy(data), null, true);
            WindowTests.Flush(window);
            Assert.Multiple(() =>
            {
                Assert.AreEqual("Драма", window.Model.SelectedGenre!.Value);
                Assert.AreEqual(2010, window.Model.SelectedYear!.Value);
                Assert.AreEqual("Начало", Control<TextBox>(window, "SearchBox").Text);
                Assert.AreEqual(5, Control<ComboBox>(window, "SortFilter").SelectedIndex);
                Assert.AreEqual(1, window.Model.VisibleItems.Count);
                Assert.AreEqual(original.Id, window.Model.SelectedItem!.Id);
                Assert.AreEqual(original.Id, Control<ListBox>(window, "Catalog").SelectedValue is MediaItem item ? item.Id : Guid.Empty);
            });
        }
        finally { window.Close(); }
    }

    [Test]
    public void ThousandCardsCanBeLoadedFilteredSelectedAndScrolledToEnd()
    {
        var repo = new MemoryRepository { Data = Enumerable.Range(0, 1000).Select(i => (MediaItem)TestData.Movie("Фильм " + i.ToString("D4"))).ToList() };
        var watch = Stopwatch.StartNew();
        var window = new MainWindow(new AppPaths(folder), repo);
        try
        {
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            WindowTests.Show(window);
            var catalog = Control<ListBox>(window, "Catalog");
            Assert.AreEqual(1000, catalog.Items.Count);
            catalog.SelectedIndex = 999;
            catalog.ScrollIntoView(catalog.SelectedItem);
            WindowTests.Flush(window);
            Assert.AreEqual("Фильм 0999", window.Model.SelectedItem!.Title);
            TestContext.WriteLine($"Создание окна с 1000 карточками и прокрутка: {watch.ElapsedMilliseconds} мс.");
            Control<TextBox>(window, "SearchBox").Text = "0999";
            WindowTests.Flush(window);
            Assert.AreEqual(1, catalog.Items.Count);
            Assert.AreEqual("Фильм 0999", window.Model.SelectedItem!.Title);
            window.Model.ResetFilters(); WindowTests.Flush(window);
            Assert.AreEqual(1000, catalog.Items.Count);
        }
        finally { window.Close(); }
    }

    [Test]
    public void LongTextAndMissingCoverFitMinimumWindowAndEditorButtonsStayVisible()
    {
        var data = TestData.Details(new string('Н', 300), new string('А', 500), 9999);
        data.Description = string.Join("\n", Enumerable.Repeat("Очень длинное описание. 🎬 日本語", 300));
        data.CoverPath = Path.Combine(folder, "Нет такого файла.png");
        data.Genres = Enumerable.Range(0, 100).Select(i => "Жанр " + i).ToList();
        var repo = new MemoryRepository { Data = new() { new Movie(Guid.NewGuid(), data, int.MaxValue) } };
        var main = new MainWindow(new AppPaths(folder), repo);
        var editor = new EditorWindow(repo.Data[0]);
        try
        {
            main.Width = main.MinWidth; main.Height = main.MinHeight;
            main.Model.SelectedItem = main.Library.Items[0]; WindowTests.Show(main);
            WindowTests.Render(main, "10-long-text-minimum");
            editor.Width = editor.MinWidth; editor.Height = editor.MinHeight;
            WindowTests.Show(editor);
            var button = Control<Button>(editor, "SaveButton");
            var content = (FrameworkElement)editor.Content;
            var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
            Assert.GreaterOrEqual(bounds.Top, 0);
            Assert.LessOrEqual(bounds.Bottom, content.ActualHeight);
            Assert.LessOrEqual(bounds.Right, content.ActualWidth);
            Assert.IsTrue(Control<TextBox>(editor, "TitleBox").IsEnabled);
            Assert.AreEqual(data.Description, Control<TextBox>(editor, "DescriptionBox").Text);
            WindowTests.Render(editor, "11-editor-long-text-minimum");
        }
        finally { editor.Close(); main.Close(); }
    }
}
