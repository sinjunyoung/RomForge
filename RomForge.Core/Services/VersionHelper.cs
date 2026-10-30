using RomForge.Core.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace RomForge.Core.Services;

public static class VersionHelper
{
    private static readonly HttpClient _httpClient = new();

    static VersionHelper() => _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RomForge", "1.0.0"));

    public static async Task<bool> IsUpdateAvailableAsync()
    {
        var latestRelease = await GetLatestReleaseAsync();
        var currentVersion = GetAppVersion();
        return IsNewerVersion(currentVersion, latestRelease.TagName);
    }

    private static async Task<GitHubRelease> GetLatestReleaseAsync()
    {
        var url = $"https://api.github.com/repos/sinjunyoung/RomForge/releases/latest";
        var json = await _httpClient.GetStringAsync(url);
        return JsonSerializer.Deserialize<GitHubRelease>(json)!;
    }


    private static string GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Revision}" : "1.0.0";
    }

    private static bool IsNewerVersion(string current, string? latest)
    {
        current = current?.TrimStart('v') ?? "0.0.0";
        latest = latest?.TrimStart('v') ?? "0.0.0";

        var currentParts = current.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var latestParts = latest.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var currentVersion = new int[]
        {
            currentParts.Length > 0 ? currentParts[0] : 0,
            currentParts.Length > 1 ? currentParts[1] : 0,
            currentParts.Length > 2 ? currentParts[2] : 0
        };
        var latestVersion = new int[]
        {
            latestParts.Length > 0 ? latestParts[0] : 0,
            latestParts.Length > 1 ? latestParts[1] : 0,
            latestParts.Length > 2 ? latestParts[2] : 0
        };

        for (int i = 0; i < 3; i++)
        {
            if (latestVersion[i] > currentVersion[i]) 
                return true;

            if (latestVersion[i] < currentVersion[i]) 
                return false;
        }

        return false;
    }
}