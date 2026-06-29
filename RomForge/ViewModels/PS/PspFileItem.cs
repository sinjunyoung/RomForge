using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.ViewModels.PS;

public class PspFileItem : ViewModelBase
{
    private string _status = string.Empty;
    private string _selectedTargetFormat = string.Empty;
    private int _progress;
    private int _no;

    public string FilePath { get; }
    public string FileName => Path.GetFileNameWithoutExtension(FilePath);
    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public string FileSize { get; }
    public long FileSizeBytes { get; }
    public List<string> AvailableFormats { get; private set; } = [];

    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set { _selectedTargetFormat = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public int No
    {
        get => _no;
        set { _no = value; OnPropertyChanged(); }
    }

    public Brush ExtensionBackground => Extension switch
    {
        "iso" => Brush("#94C8FF"),
        "cso" => Brush("#94FFB5"),
        "chd" => Brush("#D494FF"),
        _ => Brushes.Transparent
    };

    public PspFileItem(string filePath)
    {
        FilePath = filePath;
        FileSizeBytes = new FileInfo(filePath).Length;
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes);

        InitAvailableFormats();
    }

    private void InitAvailableFormats()
    {
        AvailableFormats.Clear();

        switch (Extension)
        {
            case "iso":
                AvailableFormats.AddRange(["CSO", "CHD"]);
                SelectedTargetFormat = "CSO";
                break;

            case "cso":
                AvailableFormats.AddRange(["ISO", "CHD"]);
                SelectedTargetFormat = "ISO";
                break;

            case "chd":
                AvailableFormats.AddRange(["ISO", "CSO"]);
                SelectedTargetFormat = "ISO";
                break;

            default:
                SelectedTargetFormat = "미지원";
                break;
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);

        brush.Freeze();

        return brush;
    }
}