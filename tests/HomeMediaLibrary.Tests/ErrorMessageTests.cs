using System.IO;
using System.Text.Json;
using HomeMediaLibrary.Core.Storage;
using HomeMediaLibrary.Services;
using NUnit.Framework;

namespace HomeMediaLibrary.Tests;

[TestFixture]
public class ErrorMessageTests
{
    [Test]
    public void FileAndImageErrorsHaveUnderstandableMessages()
    {
        Assert.Multiple(() =>
        {
            StringAssert.Contains("повреждён", ErrorMessages.Describe(new JsonException()));
            StringAssert.Contains("Нет доступа", ErrorMessages.Describe(new UnauthorizedAccessException()));
            StringAssert.Contains("не найден", ErrorMessages.Describe(new FileNotFoundException()));
            StringAssert.Contains("папка не найдена", ErrorMessages.Describe(new DirectoryNotFoundException()));
            StringAssert.Contains("JPEG или PNG", ErrorMessages.Describe(new FileFormatException()));
            StringAssert.Contains("свободное место", ErrorMessages.Describe(new IOException()));
            StringAssert.Contains("не поддерживается", ErrorMessages.Describe(new NotSupportedException()));
            StringAssert.Contains("Повторить загрузку", ErrorMessages.Describe(new LibraryConflictException()));
            Assert.AreEqual("Введите название.", ErrorMessages.Describe(new ArgumentException("Введите название.")));
        });
    }
}
