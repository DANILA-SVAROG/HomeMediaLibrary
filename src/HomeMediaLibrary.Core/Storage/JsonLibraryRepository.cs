using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Storage;

public class JsonLibraryRepository : ILibraryRepository
{
    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new MediaItemConverter(), new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public List<MediaItem> Load(string path)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (FileNotFoundException) { return new List<MediaItem>(); }
        catch (DirectoryNotFoundException) { return new List<MediaItem>(); }
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1)
            throw new JsonException("Неизвестный формат или версия библиотеки.");
        if (!root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new JsonException("В файле отсутствует список произведений.");
        var result = JsonSerializer.Deserialize<List<MediaItem>>(items.GetRawText(), options)!;
        ValidateItems(result);
        return result;
    }

    public void Save(IReadOnlyCollection<MediaItem> items, string path)
    {
        ValidateItems(items);
        string json = JsonSerializer.Serialize(new { Version = 1, Items = items }, options);
        AtomicFile.Write(path, json);
    }

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
