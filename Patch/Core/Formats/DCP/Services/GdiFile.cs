using Patch.Core.Formats.DCP.Models;
using System.Globalization;

namespace Patch.Core.Formats.DCP.Services;

public class GdiFile
{
    public List<GdiTrack> Tracks { get; } = [];

    public string BasePath { get; private set; } = string.Empty;

    public string GdiPath { get; private set; } = string.Empty;


    public GdiTrack DataTrack => Tracks.Where(t => t.Type == TrackType.Data).OrderByDescending(t => t.Number).First();

    public static GdiFile Parse(string? gdiPath)
    {
        if (!File.Exists(gdiPath))
            throw new FileNotFoundException($".gdi 파일을 찾을 수 없습니다: {gdiPath}");

        var result = new GdiFile 
        { 
            BasePath = Path.GetDirectoryName(Path.GetFullPath(gdiPath)) ?? string.Empty,
            GdiPath = Path.GetFullPath(gdiPath)
        };
        var lines = File.ReadAllLines(gdiPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 1)
            throw new InvalidDataException(".gdi 파일 형식이 올바르지 않습니다.");

        if (!int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trackCount))
            throw new InvalidDataException(".gdi 트랙 개수를 읽을 수 없습니다.");

        for (int i = 1; i <= trackCount && i < lines.Length; i++)
        {
            var tokens = SplitLine(lines[i]);

            if (tokens.Count < 5)
                throw new InvalidDataException($".gdi 트랙 라인 형식이 올바르지 않습니다: {lines[i]}");

            result.Tracks.Add(new GdiTrack
            {
                Number = int.Parse(tokens[0], CultureInfo.InvariantCulture),
                StartLba = long.Parse(tokens[1], CultureInfo.InvariantCulture),
                Type = (TrackType)int.Parse(tokens[2], CultureInfo.InvariantCulture),
                SectorSize = int.Parse(tokens[3], CultureInfo.InvariantCulture),
                FileName = tokens[4].Trim('"'),
                FileOffset = tokens.Count > 5 ? long.Parse(tokens[5], CultureInfo.InvariantCulture) : 0
            });
        }

        return result.Tracks.Count == trackCount ? result : throw new InvalidDataException($".gdi 선언된 트랙 수({trackCount})와 실제 파싱된 트랙 수({result.Tracks.Count})가 다릅니다.");
    }

    public string GetTrackFullPath(GdiTrack track) => Path.Combine(BasePath, track.FileName);

    private static List<string> SplitLine(string line)
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