using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RomForge.Converters;

public class BoolToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isChecked = value is bool b && b;
        string path = isChecked ? "/Assets/Images/Checked.png" : "/Assets/Images/Unchecked.png";
        return new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}