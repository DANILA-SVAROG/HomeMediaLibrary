using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class WindowTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-window-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);
    private static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);

    public static void Show(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowInTaskbar = false;
        window.Left = -10000; window.Top = -10000;
        window.Show();
        Flush(window);
    }

    public static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        window.UpdateLayout();
    }

    public static void Render(Window window, string name)
    {
        Flush(window);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        string path = Path.Combine(Path.GetTempPath(), "course-medialibrary-previews");
        Directory.CreateDirectory(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(path, name + ".png"));
        encoder.Save(stream);
    }

    public static string MakeCover(string folder, string name = "Обложка.png")
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.DarkSlateBlue, null, new Rect(0, 0, 200, 300));
            drawing.DrawEllipse(Brushes.LightSteelBlue, null, new Point(100, 125), 65, 65);
            drawing.DrawGeometry(Brushes.White, null, Geometry.Parse("M80,90 L80,160 L135,125 Z"));
        }
        var image = new RenderTargetBitmap(200, 300, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        string path = Path.Combine(folder, name);
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    [Test]
    public void EmptyWindowDisablesSelectionActionsAndShowsHelp()
    {
        var window = new MainWindow(new AppPaths(folder));
        try
        {
            Show(window);
            Assert.IsTrue(Control<Button>(window, "AddButton").IsEnabled);
            Assert.IsFalse(Control<Button>(window, "EditButton").IsEnabled);
            Assert.IsFalse(Control<Button>(window, "DeleteButton").IsEnabled);
            Assert.IsFalse(Control<Button>(window, "ImdbButton").IsEnabled);
            Assert.AreEqual(Visibility.Visible, Control<TextBlock>(window, "EmptyMessage").Visibility);
            Render(window, "01-empty");
        }
        finally { window.Close(); }
    }

    [Test]
    public void CatalogEditsFiltersMarksExportsAndRestarts()
    {
        var paths = new AppPaths(folder);
        string source = MakeCover(folder);
        string? opened = null;
        var window = new MainWindow(paths, browser: url => opened = url);
        try
        {
            Show(window);
            var details = TestData.Details("Северный ветер", "Мария Волкова", 2023);
            details.ImdbUrl = "https://www.imdb.com/title/tt1375666/";
            var film = new Movie(Guid.NewGuid(), details, 112);
            window.SaveItem(film, source, false);
            window.SaveItem(TestData.Series("Городские истории", "Иван Леснов", 2021, WatchStatus.Watched, true), null, false);
            window.SaveItem(TestData.Movie("Дорога к морю", "Анна Мирова", 2018), null, false);
            var saved = window.Library.Items.Single(item => item.Id == film.Id);
            Assert.AreNotEqual(source, saved.CoverPath);
            Assert.IsTrue(File.Exists(saved.CoverPath));
            window.Model.SelectedItem = saved;
            Flush(window);
            Assert.IsTrue(Control<Button>(window, "EditButton").IsEnabled);
            Assert.IsTrue(Control<Button>(window, "ImdbButton").IsEnabled);
            window.OpenSelectedImdb();
            Assert.AreEqual(details.ImdbUrl, opened);
            Control<Button>(window, "WatchButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(WatchStatus.Watched, saved.Status);
            Assert.AreSame(saved, window.Model.SelectedItem);
            Control<Button>(window, "PurchaseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(saved.Purchased);
            Render(window, "02-catalog");
            window.Model.SelectedKind = window.Model.Kinds[2];
            Flush(window);
            Assert.AreEqual(1, Control<ListBox>(window, "Catalog").Items.Count);
            Render(window, "03-filtered");
            string export = Path.Combine(folder, "экспорт.json");
            window.ExportLibrary(export);
            Assert.AreEqual(3, new JsonLibraryRepository().Load(export).Count);
            window.Model.Search = "ничего не найдено";
            Flush(window);
            Assert.AreEqual(Visibility.Visible, Control<TextBlock>(window, "EmptyMessage").Visibility);
            Render(window, "04-no-results");
            window.Model.ResetFilters();
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            Render(window, "05-minimum");
            window.DeleteItem(saved.Id, false);
            Assert.AreEqual(3, window.Library.Items.Count);
            File.Delete(source);
        }
        finally { window.Close(); }
        var reopened = new MainWindow(paths);
        try
        {
            Assert.AreEqual(3, reopened.Library.Items.Count);
            var film = reopened.Library.Items.Single(item => item.Title == "Северный ветер");
            Assert.IsTrue(File.Exists(film.CoverPath));
            Assert.AreNotSame(CoverStore.DefaultCover, CoverStore.LoadPreview(film.CoverPath));
            Assert.AreEqual(WatchStatus.Watched, film.Status);
            Assert.IsTrue(film.Purchased);
            reopened.DeleteItem(film.Id, true);
            Assert.AreEqual(2, new JsonLibraryRepository().Load(paths.LibraryFile).Count);
        }
        finally { reopened.Close(); }
    }

    [Test]
    public void BrokenFileShowsRecoverableErrorAndCannotBeOverwritten()
    {
        var paths = new AppPaths(folder);
        File.WriteAllText(paths.LibraryFile, "{ испорчено");
        var window = new MainWindow(paths);
        try
        {
            Show(window);
            Assert.IsTrue(window.Model.HasLoadError);
            Assert.IsFalse(Control<Button>(window, "AddButton").IsEnabled);
            Assert.Throws<InvalidOperationException>(() => window.SaveItem(TestData.Movie(), null, false));
            Assert.AreEqual("{ испорчено", File.ReadAllText(paths.LibraryFile));
            Render(window, "06-broken-file");
            new JsonLibraryRepository().Save(new[] { TestData.Movie() }, paths.LibraryFile);
            window.ReloadLibrary();
            Flush(window);
            Assert.IsFalse(window.Model.HasLoadError);
            Assert.IsTrue(Control<Button>(window, "AddButton").IsEnabled);
        }
        finally { window.Close(); }
    }

    [Test]
    public void EditingKeepsOriginalUntilConfirmedAndCanChangeType()
    {
        var original = TestData.Movie();
        var editor = new EditorWindow(original);
        try
        {
            Show(editor);
            Control<TextBox>(editor, "TitleBox").Text = "Не сохранено";
            Control<ComboBox>(editor, "KindBox").SelectedIndex = 1;
            Assert.AreEqual(Visibility.Visible, Control<StackPanel>(editor, "SeasonsPanel").Visibility);
            Render(editor, "07-editor-series");
        }
        finally { editor.Close(); }
        Assert.AreEqual("Начало", original.Title);
        Assert.IsNull(editor.Result);
        var accepted = new EditorWindow(original);
        accepted.WindowStartupLocation = WindowStartupLocation.Manual;
        accepted.Left = -10000; accepted.Top = -10000; accepted.ShowInTaskbar = false;
        accepted.Dispatcher.BeginInvoke(new Action(() =>
        {
            Control<ComboBox>(accepted, "KindBox").SelectedIndex = 1;
            Control<TextBox>(accepted, "SeasonsBox").Text = "4";
            Control<Button>(accepted, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        Assert.IsTrue(accepted.ShowDialog());
        Assert.IsInstanceOf<Series>(accepted.Result);
        Assert.AreEqual(original.Id, accepted.Result!.Id);
        Assert.AreEqual(4, ((Series)accepted.Result).Seasons);
        Assert.AreEqual("Начало", original.Title);
    }

    [Test]
    public void EditorShowsValidationAndSaveErrorsWithoutClosing()
    {
        var editor = new EditorWindow(null, (_, _) => throw new IOException("Нет доступа к файлу."));
        try
        {
            Show(editor);
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            StringAssert.Contains("название", Control<TextBlock>(editor, "ErrorText").Text);
            Control<TextBox>(editor, "TitleBox").Text = "Название";
            Control<TextBox>(editor, "YearBox").Text = "202";
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            StringAssert.Contains("четырёх", Control<TextBlock>(editor, "ErrorText").Text);
            Control<TextBox>(editor, "YearBox").Text = "2024";
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            StringAssert.Contains("Нет доступа", Control<TextBlock>(editor, "ErrorText").Text);
            Assert.IsTrue(editor.IsVisible);
            Assert.IsNull(editor.Result);
            Render(editor, "08-editor-error");
        }
        finally { editor.Close(); }
    }
}
