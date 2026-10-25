using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NSW.Core.Converters;

public class IconConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3) 
            return null;

        bool isRunning = values[0] is true;
        string iconPath = isRunning ? values[2] as string ?? string.Empty : values[1] as string ?? string.Empty;

        if (string.IsNullOrEmpty(iconPath)) 
            return null;

        try
        {
            var uri = iconPath.StartsWith("pack://") ? new Uri(iconPath, UriKind.Absolute) : new Uri(iconPath, UriKind.RelativeOrAbsolute);

            return new BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}