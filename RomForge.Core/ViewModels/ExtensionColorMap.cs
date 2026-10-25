using System.Windows.Media;

namespace RomForge.Core.ViewModels;

public static class ExtensionColorMap
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = [];

    public static Brush Resolve(string extension, IReadOnlyDictionary<string, string> map, string fallbackHex = "#00000000")
    {
        var key = extension.ToLowerInvariant();

        if (!map.TryGetValue(key, out var hex))
            hex = fallbackHex;

        return GetOrCreateBrush(hex);
    }

    private static SolidColorBrush GetOrCreateBrush(string hex)
    {
        if (Cache.TryGetValue(hex, out var cached))
            return cached;

        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);

        brush.Freeze();

        Cache[hex] = brush;

        return brush;
    }
}