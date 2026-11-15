namespace HomeMediaLibrary.Core.Models;

public class Movie : MediaItem
{
    public int? DurationMinutes { get; }
    public override string TypeName => "Фильм";

    public Movie(Guid id, MediaDetails data, int? durationMinutes = null) : base(id, data)
    {
        MediaValidation.ValidatePositive(durationMinutes, "Время фильма");
        DurationMinutes = durationMinutes;
    }

    public override string GetShortInfo() => DurationMinutes.HasValue
        ? DurationMinutes + " мин." : "Длительность не указана";

    public override MediaItem Copy(MediaDetails details) => new Movie(Id, details, DurationMinutes);
}
