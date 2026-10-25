using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Converters;

public class LockIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string fileName = value is bool b && b ? "Lock.png" : "Unlock.png";
        string path = $"pack://application:,,,/RomForge.Core;component/Assets/Images/{fileName}";
        return new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}