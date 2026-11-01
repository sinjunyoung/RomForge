using RomForge.Core.ViewModels.Switch;
using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class RepresentativeTitleNameConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Collections.IEnumerable items)
        {
            var gameFiles = items.Cast<GameFile>().Where(f => !string.IsNullOrEmpty(f.TitleID)).ToList();

            if (gameFiles.Count == 0) 
                return string.Empty;

            var baseGame = gameFiles.FirstOrDefault(f => f.TitleID != null && f.TitleID.EndsWith("000", StringComparison.OrdinalIgnoreCase));

            if (baseGame != null)
                return baseGame.TitleName;

            var updateGame = gameFiles.FirstOrDefault(f => f.TitleID != null && f.TitleID.EndsWith("800", StringComparison.OrdinalIgnoreCase));

            if (updateGame != null)
                return updateGame.TitleName;

            var dlcGames = gameFiles.Where(f => {
                string? suffix = f.TitleID?.Length >= 3 ? f.TitleID[^3..] : "";
                return int.TryParse(suffix, NumberStyles.HexNumber, null, out int val) && val > 0 && val != 0x800;
            }).OrderBy(f => {
                string? suffix = f.TitleID?[^3..];
                return System.Convert.ToInt32(suffix, 16);
            }).ToList();

            if (dlcGames.Count != 0) 
                return dlcGames.First().TitleName;

            return gameFiles.First().TitleName;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}