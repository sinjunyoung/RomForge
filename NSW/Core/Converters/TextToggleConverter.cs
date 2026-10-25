using System.Globalization;
using System.Windows.Data;

namespace NSW.Core.Converters;

public class TextToggleConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3) 
            return string.Empty;

        bool isRunning = values[0] is true;

        return isRunning ? values[2] as string ?? string.Empty  : values[1] as string ?? string.Empty;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}