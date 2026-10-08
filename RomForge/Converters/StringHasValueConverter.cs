using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

[ValueConversion(typeof(string), typeof(bool))]
public class StringHasValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
            return !string.IsNullOrWhiteSpace(str);

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}