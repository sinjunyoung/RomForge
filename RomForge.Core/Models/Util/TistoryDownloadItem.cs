using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.Core.Models.Util;

public class TistoryDownloadItem(string url) : ProcessableItemBase("대기중")
{
    private string _fileName = GuessFileName(url);
    private long _fileSizeBytes;
    private string? _savedPath;
    private bool _isSelected = true;

    public string Url { get; } = url;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set
        {
            if (SetProperty(ref _fileSizeBytes, value))
                OnPropertyChanged(nameof(FileSize));
        }
    }

    public string FileSize => FileSizeBytes > 0 ? PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes) : "-";

    public string? SavedPath
    {
        get => _savedPath;
        set => SetProperty(ref _savedPath, value);
    }

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