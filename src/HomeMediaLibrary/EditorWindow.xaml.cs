using System.IO;
using System.Windows;
using System.Windows.Controls;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Services;
using HomeMediaLibrary.ViewModels;
using Microsoft.Win32;

namespace HomeMediaLibrary;

public partial class EditorWindow : Window
{
    private readonly Guid id;
    private readonly Action<MediaItem, string?>? save;
    private string coverPath = "";
    private string? newCover;
    public MediaItem? Result { get; private set; }

    public EditorWindow(MediaItem? item = null, Action<MediaItem, string?>? save = null)
    {
        InitializeComponent();
        this.save = save;
        id = item?.Id ?? Guid.NewGuid();
        if (item is not null)
        {
            Title = "Редактирование записи";
            FormTitle.Text = "Редактировать запись";
            TitleBox.Text = item.Title;
            KindBox.SelectedIndex = item is Series ? 1 : 0;
            AuthorBox.Text = item.Author;
            YearBox.Text = item.Year?.ToString() ?? "";
            DurationBox.Text = (item as Movie)?.DurationMinutes?.ToString() ?? "";
            SeasonsBox.Text = (item as Series)?.Seasons?.ToString() ?? "";
            GenresBox.Text = string.Join(", ", item.Genres);
            DescriptionBox.Text = item.Description;
            ImdbBox.Text = item.ImdbUrl;
            StatusBox.SelectedIndex = item.Status == WatchStatus.Watched ? 1 : 0;
            PurchasedBox.IsChecked = item.Purchased;
            coverPath = item.CoverPath;
        }
        UpdatePreview();
    }

    private void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DurationPanel is null || SeasonsPanel is null) return;
        bool series = KindBox.SelectedIndex == 1;
        DurationPanel.Visibility = series ? Visibility.Collapsed : Visibility.Visible;
        SeasonsPanel.Visibility = series ? Visibility.Visible : Visibility.Collapsed;
        AuthorLabel.Text = series ? "Создатель или режиссёр" : "Режиссёр";
    }

    private void ChooseCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Обложки (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { SelectCover(dialog.FileName); }
        catch (Exception exception) { ErrorText.Text = "Не удалось открыть обложку: " + exception.Message; }
    }

    public void SelectCover(string path)
    {
        CoverStore.ValidateSource(path);
        newCover = path;
        ErrorText.Text = "";
        UpdatePreview();
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        coverPath = "";
        newCover = null;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        string path = newCover ?? coverPath;
        CoverPreview.Source = CoverStore.LoadPreview(path);
        CoverName.Text = string.IsNullOrEmpty(path) ? "Обложка не выбрана" : Path.GetFileName(path);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new MediaFormInput { Title = TitleBox.Text, Kind = KindBox.SelectedIndex, Author = AuthorBox.Text,
                Year = YearBox.Text, Genres = GenresBox.Text, Description = DescriptionBox.Text, ImdbUrl = ImdbBox.Text,
                CoverPath = coverPath, Duration = DurationBox.Text, Seasons = SeasonsBox.Text,
                Status = StatusBox.SelectedIndex, Purchased = PurchasedBox.IsChecked == true };
            MediaItem candidate = input.CreateItem(id);
            save?.Invoke(candidate, newCover);
            Result = candidate;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ErrorText.Text = "Не удалось сохранить запись. " + exception.Message;
        }
    }
}
