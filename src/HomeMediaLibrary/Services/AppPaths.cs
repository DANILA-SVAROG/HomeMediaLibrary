using System.IO;

namespace HomeMediaLibrary.Services;

public class AppPaths
{
    public string DataDirectory { get; }
    public string LibraryFile => Path.Combine(DataDirectory, "library.json");
    public string CoversDirectory => Path.Combine(DataDirectory, "covers");

    public AppPaths(string? directory = null)
    {
        DataDirectory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeMediaLibrary");
    }
}
