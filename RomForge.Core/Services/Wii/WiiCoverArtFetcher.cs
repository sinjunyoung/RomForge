using System.IO;
using System.Net.Http;

namespace RomForge.Core.Services.Wii;

public static class WiiCoverArtFetcher
{
    private const string BaseUrl = "https://art.gametdb.com/wii/disc";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string CacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "wii");

    public static async Task<byte[]?> TryGetDiscPngAsync(string gameId, CancellationToken ct = default)
    {
        if (gameId.Length != 6)
            return null;

        string cachePath = Path.Combine(CacheDirectory, gameId + ".png");

        try
        {
            if (File.Exists(cachePath))
                return await File.ReadAllBytesAsync(cachePath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch { }

        foreach (string language in GetLanguages(gameId[3]))
        {
            try
            {
                byte[] bytes = await Http.GetByteArrayAsync($"{BaseUrl}/{language}/{gameId}.png", ct);

                try
                {
                    Directory.CreateDirectory(CacheDirectory);

                    await File.WriteAllBytesAsync(cachePath, bytes, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch { }

                return bytes;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch { }
        }

        return null;
    }

    private static string[] GetLanguages(char region) => region switch
    {
        'E' => ["US"],
        'J' => ["JA"],
        'K' => ["KO"],
        'W' => ["ZHTW"],
        'D' => ["DE"],
        'F' => ["FR"],
        'S' => ["ES"],
        'I' => ["IT"],
        'H' => ["NL"],
        _ => ["EN", "US"]
    };
}