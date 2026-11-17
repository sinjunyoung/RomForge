using Common.Localization;
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
            LocaleLanguage.None => Loc.S.LabelLocaleNone,
            LocaleLanguage.JP => Loc.S.SwitchLanguageJapanese,
            LocaleLanguage.EN => Loc.S.LabelLocaleEnglish,
            LocaleLanguage.FR => Loc.S.SwitchLanguageFrench,
            LocaleLanguage.DE => Loc.S.SwitchLanguageGerman,
            LocaleLanguage.IT => Loc.S.SwitchLanguageItalian,
            LocaleLanguage.ES => Loc.S.LabelLocaleSpanish,
            LocaleLanguage.ZH => Loc.S.SwitchLanguageSimplifiedChinese,
            LocaleLanguage.KO => Loc.S.SwitchLanguageKorean,
            LocaleLanguage.NL => Loc.S.SwitchLanguageDutch,
            LocaleLanguage.PT => Loc.S.LabelLocalePortuguese,
            LocaleLanguage.RU => Loc.S.SwitchLanguageRussian,
            LocaleLanguage.TW => Loc.S.SwitchLanguageTraditionalChinese,
            _ => lang.ToString(),
        } : Loc.S.LabelLocaleNone;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}