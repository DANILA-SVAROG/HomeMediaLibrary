using System.IO;
using System.Text.Json;

namespace HomeMediaLibrary.Services;

public static class ErrorMessages
{
    public static string Describe(Exception exception)
    {
        return exception switch
        {
            HomeMediaLibrary.Core.Storage.LibraryConflictException => exception.Message,
            JsonException => "Файл библиотеки повреждён или содержит недопустимые данные.",
            UnauthorizedAccessException => "Нет доступа к выбранному файлу или папке.",
            FileNotFoundException => "Выбранный файл не найден.",
            DirectoryNotFoundException => "Выбранная папка не найдена.",
            System.IO.FileFormatException => "Не удалось прочитать изображение. Выберите исправный JPEG или PNG.",
            IOException => "Не удалось прочитать или записать файл. Проверьте доступ, свободное место и не открыт ли файл другой программой.",
            NotSupportedException => "Этот формат файла не поддерживается.",
            _ => exception.Message
        };
    }
}
