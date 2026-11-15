using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.ViewModels;

public class MediaFormInput
{
    public string Title { get; set; } = "";
    public int Kind { get; set; }
    public string Author { get; set; } = "";
    public string Year { get; set; } = "";
    public string Genres { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImdbUrl { get; set; } = "";
    public string CoverPath { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Seasons { get; set; } = "";
    public int Status { get; set; }
    public bool Purchased { get; set; }

    public MediaItem CreateItem(Guid id)
    {
        if (Kind is not 0 and not 1) throw new ArgumentException("Выберите тип: фильм или сериал.");
        if (Status is not 0 and not 1) throw new ArgumentException("Выберите статус просмотра.");
        int? year = ReadNumber(Year, "Год");
        if (year.HasValue && (Year.Trim().Length != 4 || !Year.Trim().All(char.IsAsciiDigit)))
            throw new ArgumentException("Год должен состоять из четырёх цифр.");
        var details = new MediaDetails { Title = Title, Author = Author, Year = year,
            Genres = Genres.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
            Description = Description, ImdbUrl = ImdbUrl, CoverPath = CoverPath,
            Status = Status == 1 ? WatchStatus.Watched : WatchStatus.Planned, Purchased = Purchased };
        return Kind == 0 ? new Movie(id, details, ReadNumber(Duration, "Длительность"))
            : new Series(id, details, ReadNumber(Seasons, "Число сезонов"));
    }

    private static int? ReadNumber(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!int.TryParse(text.Trim(), out int value) || value <= 0 || !text.Trim().All(char.IsAsciiDigit))
            throw new ArgumentException(name + ": введите положительное целое число или оставьте поле пустым.");
        return value;
    }
}
