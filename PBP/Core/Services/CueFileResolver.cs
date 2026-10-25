using System.Text.RegularExpressions;

namespace PBP.Core.Services;

public static class CueFileResolver
{
    private static readonly Regex FileRegex = new(@"^FILE\s+""(.*?)""\s+(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileNoQuoteRegex = new(@"^FILE\s+(.*?)\s+(BINARY|MOTOROLA|AIFF|WAVE|MP3)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string GetBinPath(string cuePath)
    {
        var referencedFiles = GetAllReferencedFiles(cuePath);

        if (referencedFiles.Count == 0)
            throw new Exception($"{Path.GetFileName(cuePath)}에서 FILE 항목을 찾을 수 없습니다.");

        return referencedFiles[0];
    }

    public static List<string> GetAllReferencedFiles(string cuePath)
    {
        var dir = Path.GetDirectoryName(cuePath) ?? string.Empty;
        var results = new List<string>();

        foreach (var line in File.ReadLines(cuePath))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                continue;

            string? fileName = null;
            var match = FileRegex.Match(trimmed);

            if (match.Success)
                fileName = match.Groups[1].Value;
            else
            {
                var noQuoteMatch = FileNoQuoteRegex.Match(trimmed);

                if (noQuoteMatch.Success)
                    fileName = noQuoteMatch.Groups[1].Value;
            }

            if (!string.IsNullOrEmpty(fileName))
                results.Add(Path.Combine(dir, fileName));
        }

        return results;
    }
}