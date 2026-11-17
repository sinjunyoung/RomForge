using Common.Localization;
using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class BoolToPlayStopTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isPlaying = value is bool b && b;

        if (parameter is string paramStr && paramStr.Contains('|'))
        {
            var parts = paramStr.Split('|');

            if (parts.Length == 2)
                return isPlaying ? parts[0] : parts[1];
        }

        return isPlaying ? Loc.S.LabelStop : Loc.S.LabelPlay;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}