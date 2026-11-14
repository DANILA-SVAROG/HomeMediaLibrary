using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HomeMediaLibrary.Core.Models;

public abstract class MediaItem : INotifyPropertyChanged
{
    private WatchStatus status;
    private bool purchased;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }
    public string Title { get; }
    public string Author { get; }
    public int? Year { get; }
    public IReadOnlyList<string> Genres { get; }
    public string Description { get; }
    public string ImdbUrl { get; }
    public string CoverPath { get; }
    public WatchStatus Status => status;
    public bool Purchased => purchased;
    public string StatusText => Status == WatchStatus.Watched ? "Просмотрено" : "В планах";
    public string PurchaseText => Purchased ? "Куплено" : "Не куплено";
    public string GenreText => Genres.Count == 0 ? "Жанры не указаны" : string.Join(", ", Genres);
    public string YearText => Year?.ToString() ?? "Год не указан";
    public abstract string TypeName { get; }
    public abstract string GetShortInfo();
    public string ShortInfo => GetShortInfo();

    protected MediaItem(Guid id, MediaDetails data)
    {
        MediaValidation.Validate(data);
        if (id == Guid.Empty) throw new ArgumentException("Идентификатор записи не должен быть пустым.");
        Id = id;
        Title = data.Title.Trim();
        Author = (data.Author ?? "").Trim();
        Year = data.Year;
        Genres = data.Genres.Select(genre => genre.Trim()).Where(genre => genre.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        Description = (data.Description ?? "").Trim();
        ImdbUrl = (data.ImdbUrl ?? "").Trim();
        CoverPath = (data.CoverPath ?? "").Trim();
        status = data.Status;
        purchased = data.Purchased;
    }

    // Отметки меняются только после успешного сохранения коллекции.
    public void SetMarks(WatchStatus newStatus, bool newPurchased)
    {
        if (!Enum.IsDefined(newStatus)) throw new ArgumentException("Неизвестный статус просмотра.");
        if (status != newStatus)
        {
            status = newStatus;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
        }
        if (purchased != newPurchased)
        {
            purchased = newPurchased;
            OnPropertyChanged(nameof(Purchased));
            OnPropertyChanged(nameof(PurchaseText));
        }
    }

    public MediaDetails GetDetails()
    {
        return new MediaDetails { Title = Title, Author = Author, Year = Year, Genres = Genres.ToList(),
            Description = Description, ImdbUrl = ImdbUrl, CoverPath = CoverPath, Status = Status, Purchased = Purchased };
    }

    public abstract MediaItem Copy(MediaDetails details);

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
