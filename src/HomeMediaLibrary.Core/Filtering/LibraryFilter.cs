using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Filtering;

public enum MediaKind { Movie, Series }
public enum LibrarySort { TitleAscending, TitleDescending, AuthorAscending, AuthorDescending, YearAscending, YearDescending }

public class LibraryFilter
{
    public string Search { get; set; } = "";
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public MediaKind? Kind { get; set; }
    public WatchStatus? Status { get; set; }
    public bool? Purchased { get; set; }
    public LibrarySort Sort { get; set; }
}
