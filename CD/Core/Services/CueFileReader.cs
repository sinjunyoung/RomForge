using CD.Core.Models;
using System.Text.RegularExpressions;

namespace CD.Core.Services;

public static class CueFileReader
{
    private static readonly Regex QuotedFileRegex = new(@"^FILE\s+""(.*?)""\s+(\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UnquotedFileRegex = new(@"^FILE\s+(.+?)\s+(BINARY|MOTOROLA|AIFF|WAVE|MP3)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrackRegex = new(@"^TRACK\s+(\d+)\s+(\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IndexRegex = new(@"^INDEX\s+(\d+)\s+(\d+):(\d+):(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CueFile Read(string filePath)
    {
        var cueFile = new CueFile { FilePath = filePath };

        CueFileEntry? entry = null;
        CueTrack? track = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');

            if (line.Length == 0)
                continue;

            var fileMatch = QuotedFileRegex.Match(line);

            if (!fileMatch.Success)
                fileMatch = UnquotedFileRegex.Match(line);

            if (fileMatch.Success)
            {
                entry = new CueFileEntry
                {
                    FileName = fileMatch.Groups[1].Value,
                    FileType = fileMatch.Groups[2].Value.ToUpperInvariant(),
                    Tracks = []
                };

                cueFile.Entries.Add(entry);
                track = null;
                continue;
            }

            var trackMatch = TrackRegex.Match(line);

            if (trackMatch.Success && entry is not null)
            {
                track = new CueTrack
                {
                    Number = int.Parse(trackMatch.Groups[1].Value),
                    DataType = trackMatch.Groups[2].Value.ToUpperInvariant(),
                    Indexes = []
                };

                entry.Tracks.Add(track);

                continue;
            }

            var indexMatch = IndexRegex.Match(line);

            if (indexMatch.Success && track is not null)
            {
                track.Indexes.Add(new CueIndex
                {
                    Number = int.Parse(indexMatch.Groups[1].Value),
                    Position = new MsfPosition(int.Parse(indexMatch.Groups[2].Value), int.Parse(indexMatch.Groups[3].Value), int.Parse(indexMatch.Groups[4].Value))
                });
            }
        }

        return cueFile;
    }
}