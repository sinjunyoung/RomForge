using Common.WPF.ViewModels;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.WiiU;

public class TitleInputEntry(string filePath, string titleIdHex) : ViewModelBase
{
    public bool IsFolder { get; init; }

    public int SubTitleIndex { get; init; }

    public string Kind { get; init; } = GuessKind(titleIdHex);

    public string TitleIdHex { get; init; } = titleIdHex;

    public int TitleVersion { get; init; }

    public int FileCount { get; init; }

    public string? TitleName { get; init; }

    public ImageSource? Icon { get; init; }

    private string? _filePath;
    public string FilePath
    {
        get => _filePath ?? filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set { _patchPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PatchDisplay)); }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(TitleName) ? FilePath : TitleName!;

    public string TitleIdVersionDisplay => $"{TitleIdHex}_v{TitleVersion}";

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : PatchPath;

    private static string GuessKind(string titleIdHex)
    {
        if (titleIdHex.Length < 8)
            return "알수없음";

        return titleIdHex[..8].ToLowerInvariant() switch
        {
            "00050000" => "본편",
            "0005000e" => "업데이트",
            "0005000c" => "DLC",
            _ => "알수없음",
        };
    }

    public Brush KindBackground
    {
        get
        {
            return Kind switch
            {
                "본편" => new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)),
                "업데이트" => new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C)),
                "DLC" => new SolidColorBrush(Color.FromRgb(0xC9, 0x7B, 0xF7)),
                _ => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A)),
            };
        }
    }

    public string Size
    {
        get
        {
            try
            {
                long size = IsFolder
                    ? GetDirectorySize(new DirectoryInfo(FilePath))
                    : new FileInfo(FilePath).Length;

                return PickPack.Disk.ETC.FileSize.FormatSize(size);
            }
            catch
            {
                return "0";
            }
        }
    }

    private static long GetDirectorySize(DirectoryInfo directoryInfo)
    {
        long size = 0;
        foreach (FileInfo file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            size += file.Length;

        return size;
    }
}