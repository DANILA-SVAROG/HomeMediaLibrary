namespace HomeMediaLibrary.Core.Models;

public class Series : MediaItem
{
    public int? Seasons { get; }
    public override string TypeName => "Сериал";

    public Series(Guid id, MediaDetails data, int? seasons = null) : base(id, data)
    {
        MediaValidation.ValidatePositive(seasons, "Число сезонов");
        Seasons = seasons;
    }

    public override string GetShortInfo() => Seasons.HasValue
        ? "Сезонов: " + Seasons : "Число сезонов не указано";

    public override MediaItem Copy(MediaDetails details) => new Series(Id, details, Seasons);
}
