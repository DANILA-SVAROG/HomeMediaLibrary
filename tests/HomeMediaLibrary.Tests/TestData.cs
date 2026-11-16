using HomeMediaLibrary.Core.Models;
using HomeMediaLibrary.Core.Storage;

namespace HomeMediaLibrary.Tests;

public static class TestData
{
    public static Movie Movie(string title = "Начало", string author = "Кристофер Нолан", int? year = 2010,
        WatchStatus status = WatchStatus.Planned, bool purchased = false)
        => new(Guid.NewGuid(), Details(title, author, year, status, purchased), 148);

    public static MediaDetails Details(string title, string author = "", int? year = null,
        WatchStatus status = WatchStatus.Planned, bool purchased = false)
        => new() { Title = title, Author = author, Year = year, Status = status, Purchased = purchased,
            Genres = new() { "Фантастика", "Драма" }, Description = "Описание для проверки." };

    public static Series Series(string title = "Сериал", string author = "Автор сериала", int? year = 2020,
        WatchStatus status = WatchStatus.Planned, bool purchased = false)
        => new(Guid.NewGuid(), Details(title, author, year, status, purchased), 3);
}

public class MemoryRepository : ILibraryRepository
{
    public List<MediaItem> Data { get; set; } = new();
    public bool FailSave { get; set; }
    public bool FailLoad { get; set; }
    public int Saved { get; private set; }
    public List<MediaItem> Load(string path)
    {
        if (FailLoad) throw new System.IO.IOException("Файл недоступен.");
        return Data.ToList();
    }
    public void Save(IReadOnlyCollection<MediaItem> items, string path)
    {
        if (FailSave) throw new System.IO.IOException("Нет места для сохранения.");
        Data = items.ToList();
        Saved++;
    }
}
