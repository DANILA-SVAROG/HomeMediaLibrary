using System.Collections.ObjectModel;
using System.ComponentModel;
using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;

namespace HomeMediaLibrary.Core;

public class LibraryController : INotifyPropertyChanged
{
    private readonly ILibraryRepository repository;
    private readonly ObservableCollection<MediaItem> items = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public ReadOnlyObservableCollection<MediaItem> Items { get; }
    public string FilePath { get; }
    public bool IsReady { get; private set; }

    public LibraryController(ILibraryRepository repository, string filePath)
    {
        this.repository = repository;
        FilePath = Path.GetFullPath(filePath);
        Items = new ReadOnlyObservableCollection<MediaItem>(items);
    }

    public void Load()
    {
        IsReady = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReady)));
        var loaded = repository.Load(FilePath);
        items.Clear();
        foreach (var item in loaded) items.Add(item);
        IsReady = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReady)));
    }

    public void Add(MediaItem item)
    {
        CheckReady();
        if (items.Any(existing => existing.Id == item.Id)) throw new ArgumentException("Такой идентификатор уже существует.");
        var candidate = items.ToList();
        candidate.Add(item);
        repository.Save(candidate, FilePath);
        items.Add(item);
    }

    public void Update(MediaItem item)
    {
        CheckReady();
        int index = Find(item.Id);
        var candidate = items.ToList();
        candidate[index] = item;
        repository.Save(candidate, FilePath);
        items[index] = item;
    }

    public void Delete(Guid id)
    {
        CheckReady();
        int index = Find(id);
        var candidate = items.ToList();
        candidate.RemoveAt(index);
        repository.Save(candidate, FilePath);
        items.RemoveAt(index);
    }

    public void ChangeMarks(Guid id, WatchStatus status, bool purchased)
    {
        CheckReady();
        int index = Find(id);
        MediaItem original = items[index];
        var details = original.GetDetails();
        details.Status = status;
        details.Purchased = purchased;
        var candidate = items.ToList();
        candidate[index] = original.Copy(details);
        repository.Save(candidate, FilePath);
        original.SetMarks(status, purchased);
    }

    public void Export(string path)
    {
        CheckReady();
        if (string.Equals(Path.GetFullPath(path), FilePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Для экспорта выберите другой файл, а не рабочую библиотеку.");
        // В экспорт попадает вся коллекция, а не отфильтрованный список.
        repository.Save(items.ToList(), path);
    }

    private int Find(Guid id)
    {
        int index = items.ToList().FindIndex(item => item.Id == id);
        if (index < 0) throw new ArgumentException("Запись уже отсутствует в библиотеке.");
        return index;
    }

    private void CheckReady()
    {
        if (!IsReady) throw new InvalidOperationException("Сначала устраните ошибку загрузки и повторите чтение библиотеки.");
    }
}
