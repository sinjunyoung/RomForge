using RomForge.Core.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.PSP;

public class DiscFileItem(string filePath) : ProcessableItemBase
{
    public string FilePath { get; } = filePath;

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    private string _gameId = "인식중...";
    public string GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private long _fileSizeBytes;
    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set
        {
            if (SetProperty(ref _fileSizeBytes, value))
                OnPropertyChanged(nameof(FileSize));
        }
    }

    public string FileSize => FileSizeBytes <= 0 ? "..." : FileSizeBytes >= 1024L * 1024 * 1024 ? $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB" : $"{FileSizeBytes / (1024.0 * 1024):F1} MB";

    public byte[]? PresetConfigBytes { get; set; }
    public bool HasPresetConfig => PresetConfigBytes != null;

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["chd"] = "#A2C4FC",
        ["iso"] = "#FFF9A6",
        ["cue"] = "#EAE2A6",
        ["m3u"] = "#D2DAA5",
    };
}