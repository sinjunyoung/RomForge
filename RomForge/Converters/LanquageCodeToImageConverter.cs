using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RomForge.Converters
{
    public class LanquageCodeToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string code = value?.ToString() ?? string.Empty;

            string fileName = code.ToUpperInvariant() switch
            {
                "JA" or "JA-JP" or "J" => "japanese.png",
                "KO" or "KO-KR" or "K" => "korean.png",
                "EN" or "EN-US" or "EN-GB" or "E" or "U" or "N" => "american.png",
                "FR" or "FR-FR" or "F" => "french.png",
                "DE" or "DE-DE" or "G" => "german.png",
                "ES" or "ES-ES" or "S" => "spanish.png",
                "IT" or "IT-IT" or "I" => "italian.png",
                "ZH" or "ZH-CN" or "ZH-CHS" or "C" => "chinese.png",
                "ZH-TW" or "ZH-CHT" or "T" => "taiwanese.png",
                "RU" or "RU-RU" or "R" => "russian.png",
                _ => "world.png"
            };

            try
            {
                string path = $"pack://application:,,,/RomForge.Core;component/Assets/Images/{fileName}";
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}