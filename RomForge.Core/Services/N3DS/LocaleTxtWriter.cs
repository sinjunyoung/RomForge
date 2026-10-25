using N3DS.Core.Enums;
using System.IO;

namespace RomForge.Core.Services.N3DS;

public static class LocaleTxtWriter
{
    public static readonly Dictionary<LocaleLanguage, LocaleRegion> LanguageToRegion = new()
    {
        [LocaleLanguage.JP] = LocaleRegion.JPN,
        [LocaleLanguage.EN] = LocaleRegion.USA,
        [LocaleLanguage.FR] = LocaleRegion.EUR,
        [LocaleLanguage.DE] = LocaleRegion.EUR,
        [LocaleLanguage.IT] = LocaleRegion.EUR,
        [LocaleLanguage.ES] = LocaleRegion.EUR,
        [LocaleLanguage.ZH] = LocaleRegion.CHN,
        [LocaleLanguage.KO] = LocaleRegion.KOR,
        [LocaleLanguage.NL] = LocaleRegion.EUR,
        [LocaleLanguage.PT] = LocaleRegion.EUR,
        [LocaleLanguage.RU] = LocaleRegion.EUR,
        [LocaleLanguage.TW] = LocaleRegion.TWN,
    };

    public static async Task WriteAsync(string sdRoot, string titleId, LocaleLanguage language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sdRoot))
            throw new ArgumentException("SD 루트 경로가 비어있습니다.", nameof(sdRoot));

        if (string.IsNullOrEmpty(titleId) || titleId.Length != 16)
            throw new ArgumentException("타이틀 ID가 올바르지 않습니다.", nameof(titleId));

        if (language == LocaleLanguage.None)
            return;

        if (!LanguageToRegion.TryGetValue(language, out LocaleRegion region))
            return;

        string dir = Path.Combine(sdRoot, "luma", "titles", titleId.ToUpperInvariant());

        Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, "locale.txt");
        string content = $"{region} {language}";

        await File.WriteAllTextAsync(path, content, ct);
    }
}