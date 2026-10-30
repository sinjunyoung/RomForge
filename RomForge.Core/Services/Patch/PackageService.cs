using RomForge.Core.Models.Patch;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public static partial class PackageService
{
    [GeneratedRegex(@"CRC\(([0-9a-fA-F]+)\)")]
    private static partial Regex CrcPatternRegex();

    public static Package ParseDatFile(string fileName, string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '\uFEFF')
            lines[0] = lines[0][1..];

        var entries = new List<PackageEntry>();
        int i = 0;

        for (; i < lines.Length; i++)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                break;

            var parts = line.Split('\t');

            if (parts.Length < 3)
                break;

            var crcMatch = CrcPatternRegex().Match(parts[2]);

            if (!crcMatch.Success)
                break;

            entries.Add(new PackageEntry
            {
                SourceFileName = parts[0].Trim(),
                PatchBaseName = parts[1].Trim(),
                Crc = crcMatch.Groups[1].Value.ToLowerInvariant()
            });
        }

        string? koTitle = null;
        string? firstTitle = null;

        for (; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith('[') || !line.EndsWith(']'))
                continue;

            var locale = line[1..^1];
            string? title = null;

            for (int j = i + 1; j < lines.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(lines[j]))
                    continue;

                if (lines[j].Trim().StartsWith('['))
                    break;

                title = lines[j].Trim();

                break;
            }

            firstTitle ??= title;

            if (string.Equals(locale, "ko_KR", StringComparison.OrdinalIgnoreCase))
            {
                koTitle = title;
                break;
            }
        }

        return new Package
        {
            DisplayName = koTitle ?? firstTitle ?? Path.GetFileNameWithoutExtension(fileName),
            DatFileName = fileName,
            Entries = entries
        };
    }
}