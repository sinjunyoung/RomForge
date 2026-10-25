using RomForge.Core.Services.Compression;
using RomForge.Core.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.Compression;

public class FileItem(string filePath) : FileItemBase(filePath)
{
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

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["chd"] = "#A2C4FC",
        ["iso"] = "#FFF9A6",
        ["cue"] = "#EAE2A6",
        ["gdi"] = "#D2DAA5",
        ["ccd"] = "#A6C8E2",

        ["nsp"] = "#FFA4B3",
        ["xci"] = "#FFB1C1",
        ["nsz"] = "#E65C7B",
        ["xcz"] = "#CC4466",

        ["3ds"] = "#FFE094",
        ["cci"] = "#FFCE73",
        ["cia"] = "#C96F2C",
        ["zcci"] = "#D48843",

        ["gcm"] = "#C9BFFF",
        ["gcz"] = "#9485EA",
        ["wbfs"] = "#B6D0FF",
        ["wia"] = "#7A9CE6",
        ["rvz"] = "#E2CEFF",
    };

    protected override long CalculateSize(string filePath)
    {
        var ext = Extension;
        var dir = Directory;
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
                        var parts = SplitGdiLine(l.Trim());

                        return parts.Count >= 5
                            ? parts[4].Trim('"')
                            : string.Empty;
                    })
                    .Where(f => !string.IsNullOrEmpty(f))),

            "ccd" => ParsedSum([
                Path.GetFileNameWithoutExtension(filePath) + ".img",
                Path.GetFileNameWithoutExtension(filePath) + ".sub"
            ]),

            _ => base.CalculateSize(filePath)
        };
    }

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);

    private static List<string> SplitGdiLine(string line)
    {
        var tokens = new List<string>();
        int i = 0;

        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            if (i >= line.Length)
                break;

            if (line[i] == '"')
            {
                int end = line.IndexOf('"', i + 1);

                if (end < 0)
                    end = line.Length - 1;

                tokens.Add(line.Substring(i, end - i + 1));

                i = end + 1;
            }
            else
            {
                int start = i;

                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;

                tokens.Add(line[start..i]);
            }
        }

        return tokens;
    }
}