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

    public string? TitleName { get; init; }

    public ImageSource? Icon { get; init; }

    private TitleRole _role = GuessRole(titleIdHex);

    /// <summary>이 항목의 실제 역할(본편/업데이트/DLC). WUA 하나에 여러 타이틀이 번들된 경우가 아니면
    /// title ID만으로는 신뢰할 수 없어서(업데이트/DLC 폴더도 본편 카테고리로 찍혀있는 경우가 흔함),
    /// 사용자가 어떤 버튼으로 추가했는지에 따라 명시적으로 설정된다.</summary>
    public TitleRole Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); OnPropertyChanged(nameof(Kind)); OnPropertyChanged(nameof(KindBackground)); }
    }

    /// <summary>화면 표시용. Role 기준으로 계산되며, title ID로 추측한 값이 아니다.</summary>
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
        set { _patchPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PatchDisplay)); }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(TitleName) ? FilePath : TitleName!;

    public string TitleIdVersionDisplay => $"{TitleIdHex}_v{TitleVersion}";

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : PatchPath;

    /// <summary>title ID 앞 8자리(카테고리)만 보고 본편/업데이트/DLC를 추측한다. WUA 하나에 여러 타이틀이
    /// 정상적으로 번들된 경우에만 신뢰할 수 있다 — wud/wux나 단일 폴더 입력에는 이 값이 실제로 맞다는
    /// 보장이 없으니 그런 경우는 항상 사용자가 고른 Role로 덮어써야 한다.</summary>
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

    /// <summary>Role에 맞춰 title ID의 상위 4바이트(카테고리)만 바로잡은 64비트 값.
    /// 하위 4바이트(고유 게임 ID)는 그대로 둔다. 패키징(WUA/WUP) 시점에 반드시 이 값을 써야
    /// 업데이트/DLC 폴더가 본편 카테고리 title ID를 갖고 있는 흔한 케이스에서도 결과물이 올바르게 나온다.</summary>
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