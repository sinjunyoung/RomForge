using Common.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RomForge.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is LogLevel level ? level switch
        {
            LogLevel.Ok => Brushes.LimeGreen,
            LogLevel.Highlight => Brushes.Orange,
            LogLevel.Warning => Brushes.Gold,
            LogLevel.Error => Brushes.Tomato,
            _ => Brushes.Gray
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}