using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.Core.Converters
{
    public class LanguageToFlagImageConverter : IValueConverter
    {
        public LanguageToFlagImageConverter() { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Language lang) 
                return null;

            string fileName = lang switch
            {
                Language.AmericanEnglish => "american.png",
                Language.BritishEnglish => "british.png",
                Language.Japanese => "japanese.png",
                Language.French => "french.png",
                Language.German => "german.png",
                Language.LatinAmericanSpanish => "mexican.png",
                Language.Spanish => "spanish.png",
                Language.Italian => "italian.png",
                Language.Dutch => "dutch.png",
                Language.CanadianFrench => "canadian.png",
                Language.Portuguese => "portuguese.png",
                Language.Russian => "russian.png",
                Language.Korean => "korean.png",
                Language.TraditionalChinese => "taiwanese.png",
                Language.SimplifiedChinese => "chinese.png",
                Language.BrazilianPortuguese => "brazilian.png",
                Language.Polish => "polish.png",
                Language.Thai => "thai.png",
                _ => "unknown.png"
            };

            try
            {
                string path = $"pack://application:,,,/NSW.Core;component/Assets/Images/{fileName}";
                return new BitmapImage(new Uri(path, UriKind.Absolute));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}