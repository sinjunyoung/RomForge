using System.Net.Http;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.PSP;

public static partial class CoverArtFetcher
{
    [GeneratedRegex(@"^([A-Z]{4})(\d+)$")]
    private static partial Regex GameIdRegex();

    private const string BaseUrl = "https://raw.githubusercontent.com/sinjunyoung/psx-covers/main/covers/default";
    private static readonly HttpClient Http = new();

    public static async Task<byte[]?> TryDownloadIconPngAsync(string gameId, CancellationToken ct = default)
    {
        var dashed = ToDashedSerial(gameId);

        if (dashed == null) 
            return null;

        try
        {
            var jpgBytes = await Http.GetByteArrayAsync($"{BaseUrl}/{dashed}.jpg", ct);
            return ImageConversion.ToPng(jpgBytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? ToDashedSerial(string gameId)
    {
        var m = GameIdRegex().Match(gameId);
        return m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
    }
}