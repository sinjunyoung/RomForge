using CHD.Core.Models.Enums;
using System.Text.RegularExpressions;

namespace CHD.Core.Services;

public class ConversionSource
{
    public InputFormat Format { get; init; }

    public string PrimaryFile { get; init; }

    public IReadOnlyList<string> BinFiles { get; init; } = [];

    public static ConversionSource FromPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("경로가 비어있습니다.", nameof(filePath));

        filePath = Path.GetFullPath(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".chd" => new ConversionSource { Format = InputFormat.Chd, PrimaryFile = filePath },
            ".iso" => new ConversionSource { Format = InputFormat.Iso, PrimaryFile = filePath },
            ".cue" => FromCue(filePath),
            ".bin" => FromBin(filePath),
            ".gdi" => new ConversionSource { Format = InputFormat.Gdi, PrimaryFile = filePath },
            _ => new ConversionSource { Format = InputFormat.Unknown, PrimaryFile = filePath }
        };
    }

    private static ConversionSource FromCue(string cuePath)
    {
        var bins = ParseBinsFromCue(cuePath);
        return new ConversionSource
        {
            Format = InputFormat.BinCue,
            PrimaryFile = cuePath,
            BinFiles = bins
        };
    }

    private static ConversionSource FromBin(string binPath)
    {
        var dir = Path.GetDirectoryName(binPath)!;

        foreach (var cue in Directory.GetFiles(dir, "*.cue"))
        {
            var bins = ParseBinsFromCue(cue);
            if (bins.Any(b => string.Equals(b, binPath, StringComparison.OrdinalIgnoreCase)))
                return FromCue(cue);
        }

        return new ConversionSource { Format = InputFormat.Unknown, PrimaryFile = binPath };
    }

    public static IReadOnlyList<string> ParseBinsFromCue(string cuePath)
    {
        cuePath = Path.GetFullPath(cuePath);
        var dir = Path.GetDirectoryName(cuePath)!;
        var bins = new List<string>();

        if (!File.Exists(cuePath))
            return bins;

        foreach (var line in File.ReadAllLines(cuePath))
        {
            var match = Regex.Match(line.Trim(),
                @"^FILE\s+""(.+?)""\s+BINARY", RegexOptions.IgnoreCase);

            if (!match.Success) continue;

            var binName = match.Groups[1].Value;
            var fullPath = Path.IsPathRooted(binName)
                ? binName
                : Path.GetFullPath(Path.Combine(dir, binName));

            bins.Add(fullPath);
        }

        return bins;
    }

    public static IReadOnlyList<string> ParseFilesFromGdi(string gdiPath)
    {
        var files = new List<string>();

        if (!File.Exists(gdiPath))
            return files;

        var lines = File.ReadAllLines(gdiPath);

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length >= 5)
            {
                string fileName = tokens[4].Replace("\"", string.Empty);
                files.Add(fileName);
            }
        }

        return files;
    }

    public string Validate()
    {
        return Format switch
        {
            InputFormat.Unknown => $"지원하지 않는 파일 형식: {Path.GetExtension(PrimaryFile)}",
            InputFormat.BinCue => ValidateBinCue(),
            _ => File.Exists(PrimaryFile)
                    ? null
                    : $"파일을 찾을 수 없습니다: {Path.GetFileName(PrimaryFile)}"
        };
    }

    private string ValidateBinCue()
    {
        if (!File.Exists(PrimaryFile))
            return $"CUE 파일을 찾을 수 없습니다: {Path.GetFileName(PrimaryFile)}";

        if (BinFiles.Count == 0)
            return $"CUE 파일에 BIN 참조가 없습니다: {Path.GetFileName(PrimaryFile)}";

        var missing = BinFiles.FirstOrDefault(b => !File.Exists(b));
        if (missing != null)
            return $"BIN 파일을 찾을 수 없습니다: {Path.GetFileName(missing)}";

        return null;
    }
}