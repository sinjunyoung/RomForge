using N3DS.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class Locale3dsLanguageToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is LocaleLanguage lang ? lang switch
        {
            LocaleLanguage.None => "미선택",
            LocaleLanguage.JP => "일본어",
            LocaleLanguage.EN => "영어",
            LocaleLanguage.FR => "프랑스어",
            LocaleLanguage.DE => "독일어",
            LocaleLanguage.IT => "이탈리아어",
            LocaleLanguage.ES => "스페인어",
            LocaleLanguage.ZH => "중국어(간체)",
            LocaleLanguage.KO => "한국어",
            LocaleLanguage.NL => "네덜란드어",
            LocaleLanguage.PT => "포르투갈어",
            LocaleLanguage.RU => "러시아어",
            LocaleLanguage.TW => "중국어(번체)",
            _ => lang.ToString(),
        } : "미선택";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}