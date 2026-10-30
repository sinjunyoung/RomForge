using RomForge.Core.ViewModels;
using System.IO;

namespace RomForge.Core.Models.Util;

public class TistoryDownloadItem(string url) : ProcessableItemBase("대기중")
{
    public string Url { get; } = url;

    public bool IsSelected { get; set => SetProperty(ref field, value); }

    public string FileName { get; set => SetProperty(ref field, value); } = GuessFileName(url);

    public long FileSizeBytes
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(FileSize));
        }
    }

    public string FileSize => FileSizeBytes > 0 ? PickPack.Disk.Helpers.FileSize.FormatSize(FileSizeBytes) : "-";

    public string? SavedPath { get; set => SetProperty(ref field, value); }

    private static string GuessFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrWhiteSpace(name) || name.Contains('?'))
                return $"file_{Guid.NewGuid().ToString()[..8]}";

            return Uri.UnescapeDataString(name);
        }
        catch
        {
            return $"file_{Guid.NewGuid().ToString()[..8]}";
        }
    }
}