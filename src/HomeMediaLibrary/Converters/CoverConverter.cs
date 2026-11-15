using System.Globalization;
using System.Windows.Data;
using HomeMediaLibrary.Services;

namespace HomeMediaLibrary.Converters;

public class CoverConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => CoverStore.LoadPreview(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
