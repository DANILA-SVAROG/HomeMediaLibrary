namespace HomeMediaLibrary.Core.Models;

public static class MediaValidation
{
    public static void Validate(MediaDetails data)
    {
        if (string.IsNullOrWhiteSpace(data.Title))
            throw new ArgumentException("Введите название.");
        if (data.Year.HasValue && (data.Year < 1000 || data.Year > 9999))
            throw new ArgumentException("Год должен состоять из четырёх цифр.");
        if (!Enum.IsDefined(data.Status))
            throw new ArgumentException("Выберите статус просмотра.");
        if (data.Genres is null || data.Genres.Any(genre => genre is null))
            throw new ArgumentException("Список жанров содержит неверные данные.");
        if (!string.IsNullOrWhiteSpace(data.ImdbUrl) && !IsImdbUrl(data.ImdbUrl.Trim()))
            throw new ArgumentException("Укажите ссылку HTTPS на imdb.com или www.imdb.com.");
    }

    public static bool IsImdbUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            && uri.IsDefaultPort && (uri.Host.Equals("imdb.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.imdb.com", StringComparison.OrdinalIgnoreCase));
    }

    public static void ValidatePositive(int? value, string name)
    {
        if (value.HasValue && value <= 0)
            throw new ArgumentException(name + " должно быть положительным целым числом.");
    }
}
