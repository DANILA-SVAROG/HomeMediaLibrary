using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class EditorCoverTests
{
    private string folder = null!;
    [SetUp]
    public void Setup() => folder = Directory.CreateTempSubdirectory("medialibrary-editor-cover-").FullName;
    [TearDown]
    public void Cleanup() => Directory.Delete(folder, true);
    private static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);

    [Test]
    public void RemovingCoverUpdatesDraftAndSavedRecordButKeepsSourceFile()
    {
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var main = new MainWindow(paths);
        string source = WindowTests.MakeCover(folder);
        main.SaveItem(TestData.Movie(), source, false);
        var original = main.Library.Items[0];
        var editor = new EditorWindow(original, (item, cover) => main.SaveItem(item, cover, true));
        editor.WindowStartupLocation = WindowStartupLocation.Manual;
        editor.Left = -10000; editor.Top = -10000; editor.ShowInTaskbar = false;
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            editor.UpdateLayout();
            var remove = Descendants<Button>(editor).Single(button => Equals(button.Content, "Убрать обложку"));
            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreSame(CoverStore.DefaultCover, Control<Image>(editor, "CoverPreview").Source);
            Assert.AreEqual(original.CoverPath, main.Library.Items[0].CoverPath);
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        try
        {
            Assert.IsTrue(editor.ShowDialog());
            Assert.IsEmpty(main.Library.Items[0].CoverPath);
            Assert.IsTrue(File.Exists(source));
            Assert.IsTrue(File.Exists(original.CoverPath));
            Assert.IsEmpty(new HomeMediaLibrary.Core.Storage.JsonLibraryRepository().Load(paths.LibraryFile).Single().CoverPath);
        }
        finally { editor.Close(); main.Close(); }
    }

    [Test]
    public void SelectedCoverDisappearingBeforeSaveDoesNotLoseDraftOrPreviousImage()
    {
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var main = new MainWindow(paths);
        string source = WindowTests.MakeCover(folder);
        main.SaveItem(TestData.Movie(), source, false);
        var original = main.Library.Items[0];
        byte[] json = File.ReadAllBytes(paths.LibraryFile);
        var editor = new EditorWindow(original, (item, cover) => main.SaveItem(item, cover, true));
        try
        {
            WindowTests.Show(editor);
            Control<TextBox>(editor, "TitleBox").Text = "Черновик сохранён в форме";
            editor.SelectCover(source); File.Delete(source);
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(editor.IsVisible);
            Assert.IsNull(editor.Result);
            StringAssert.Contains("не найден", Control<TextBlock>(editor, "ErrorText").Text);
            Assert.AreEqual("Черновик сохранён в форме", Control<TextBox>(editor, "TitleBox").Text);
            Assert.AreSame(original, main.Library.Items[0]);
            Assert.IsTrue(File.Exists(original.CoverPath));
            CollectionAssert.AreEqual(json, File.ReadAllBytes(paths.LibraryFile));
            Assert.AreEqual(1, Directory.GetFiles(paths.CoversDirectory).Length);
        }
        finally { editor.Close(); main.Close(); }
    }

    [Test]
    public void SelectingAnotherCoverBeforeSaveCopiesOnlyLastChoice()
    {
        var paths = new AppPaths(Path.Combine(folder, "data"));
        var main = new MainWindow(paths);
        string first = WindowTests.MakeCover(folder, "Первая.png");
        string second = WindowTests.MakeCover(folder, "Вторая.png");
        var editor = new EditorWindow(null, (item, cover) => main.SaveItem(item, cover, false));
        editor.WindowStartupLocation = WindowStartupLocation.Manual;
        editor.Left = -10000; editor.Top = -10000; editor.ShowInTaskbar = false;
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            Control<TextBox>(editor, "TitleBox").Text = "Выбор обложки";
            editor.SelectCover(first); editor.SelectCover(second);
            Assert.AreEqual("Вторая.png", Control<TextBlock>(editor, "CoverName").Text);
            Assert.IsFalse(Directory.Exists(paths.CoversDirectory));
            Control<Button>(editor, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        try
        {
            Assert.IsTrue(editor.ShowDialog());
            Assert.AreEqual(1, Directory.GetFiles(paths.CoversDirectory).Length);
            CollectionAssert.AreEqual(File.ReadAllBytes(second), File.ReadAllBytes(main.Library.Items[0].CoverPath));
            Assert.IsTrue(File.Exists(first)); Assert.IsTrue(File.Exists(second));
        }
        finally { editor.Close(); main.Close(); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
