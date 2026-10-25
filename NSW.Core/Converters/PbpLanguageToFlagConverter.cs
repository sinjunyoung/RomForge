using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NSW.Core.Converters
{
    public class PbpLanguageToFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string lang) 
                return null;

            string fileName = lang switch
            {
                "Chinese" => "chinese.png",
                "Czech" => "czech.png",
                "Danish" => "danish.png",
                "Dutch" => "dutch.png",
                "English" => "american.png",
                "Finnish" => "finnish.png",
                "French" => "french.png",
                "German" => "german.png",
                "Greek" => "greek.png",
                "Italian" => "italian.png",
                "Japanese" => "japanese.png",
                "Korean" => "korean.png",
                "Norwegian" => "norwegian.png",
                "Polish" => "polish.png",
                "Portuguese" => "portuguese.png",
                "Russian" => "russian.png",
                "Spanish" => "spanish.png",
                "Swedish" => "swedish.png",
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