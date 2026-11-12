using System.Text;
using Common.Localization;
using WiiGC.Core.Models.Wii;

namespace RomForge.Core.Services.Wii;

public sealed record GameDisplay(string Title, string Publisher, string GameId, string TitleId, string Region, string RegionKey)
{
    private static readonly Dictionary<string, string> Publishers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "Nintendo",
        ["08"] = "Capcom",
        ["41"] = "Ubisoft",
        ["4F"] = "Eidos",
        ["4Q"] = "Disney Interactive",
        ["51"] = "Acclaim",
        ["52"] = "Activision",
        ["5D"] = "Midway",
        ["64"] = "LucasArts",
        ["69"] = "Electronic Arts",
        ["6S"] = "TDK Mediactive",
        ["70"] = "Infogrames",
        ["78"] = "THQ",
        ["7D"] = "Vivendi Universal",
        ["8P"] = "Sega",
        ["99"] = "Pack-In-Video",
        ["9B"] = "Tecmo",
        ["AF"] = "Namco",
        ["B2"] = "Bandai",
        ["C8"] = "Koei",
        ["EM"] = "Konami",
        ["GD"] = "Square Enix"
    };

    public string IdText => $"{GameId} · {TitleId}";

    public static GameDisplay From(DiscInfo info)
    {
        string gameId = info.GameId;
        string title = string.IsNullOrWhiteSpace(info.Title) ? gameId : info.Title;
        string makerCode = gameId.Length >= 6 ? gameId[4..6] : string.Empty;
        string publisher = Publishers.TryGetValue(makerCode, out string? name) ? name : Loc.S.LabelMakerCode(makerCode);
        string titleId = gameId.Length >= 4 ? "00010000-" + Convert.ToHexString(Encoding.ASCII.GetBytes(gameId[..4])) : string.Empty;
        char regionChar = gameId.Length >= 4 ? gameId[3] : '?';
        string regionKey = gameId.Length >= 4 ? $"WII-P-{gameId[..4]}" : string.Empty;

        string region = regionChar switch
        {
            'J' => Loc.S.RegionJapan,
            'E' => Loc.S.RegionNorthAmerica,
            'K' => Loc.S.RegionKorea,
            'W' => Loc.S.RegionTaiwan,
            'P' or 'X' or 'Y' or 'Z' or 'D' or 'F' or 'S' or 'I' or 'H' or 'U' => Loc.S.RegionEuropePal,
            _ => Loc.S.LabelOther
        };

        return new GameDisplay(title, publisher, gameId, titleId, region, regionKey);
    }
}