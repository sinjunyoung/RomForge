using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.ViewModels.PS;

public class DiscFileItem(string filePath) : ViewModelBase
{
    public string FilePath { get; } = filePath;

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    private int _no;
    public int No { get => _no; set { _no = value; OnPropertyChanged(); } }

    private string _gameId = "인식중...";
    public string GameId { get => _gameId; set { _gameId = value; OnPropertyChanged(); } }

    private long _fileSizeBytes;
    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set { _fileSizeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSize)); }
    }

    public string FileSize => FileSizeBytes <= 0 ? "..." : FileSizeBytes >= 1024L * 1024 * 1024
        ? $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
        : $"{FileSizeBytes / (1024.0 * 1024):F1} MB";

    public Brush ExtensionBackground => Extension.ToLowerInvariant() switch
    {
        "chd" => Brush("#A2C4FC"),
        "iso" => Brush("#FFF9A6"),
        "cue" => Brush("#EAE2A6"),
        "m3u" => Brush("#D2DAA5"),

        _ => Brushes.Transparent
    };

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);

        brush.Freeze();

        return brush;
    }
}