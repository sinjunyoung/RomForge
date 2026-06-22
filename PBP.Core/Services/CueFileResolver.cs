namespace PBP.Core.Services;

public static class CueFileResolver
{
    public static string GetBinPath(string cuePath)
    {
        var line = File.ReadLines(cuePath)
            .FirstOrDefault(l => l.TrimStart().StartsWith("FILE", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"{Path.GetFileName(cuePath)}에서 FILE 항목을 찾을 수 없습니다.");

        var match = System.Text.RegularExpressions.Regex.Match(line, "\"(.+?)\"");
        var binFileName = match.Success ? match.Groups[1].Value : line.Split(' ', 2)[1].Trim('"');

        return Path.Combine(Path.GetDirectoryName(cuePath)!, binFileName);
    }

    public static List<string> GetAllReferencedFiles(string cuePath)
    {
        var dir = Path.GetDirectoryName(cuePath)!;
        var results = new List<string>();

        foreach (var line in File.ReadLines(cuePath))
        {
            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase)) 
                continue;

            var match = System.Text.RegularExpressions.Regex.Match(trimmed, "\"(.+?)\"");
            var fileName = match.Success ? match.Groups[1].Value : trimmed.Split(' ', 3)[1].Trim('"');

            results.Add(Path.Combine(dir, fileName));
        }

        return results;
    }
}