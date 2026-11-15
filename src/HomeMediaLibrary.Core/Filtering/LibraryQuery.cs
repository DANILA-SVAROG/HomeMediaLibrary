using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Filtering;

public static class LibraryQuery
{
    public static List<MediaItem> Apply(IEnumerable<MediaItem> items, LibraryFilter filter)
    {
        string search = filter.Search.Trim();
        var result = items.Where(item =>
            (search.Length == 0 || item.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.Author.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (filter.Genre is null || item.Genres.Contains(filter.Genre, StringComparer.OrdinalIgnoreCase))
            && (!filter.Year.HasValue || item.Year == filter.Year)
            && (!filter.Kind.HasValue || (filter.Kind == MediaKind.Movie ? item is Movie : item is Series))
            && (!filter.Status.HasValue || item.Status == filter.Status)
            && (!filter.Purchased.HasValue || item.Purchased == filter.Purchased));
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        var ordered = filter.Sort switch
        {
            LibrarySort.TitleDescending => result.OrderByDescending(item => item.Title, comparer),
            LibrarySort.AuthorAscending => result.OrderBy(item => item.Author, comparer),
            LibrarySort.AuthorDescending => result.OrderByDescending(item => item.Author, comparer),
            LibrarySort.YearAscending => result.OrderBy(item => item.Year.HasValue ? 0 : 1).ThenBy(item => item.Year),
            LibrarySort.YearDescending => result.OrderBy(item => item.Year.HasValue ? 0 : 1).ThenByDescending(item => item.Year),
            _ => result.OrderBy(item => item.Title, comparer)
        };
        return ordered.ThenBy(item => item.Title, comparer).ThenBy(item => item.Id).ToList();
    }
}
