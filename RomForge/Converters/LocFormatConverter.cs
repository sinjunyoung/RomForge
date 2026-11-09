using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Common.Localization;

namespace RomForge.Converters;

public class LocFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string name)
            return string.Empty;

        var method = typeof(LocalizedStrings).GetMethod(name);
        if (method is null)
            return string.Empty;

        var count = method.GetParameters().Length;
        if (values.Length < count)
            return string.Empty;

        var args = new object?[count];
        for (var i = 0; i < count; i++)
            args[i] = values[i] == DependencyProperty.UnsetValue ? null : values[i];

        return method.Invoke(Loc.S, args) ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
