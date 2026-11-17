using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HomeMediaLibrary.Core;
using HomeMediaLibrary.Core.Filtering;
using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.ViewModels;

public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly LibraryController library;
    private readonly List<MediaItem> subscribed = new();
    private string search = "";
    private FilterOption? selectedGenre, selectedYear, selectedKind, selectedStatus, selectedPurchase, selectedSort;
    private MediaItem? selectedItem;
    private string statusMessage = "Добавьте фильмы и сериалы в свою коллекцию.";
    private string loadError = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<MediaItem> VisibleItems { get; } = new();
    public ObservableCollection<FilterOption> Genres { get; } = new();
    public ObservableCollection<FilterOption> Years { get; } = new();
    public List<FilterOption> Kinds { get; } = new() { new("Все типы", null), new("Фильмы", MediaKind.Movie), new("Сериалы", MediaKind.Series) };
    public List<FilterOption> Statuses { get; } = new() { new("Все статусы", null), new("В планах", WatchStatus.Planned), new("Просмотрено", WatchStatus.Watched) };
    public List<FilterOption> Purchases { get; } = new() { new("Все записи", null), new("Куплено", true), new("Не куплено", false) };
    public List<FilterOption> Sorts { get; } = new()
    {
        new("Название А–Я", LibrarySort.TitleAscending), new("Название Я–А", LibrarySort.TitleDescending),
        new("Автор А–Я", LibrarySort.AuthorAscending), new("Автор Я–А", LibrarySort.AuthorDescending),
        new("Год по возрастанию", LibrarySort.YearAscending), new("Год по убыванию", LibrarySort.YearDescending)
    };

    public string Search { get => search; set { search = value ?? ""; Changed(); Refresh(); } }
    public FilterOption? SelectedGenre { get => selectedGenre; set { selectedGenre = value; Changed(); Refresh(); } }
    public FilterOption? SelectedYear { get => selectedYear; set { selectedYear = value; Changed(); Refresh(); } }
    public FilterOption? SelectedKind { get => selectedKind; set { selectedKind = value; Changed(); Refresh(); } }
    public FilterOption? SelectedStatus { get => selectedStatus; set { selectedStatus = value; Changed(); Refresh(); } }
    public FilterOption? SelectedPurchase { get => selectedPurchase; set { selectedPurchase = value; Changed(); Refresh(); } }
    public FilterOption? SelectedSort { get => selectedSort; set { selectedSort = value; Changed(); Refresh(); } }
    public MediaItem? SelectedItem
    {
        get => selectedItem;
        set { selectedItem = value; Changed(); Changed(nameof(CanEditSelection)); Changed(nameof(CanOpenImdb)); Changed(nameof(HasSelection)); }
    }
    public bool HasSelection => SelectedItem is not null;
    public bool CanChange => library.IsReady;
    public bool CanEditSelection => CanChange && HasSelection;
    public bool CanOpenImdb => SelectedItem is not null && MediaValidation.IsImdbUrl(SelectedItem.ImdbUrl);
    public bool IsEmpty => VisibleItems.Count == 0;
    public string CountText => $"Показано: {VisibleItems.Count} из {library.Items.Count}";
    public string EmptyText => !library.IsReady ? "Библиотека не загружена. Исправьте ошибку и повторите загрузку."
        : library.Items.Count == 0 ? "В медиатеке пока пусто. Добавьте первый фильм или сериал."
        : "По выбранным условиям ничего не найдено. Попробуйте сбросить фильтры.";
    public string StatusMessage { get => statusMessage; set { statusMessage = value; Changed(); } }
    public string LoadError { get => loadError; set { loadError = value; Changed(); Changed(nameof(HasLoadError)); } }
    public bool HasLoadError => LoadError.Length > 0;
    public string DataPath => library.FilePath;

    public LibraryViewModel(LibraryController library)
    {
        this.library = library;
        selectedKind = Kinds[0]; selectedStatus = Statuses[0]; selectedPurchase = Purchases[0]; selectedSort = Sorts[0];
        ((INotifyCollectionChanged)library.Items).CollectionChanged += CollectionChanged;
        library.PropertyChanged += LibraryChanged;
        RebuildOptions();
    }

    private void LibraryChanged(object? sender, PropertyChangedEventArgs e)
    {
        Changed(nameof(CanChange)); Changed(nameof(CanEditSelection)); Changed(nameof(EmptyText));
        if (e.PropertyName == nameof(LibraryController.IsReady) && library.IsReady) RebuildOptions();
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // При загрузке обновляем список один раз, когда прочитаны все записи.
        if (library.IsReady) RebuildOptions();
    }

    private void RebuildOptions()
    {
        object? oldGenre = selectedGenre?.Value, oldYear = selectedYear?.Value;
        foreach (var item in subscribed) item.PropertyChanged -= ItemChanged;
        subscribed.Clear();
        foreach (var item in library.Items) { item.PropertyChanged += ItemChanged; subscribed.Add(item); }
        Genres.Clear(); Genres.Add(new("Все жанры", null));
        foreach (string genre in library.Items.SelectMany(item => item.Genres).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)) Genres.Add(new(genre, genre));
        Years.Clear(); Years.Add(new("Все годы", null));
        foreach (int year in library.Items.Where(item => item.Year.HasValue).Select(item => item.Year!.Value).Distinct().OrderByDescending(value => value))
            Years.Add(new(year.ToString(), year));
        selectedGenre = Genres.FirstOrDefault(option => Equals(option.Value, oldGenre)) ?? Genres[0];
        selectedYear = Years.FirstOrDefault(option => Equals(option.Value, oldYear)) ?? Years[0];
        Changed(nameof(SelectedGenre)); Changed(nameof(SelectedYear));
        Refresh();
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MediaItem.Status) or nameof(MediaItem.Purchased)) Refresh();
    }

    public void ResetFilters()
    {
        search = "";
        selectedGenre = Genres[0]; selectedYear = Years[0]; selectedKind = Kinds[0];
        selectedStatus = Statuses[0]; selectedPurchase = Purchases[0]; selectedSort = Sorts[0];
        Changed(null);
        Refresh();
    }

    public void Refresh()
    {
        Guid? selection = SelectedItem?.Id;
        var filter = new LibraryFilter { Search = Search, Genre = selectedGenre?.Value as string,
            Year = selectedYear?.Value as int?, Kind = selectedKind?.Value as MediaKind?,
            Status = selectedStatus?.Value as WatchStatus?, Purchased = selectedPurchase?.Value as bool?,
            Sort = selectedSort?.Value as LibrarySort? ?? LibrarySort.TitleAscending };
        var result = LibraryQuery.Apply(library.Items, filter);
        VisibleItems.Clear();
        foreach (var item in result) VisibleItems.Add(item);
        SelectedItem = VisibleItems.FirstOrDefault(item => item.Id == selection);
        Changed(nameof(IsEmpty)); Changed(nameof(EmptyText)); Changed(nameof(CountText));
    }

    public void Dispose()
    {
        ((INotifyCollectionChanged)library.Items).CollectionChanged -= CollectionChanged;
        library.PropertyChanged -= LibraryChanged;
        foreach (var item in subscribed) item.PropertyChanged -= ItemChanged;
    }

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
