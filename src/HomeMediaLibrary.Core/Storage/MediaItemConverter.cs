using System.Text.Json;
using System.Text.Json.Serialization;
using HomeMediaLibrary.Core.Models;

namespace HomeMediaLibrary.Core.Storage;

public class MediaItemConverter : JsonConverter<MediaItem>
{
    public override MediaItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Запись должна быть объектом JSON.");
        try
        {
            Guid id = root.GetProperty("Id").GetGuid();
            string? type = root.GetProperty("$type").GetString();
            if (!root.TryGetProperty("Title", out _)) throw new JsonException("В записи нет названия.");
            var details = JsonSerializer.Deserialize<MediaDetails>(root.GetRawText(), options)
                ?? throw new JsonException("В записи нет данных.");
            // Тип восстанавливается по тому же принципу, что и в четвёртой лабе.
            return type switch
            {
                nameof(Movie) => new Movie(id, details, ReadNumber(root, "DurationMinutes")),
                nameof(Series) => new Series(id, details, ReadNumber(root, "Seasons")),
                _ => throw new JsonException("Неизвестный тип произведения: " + type)
            };
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new JsonException("Некорректная запись медиатеки: " + e.Message, e);
        }
    }

    private static int? ReadNumber(JsonElement root, string name)
    {
        return !root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null
            ? null : value.GetInt32();
    }

    public override void Write(Utf8JsonWriter writer, MediaItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", value.GetType().Name);
        writer.WriteString("Id", value.Id);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value.GetDetails(), options));
        foreach (var property in document.RootElement.EnumerateObject()) property.WriteTo(writer);
        if (value is Movie movie) WriteNumber(writer, "DurationMinutes", movie.DurationMinutes);
        else if (value is Series series) WriteNumber(writer, "Seasons", series.Seasons);
        else throw new JsonException("Неизвестный класс произведения.");
        writer.WriteEndObject();
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, int? number)
    {
        if (number.HasValue) writer.WriteNumber(name, number.Value);
        else writer.WriteNull(name);
    }
}
