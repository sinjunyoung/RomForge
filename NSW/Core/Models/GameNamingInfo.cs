using LibHac.Ncm;

namespace NSW.Core.Models;

public sealed record GameNamingInfo(string KoreanTitle, string DefaultTitle, string KrTitle, string EnTitle, string DisplayVersion, uint TitleVersion, int DlcCount)
{
    public static GameNamingInfo From(IEnumerable<MetadataResult> metas)
    {
        var all = metas
            .GroupBy(m => new { m.TitleId, m.TitleVersion, m.Type })
            .Select(g => g.First())
            .ToList();

        if (all.Count == 0)
            return new GameNamingInfo(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0);

        int dlcCount = all.Count(m => m.Type == ContentMetaType.AddOnContent);
        var latestPatch = all.Where(m => m.Type == ContentMetaType.Patch).OrderByDescending(m => m.TitleVersion).FirstOrDefault();
        var baseGame = all.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? all[0];
        var versionSource = latestPatch ?? baseGame;
        var titleSources = new[] { baseGame, latestPatch }.OfType<MetadataResult>().Concat(all).ToList();
        string koreanTitle = titleSources.Select(m => m.KoreanTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;
        string defaultTitle = titleSources.Select(m => m.DefaultTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;

        return new GameNamingInfo(koreanTitle, defaultTitle, baseGame.KrTitle, baseGame.EnTitle, versionSource.GetEffectiveDisplayVersion(), versionSource.TitleVersion, dlcCount);
    }
}