using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;
using HomeMediaLibrary.Services;
using HomeMediaLibrary.ViewModels;
using Microsoft.Win32;

namespace HomeMediaLibrary;

public partial class MainWindow : Window
{
    private readonly CoverStore covers;
    private readonly Action<string> openImdb;
    public LibraryController Library { get; }
    public LibraryViewModel Model { get; }

    public MainWindow() : this(new AppPaths()) { }

    public MainWindow(AppPaths paths, ILibraryRepository? repository = null, Action<string>? browser = null)
    {
        InitializeComponent();
        covers = new CoverStore(paths);
        openImdb = browser ?? (url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
        Library = new LibraryController(repository ?? new JsonLibraryRepository(), paths.LibraryFile);
        Model = new LibraryViewModel(Library);
        DataContext = Model;
        Closed += (_, _) => Model.Dispose();
        ReloadLibrary();
    }

    public void ReloadLibrary()
    {
        try
        {
            Library.Load();
            Model.LoadError = "";
            Model.StatusMessage = Library.Items.Count == 0 ? "Добавьте первый фильм или сериал." : "Библиотека загружена.";
        }
        catch (Exception exception)
        {
            Model.LoadError = "Не удалось загрузить библиотеку. Исходный файл не изменён. " + ErrorMessages.Describe(exception);
            Model.StatusMessage = "Добавление и изменение записей недоступны до успешной загрузки.";
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new EditorWindow(null, (item, cover) => SaveItem(item, cover, false)) { Owner = this };
        editor.ShowDialog();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!Model.CanEditSelection) return;
        var editor = new EditorWindow(Model.SelectedItem, (item, cover) => SaveItem(item, cover, true)) { Owner = this };
        editor.ShowDialog();
    }

    public void SaveItem(MediaItem item, string? coverSource, bool editing)
    {
        string? imported = null;
        try
        {
            if (coverSource is not null)
            {
                imported = covers.Import(coverSource);
                var details = item.GetDetails();
                details.CoverPath = imported;
                item = item.Copy(details);
            }
            if (editing) Library.Update(item);
            else Library.Add(item);
        }
        catch (Exception exception)
        {
            ShowConflict(exception);
            if (imported is not null)
            {
                try { covers.RemoveUnusedImport(imported); }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
        Model.SelectedItem = Model.VisibleItems.FirstOrDefault(record => record.Id == item.Id);
        Model.StatusMessage = Model.SelectedItem is null
            ? "Запись сохранена, но скрыта выбранными фильтрами." : "Запись сохранена.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedItem is null) return;
        MediaItem selected = Model.SelectedItem;
        bool confirmed = MessageBox.Show(this, $"Удалить «{selected.Title}» из медиатеки?", "Удаление записи",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        TryAction(() => DeleteItem(selected.Id, confirmed));
    }

    public void DeleteItem(Guid id, bool confirmed)
    {
        if (!confirmed) return;
        Library.Delete(id);
        Model.StatusMessage = "Запись удалена.";
    }

    private void Watch_Click(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedItem is not MediaItem selected) return;
        TryAction(() =>
        {
            Library.ChangeMarks(selected.Id, selected.Status == WatchStatus.Planned ? WatchStatus.Watched : WatchStatus.Planned, selected.Purchased);
            Model.StatusMessage = "Статус просмотра сохранён.";
        });
    }

    private void Purchase_Click(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedItem is not MediaItem selected) return;
        TryAction(() =>
        {
            Library.ChangeMarks(selected.Id, selected.Status, !selected.Purchased);
            Model.StatusMessage = "Отметка покупки сохранена.";
        });
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = "Моя медиатека.json", Filter = "JSON (*.json)|*.json", DefaultExt = ".json" };
        if (dialog.ShowDialog(this) == true) TryAction(() => ExportLibrary(dialog.FileName));
    }

    public void ExportLibrary(string path)
    {
        Library.Export(path);
        Model.StatusMessage = $"Экспортировано записей: {Library.Items.Count}. Файл: {path}";
    }

    private void Imdb_Click(object sender, RoutedEventArgs e) => TryAction(OpenSelectedImdb);

    public void OpenSelectedImdb()
    {
        if (Model.CanOpenImdb) openImdb(Model.SelectedItem!.ImdbUrl);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => Model.ResetFilters();
    private void Reload_Click(object sender, RoutedEventArgs e) => ReloadLibrary();
    private void Catalog_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Model.CanEditSelection) Edit_Click(sender, e);
    }

    private void TryAction(Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            ShowConflict(exception);
            Model.StatusMessage = "Операция не выполнена. " + ErrorMessages.Describe(exception);
            MessageBox.Show(this, Model.StatusMessage, "Домашняя медиатека", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowConflict(Exception exception)
    {
        if (exception is LibraryConflictException) Model.LoadError = ErrorMessages.Describe(exception);
    }
}
