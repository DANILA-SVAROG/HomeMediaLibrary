using System.Text;

namespace HomeMediaLibrary.Core.Storage;

public static class AtomicFile
{
    public static void Write(string path, string text)
    {
        string fullPath = Path.GetFullPath(path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        string temporary = Path.Combine(folder, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Готовый файл заменяет старый только после записи всех данных.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
