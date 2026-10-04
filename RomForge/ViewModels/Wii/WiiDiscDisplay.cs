using Common;
using DolphinTool.Core.Models;

namespace RomForge.ViewModels.Wii;

public sealed record WiiDiscDisplay(string Title, string GameIdText, string DetailText, string Region, string RegionKey)
{
    public static WiiDiscDisplay From(WiiDiscInfo info)
    {
        string title = string.IsNullOrWhiteSpace(info.Title) ? info.GameId : info.Title;
        string regionKey = info.GameId.Length >= 4 ? $"WII-P-{info.GameId[..4]}" : string.Empty;
        char regionChar = info.GameId.Length >= 4 ? info.GameId[3] : '?';
        string format = info.Container switch
        {
            DiscContainerFormat.PlainDisc => "ISO",
            DiscContainerFormat.Wbfs => "WBFS",
            DiscContainerFormat.Rvz => "RVZ",
            DiscContainerFormat.Wia => "WIA",
            DiscContainerFormat.Gcz => "GCZ",
            _ => "알 수 없음"
        };
        string region = regionChar switch
        {
            'J' => "일본",
            'E' => "북미",
            'P' or 'X' or 'Y' or 'Z' or 'D' or 'F' or 'S' or 'I' or 'H' or 'U' => "유럽/PAL",
            'K' => "한국",
            'W' => "대만",
            _ => "기타"
        };

        return new WiiDiscDisplay(
            title,
            $"{info.GameId} · 디스크 {info.DiscNumber + 1} · Rev {info.Version}",
            $"{format} · 파일 {Utils.FormatFileSize(info.FileSize)} / 원본 {Utils.FormatFileSize(info.DiscSize)}",
            region,
            regionKey);
    }
}