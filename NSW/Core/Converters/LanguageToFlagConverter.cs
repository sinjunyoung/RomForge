using System.Globalization;
using Common.Localization;
using System.Windows.Data;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.Core.Converters
{
    public class LanguageToFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Language lang)
                return Loc.S.LabelNone;

            return lang switch
            {
                Language.AmericanEnglish => Loc.S.SwitchLanguageAmericanEnglish,
                Language.BritishEnglish => Loc.S.SwitchLanguageBritishEnglish,
                Language.Japanese => Loc.S.SwitchLanguageJapanese,
                Language.French => Loc.S.SwitchLanguageFrench,
                Language.German => Loc.S.SwitchLanguageGerman,
                Language.LatinAmericanSpanish => Loc.S.SwitchLanguageLatinAmericanSpanish,
                Language.Spanish => Loc.S.SwitchLanguageSpanish,
                Language.Italian => Loc.S.SwitchLanguageItalian,
                Language.Dutch => Loc.S.SwitchLanguageDutch,
                Language.CanadianFrench => Loc.S.SwitchLanguageCanadianFrench,
                Language.Portuguese => Loc.S.SwitchLanguagePortuguese,
                Language.Russian => Loc.S.SwitchLanguageRussian,
                Language.Korean => Loc.S.SwitchLanguageKorean,
                Language.TraditionalChinese => Loc.S.SwitchLanguageTraditionalChinese,
                Language.SimplifiedChinese => Loc.S.SwitchLanguageSimplifiedChinese,
                Language.BrazilianPortuguese => Loc.S.SwitchLanguageBrazilianPortuguese,
                Language.Polish => Loc.S.SwitchLanguagePolish,
                Language.Thai => Loc.S.SwitchLanguageThai,
                _ => Loc.S.LabelNone
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}