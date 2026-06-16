using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Common.WPF.Converters
{
    public class RegionToImageConverter : IValueConverter
    {

        public RegionToImageConverter() { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string productCode = value?.ToString();
            if (string.IsNullOrEmpty(productCode) || productCode.Length < 7) return null;

            try
            {
                char regionChar = productCode[productCode.IndexOf('-', 4) + 4];

                string fileName = regionChar switch
                {
                    'J' => "japanese.png",
                    'K' => "korean.png",
                    'E' or 'U' or 'N' => "american.png",
                    'P' => "european.png",
                    'A' => "australia.png",
                    'C' => "chinese.png",
                    'T' => "taiwanese.png",
                    'G' => "german.png",
                    'F' => "french.png",
                    'S' => "spanish.png",
                    'I' => "italian.png",
                    'H' => "netherlands.png",
                    'R' => "russian.png",
                    'W' or 'X' or 'Z' => "world.png",
                    _ => "unknown.png"
                };

                string path = $"pack://application:,,,/Common.WPF;component/Assets/Images/{fileName}";

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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}