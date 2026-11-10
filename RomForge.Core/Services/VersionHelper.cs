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

    public static async Task<GitHubRelease?> GetUpdateAsync()
    {
        var latestRelease = await GetLatestReleaseAsync();

        if (latestRelease == null)
            return null;

        var currentVersion = GetAppVersion();

        return IsNewerVersion(currentVersion, latestRelease.TagName) ? latestRelease : null;
    }

    private static async Task<GitHubRelease?> GetLatestReleaseAsync()
    {
        const string url = "https://api.github.com/repos/sinjunyoung/RomForge/releases/latest";

        var json = await _httpClient.GetStringAsync(url);

        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Revision}" : "1.0.0";
    }

    private static bool IsNewerVersion(string? current, string? latest)
    {
        if (!Version.TryParse(current?.TrimStart('v'), out var currentVersion))
            currentVersion = new Version(0, 0, 0);

        if (!Version.TryParse(latest?.TrimStart('v'), out var latestVersion))
            return false;

        return latestVersion > currentVersion;
    }
}