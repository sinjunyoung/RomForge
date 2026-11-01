using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class TitleIdGroupConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string titleId && !string.IsNullOrEmpty(titleId))
            return titleId.Length > 12 ? titleId[..12] : titleId;

        return value ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}