using System.Globalization;
using System.Windows.Data;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.Core.Converters
{
    public class LanguageToFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Language lang)
                return "없음";

            return lang switch
            {
                Language.AmericanEnglish => "미국 영어",
                Language.BritishEnglish => "영국 영어",
                Language.Japanese => "일본어",
                Language.French => "프랑스어",
                Language.German => "독일어",
                Language.LatinAmericanSpanish => "스페인어(중남미)",
                Language.Spanish => "스페인어(스페인)",
                Language.Italian => "이탈리아어",
                Language.Dutch => "네덜란드어",
                Language.CanadianFrench => "캐나다 프랑스어",
                Language.Portuguese => "포르투갈어(유럽)",
                Language.Russian => "러시아어",
                Language.Korean => "한국어",
                Language.TraditionalChinese => "중국어(번체)",
                Language.SimplifiedChinese => "중국어(간체)",
                Language.BrazilianPortuguese => "포르투갈어(브라질)",
                Language.Polish => "폴란드어",
                Language.Thai => "태국어",
                _ => "없음"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}