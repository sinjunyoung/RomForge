using Common.WPF.ViewModels;
using System.Windows.Media;

namespace RomForge.Core.Models.WiiU;

public sealed class TitleInputEntry : ViewModelBase
{
    public string Path { get; init; } = "";

    public bool IsFolder { get; init; }

    public int SubTitleIndex { get; init; }

    public string Kind { get; init; } = "알수없음";

    public string TitleIdHex { get; init; } = "0000000000000000";

    public int TitleVersion { get; init; }

    public int FileCount { get; init; }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set { _patchPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PatchDisplay)); }
    }

    public string Summary => $"{TitleIdHex}_v{TitleVersion}  ({FileCount:N0}개 파일)";

    public string SourceDisplay => IsFolder ? Path : System.IO.Path.GetFileName(Path);

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : System.IO.Path.GetFileName(PatchPath);

    public static string GuessKind(string titleIdHex)
    {
        if (titleIdHex.Length < 8) 
            return "알수없음";

        return titleIdHex[..8].ToLowerInvariant() switch
        {
            "00050000" => "베이스",
            "0005000e" => "업데이트",
            "0005000c" => "DLC",
            _ => "알수없음",
        };
    }

    public Brush KindBackground
    {
        get
        {
            //if (IsKeyMissing)
            //    return new SolidColorBrush(Color.FromArgb(60, 220, 50, 50));

            return Kind switch
            {
                "베이스" => new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)),
                "업데이트" => new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C)),
                "DLC" => new SolidColorBrush(Color.FromRgb(0xC9, 0x7B, 0xF7)),
                _ => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A)),
            };
        }
    }
}