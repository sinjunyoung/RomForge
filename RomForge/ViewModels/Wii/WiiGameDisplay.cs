using System.Text;
using WiiGC.Core.Models;

namespace RomForge.ViewModels.Wii;

public sealed record WiiGameDisplay(string Title, string Publisher, string GameId, string TitleId, string Region, string RegionKey)
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

    public static WiiGameDisplay From(WiiDiscInfo info)
    {
        string gameId = info.GameId;
        string title = string.IsNullOrWhiteSpace(info.Title) ? gameId : info.Title;
        string makerCode = gameId.Length >= 6 ? gameId[4..6] : string.Empty;
        string publisher = Publishers.TryGetValue(makerCode, out string? name) ? name : $"제작사 코드 {makerCode}";
        string titleId = gameId.Length >= 4 ? "00010000-" + Convert.ToHexString(Encoding.ASCII.GetBytes(gameId[..4])) : string.Empty;
        char regionChar = gameId.Length >= 4 ? gameId[3] : '?';
        string regionKey = gameId.Length >= 4 ? $"WII-P-{gameId[..4]}" : string.Empty;

        string region = regionChar switch
        {
            'J' => "일본",
            'E' => "북미",
            'K' => "한국",
            'W' => "대만",
            'P' or 'X' or 'Y' or 'Z' or 'D' or 'F' or 'S' or 'I' or 'H' or 'U' => "유럽/PAL",
            _ => "기타"
        };

        return new WiiGameDisplay(title, publisher, gameId, titleId, region, regionKey);
    }
}