using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.WiiU;

public class TitleInputEntry(string filePath, string titleIdHex) : ViewModelBase
{
    public bool IsFolder { get; init; }

    public int SubTitleIndex { get; init; }

    public string TitleIdHex { get; init; } = titleIdHex;

    public int TitleVersion { get; init; }

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public string? TitleName { get; init; }

    public ImageSource? Icon { get; init; }

    private TitleRole _role = GuessRole(titleIdHex);

    public TitleRole Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); OnPropertyChanged(nameof(Kind)); OnPropertyChanged(nameof(KindBackground)); }
    }

    public string Kind => Role switch
    {
        TitleRole.Base => "본편",
        TitleRole.Update => "업데이트",
        TitleRole.Dlc => "DLC",
        _ => "알수없음",
    };

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
        set
        {
            _patchPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchDisplay));
            OnPropertyChanged(nameof(PatchIconSource));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(TitleName) ? FilePath : TitleName!;

    public string TitleIdVersionDisplay => $"{TitleIdHex}_v{TitleVersion}";

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : PatchPath;

    public string PatchIconSource => string.IsNullOrEmpty(PatchPath) ? "/Assets/Images/NoPatch.png" : "/Assets/Images/Patch.png";

    public static TitleRole GuessRole(string titleIdHex)
    {
        if (titleIdHex.Length < 8)
            return TitleRole.Unknown;

        return titleIdHex[..8].ToLowerInvariant() switch
        {
            "00050000" => TitleRole.Base,
            "0005000e" => TitleRole.Update,
            "0005000c" => TitleRole.Dlc,
            _ => TitleRole.Unknown,
        };
    }

    public ulong GetRoleCorrectedTitleId()
    {
        ulong titleId = Convert.ToUInt64(TitleIdHex, 16);
        uint category = Role switch
        {
            TitleRole.Base => 0x00050000u,
            TitleRole.Update => 0x0005000Eu,
            TitleRole.Dlc => 0x0005000Cu,
            _ => (uint)(titleId >> 32),
        };

        uint uniqueId = (uint)(titleId & 0xFFFFFFFFu);

        return ((ulong)category << 32) | uniqueId;
    }

    public string RoleCorrectedTitleIdHex => GetRoleCorrectedTitleId().ToString("x16");

    public Brush KindBackground
    {
        get
        {
            return Role switch
            {
                TitleRole.Base => new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)),
                TitleRole.Update => new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C)),
                TitleRole.Dlc => new SolidColorBrush(Color.FromRgb(0xC9, 0x7B, 0xF7)),
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
                long size = IsFolder ? GetDirectorySize(new DirectoryInfo(FilePath)) : new FileInfo(FilePath).Length;

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