namespace HomeMediaLibrary.ViewModels;

public class FilterOption
{
    public string Text { get; }
    public object? Value { get; }
    public FilterOption(string text, object? value) { Text = text; Value = value; }
    public override string ToString() => Text;
}
