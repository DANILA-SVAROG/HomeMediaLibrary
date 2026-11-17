using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Storage;

public class JsonLibraryRepository : ILibraryRepository
{
    private readonly Dictionary<string, string?> fingerprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new MediaItemConverter(), new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public List<MediaItem> Load(string path)
    {
        path = Path.GetFullPath(path);
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (FileNotFoundException) { fingerprints[path] = null; return new List<MediaItem>(); }
        catch (DirectoryNotFoundException) { fingerprints[path] = null; return new List<MediaItem>(); }
        string json;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            json = reader.ReadToEnd();
        }
        catch (DecoderFallbackException e) { throw new JsonException("В файле повреждена кодировка текста.", e); }
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1)
            throw new JsonException("Неизвестный формат или версия библиотеки.");
        if (!root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new JsonException("В файле отсутствует список произведений.");
        var result = JsonSerializer.Deserialize<List<MediaItem>>(items.GetRawText(), options)!;
        ValidateItems(result);
        fingerprints[path] = Fingerprint(bytes);
        return result;
    }

    public void Save(IReadOnlyCollection<MediaItem> items, string path)
    {
        ValidateItems(items);
        string json = JsonSerializer.Serialize(new { Version = 1, Items = items }, options);
        path = Path.GetFullPath(path);
        string mutexName = "HomeMediaLibrary-" + Fingerprint(Encoding.UTF8.GetBytes(path.ToUpperInvariant()));
        using var mutex = new Mutex(false, mutexName);
        bool entered;
        try { entered = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { entered = true; }
        if (!entered) throw new IOException("Другое окно сейчас сохраняет библиотеку. Повторите действие.");
        try
        {
            // Чужие изменения нельзя заменять устаревшей копией из памяти.
            if (fingerprints.TryGetValue(path, out string? expected) && CurrentFingerprint(path) != expected)
                throw new LibraryConflictException();
            AtomicFile.Write(path, json);
            fingerprints[path] = Fingerprint(Encoding.UTF8.GetBytes(json));
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static string? CurrentFingerprint(string path)
    {
        try { return Fingerprint(File.ReadAllBytes(path)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void ValidateItems(IEnumerable<MediaItem> items)
    {
        var ids = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (item is null || !ids.Add(item.Id))
                throw new JsonException("В библиотеке пустая запись или повторяется идентификатор.");
        }
    }
}
