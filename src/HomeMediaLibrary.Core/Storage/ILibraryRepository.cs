using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Storage;

public interface ILibraryRepository
{
    List<MediaItem> Load(string path);
    void Save(IReadOnlyCollection<MediaItem> items, string path);
}
