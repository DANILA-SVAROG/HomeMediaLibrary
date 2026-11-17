namespace HomeMediaLibrary.Core.Storage;

public class LibraryConflictException : IOException
{
    public LibraryConflictException() : base("Файл библиотеки изменён вне этого окна. Закройте форму редактирования, нажмите «Повторить загрузку» и повторите изменение.") { }
}
