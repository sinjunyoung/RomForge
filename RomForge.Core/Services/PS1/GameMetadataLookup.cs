using PBP.Core.Services;
using RomForge.Core.Models.PS1;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace RomForge.Core.Services.PS1;

public static class GameMetadataLookup
{
    private static readonly HttpClient Http = new();
    private static Dictionary<string, GameItem>? _cache;
    private static readonly JsonSerializerOptions options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static GameItem? Find(string gameId)
    {
        _cache ??= Load();

        return _cache.GetValueOrDefault(gameId);
    }

    public static async Task<byte[]?> TryDownloadImagePngAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) 
            return null;

        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct);

            return ImageConversion.ToPng(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, GameItem> Load()
    {
        var gameDict = new Dictionary<string, GameItem>();        
        var zipBytes = EmbeddedAssetProvider.GetGamesDatabase();
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var jsonEntry = archive.Entries[0];
        using var entryStream = jsonEntry.Open();
        var rawList = JsonSerializer.Deserialize<List<GameItem>>(entryStream, options);

        if (rawList != null)
        {
            foreach (var item in rawList)
            {
                if (string.IsNullOrEmpty(item.Id))
                    continue;

                gameDict.TryAdd(item.Id, item);
            }
        }

        return gameDict;
    }
}