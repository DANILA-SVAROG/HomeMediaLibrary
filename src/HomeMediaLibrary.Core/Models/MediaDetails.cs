namespace HomeMediaLibrary.Core.Models;

// Общие поля формы передаются в модель одним объектом.
public class MediaDetails
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? Year { get; set; }
    public List<string> Genres { get; set; } = new();
    public string Description { get; set; } = "";
    public string ImdbUrl { get; set; } = "";
    public string CoverPath { get; set; } = "";
    public WatchStatus Status { get; set; } = WatchStatus.Planned;
    public bool Purchased { get; set; }
}
