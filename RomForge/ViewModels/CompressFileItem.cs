using Common.WPF.ViewModels;
using RomZip.Core.Enums;
using RomZip.Core.Services;
using System.IO;
using System.Windows.Media;

namespace RomForge.ViewModels;

public class CompressFileItem : ViewModelBase
{
    private int _progress;
    private string _status = string.Empty;
    private int _no;

    public string FilePath { get; }

    public string FileName  => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.');

    public string ExtensionLabel
    {
        get
        {
            var detected = FormatDetector.Detect(FilePath);

            if (detected.Format == RomFormat.Unknown || string.IsNullOrEmpty(detected.OutputExtension))
                return Extension;

            return $"{Extension}→{detected.OutputExtension}";
        }
    }

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileSize  { get; }

    public long FileSizeBytes { get; }

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

    public Brush ExtensionBackground => Extension.ToLowerInvariant() switch
    {
        "chd" => Brush("#A2C4FC"),
        "iso" => Brush("#FFF9A6"),
        "cue" => Brush("#EAE2A6"),
        "gdi" => Brush("#D2DAA5"),

        "nsp" => Brush("#FFA4B3"),
        "xci" => Brush("#FFB1C1"),
        "nsz" => Brush("#E65C7B"),
        "xcz" => Brush("#CC4466"),

        "3ds" => Brush("#FFE094"),
        "cci" => Brush("#FFCE73"),
        "cia" => Brush("#C96F2C"),
        "zcci" => Brush("#D48843"),

        "gcm" => Brush("#C9BFFF"),
        "gcz" => Brush("#9485EA"),
        "wbfs" => Brush("#B6D0FF"),
        "wia" => Brush("#7A9CE6"),
        "rvz" => Brush("#E2CEFF"),

        _ => Brushes.Transparent
    };

    public CompressFileItem(string filePath)
    {
        FilePath = filePath;
        FileSizeBytes = CalculateTotalSize(filePath);
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes);
    }

    public static long CalculateTotalSize(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        long SumFiles(IEnumerable<string> paths) => paths.Where(File.Exists).Sum(p => new FileInfo(p).Length);
        long ParsedSum(IEnumerable<string> referencedFiles) => new FileInfo(filePath).Length + SumFiles(referencedFiles.Select(f => Path.Combine(dir, f)));

        return ext switch
        {
            "cue" => ParsedSum(
                File.ReadLines(filePath)
                    .Where(l => l.TrimStart().StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                    .Select(l =>
                    {
                        var start = l.IndexOf('"') + 1;
                        var end = l.IndexOf('"', start);
                        return start > 0 && end > start ? l[start..end] : string.Empty;
                    })
                    .Where(f => !string.IsNullOrEmpty(f))),

            "gdi" => ParsedSum(
                File.ReadLines(filePath)
                    .Skip(1)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l =>
                    {
                        var parts = l.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        return parts.Length >= 5 ? parts[4] : string.Empty;
                    })
                    .Where(f => !string.IsNullOrEmpty(f))),

            _ => new FileInfo(filePath).Length
        };
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);

        brush.Freeze();

        return brush;
    }
}