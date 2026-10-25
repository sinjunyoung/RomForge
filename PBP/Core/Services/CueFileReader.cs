using CHD.Core.Models;
using PBP.Core.Constants;
using PBP.Core.Models;
using System.Text.RegularExpressions;

namespace PBP.Core.Services;

public static class CueFileReader
{
    private static readonly Regex FileRegex = new(@"^FILE\s+""(.*?)""\s+(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrackRegex = new(@"^\s*TRACK\s+(\d+)\s+(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IndexRegex = new(@"^\s*INDEX\s+(\d+)\s+(\d+:\d+:\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CueFile Dummy() => new()
    {
        Entries =
        [
            new CueFileEntry
            {
                FileType = "BINARY",
                Tracks =
                [
                    new CueTrack
                    {
                        DataType = CueFormatStrings.Mode2_2352,
                        Number = 1,
                        Indexes = [new CueIndex { Number = 1, Position = new MsfPosition() }]
                    }
                ]
            }
        ]
    };

    public static CueFile Read(string file)
    {
        var cueFile = new CueFile { FilePath = file };

        ParseLines(File.ReadLines(file), cueFile);

        return cueFile;
    }

    public static CueFile Parse(string content)
    {
        var cueFile = new CueFile();
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

        ParseLines(lines, cueFile);

        return cueFile;
    }

    private static void ParseLines(IEnumerable<string> lines, CueFile cueFile)
    {
        CueFileEntry? cueFileEntry = null;
        CueTrack? cueTrack = null;

        foreach (var line in lines)
        {
            var fileMatch = FileRegex.Match(line);
            var trackMatch = TrackRegex.Match(line);
            var indexMatch = IndexRegex.Match(line);

            if (fileMatch.Success)
            {
                cueFileEntry = new CueFileEntry
                {
                    FileName = fileMatch.Groups[1].Value,
                    FileType = fileMatch.Groups[2].Value.ToUpperInvariant(),
                    Tracks = []
                };
                cueFile.Entries.Add(cueFileEntry);
            }
            else if (trackMatch.Success && cueFileEntry != null)
            {
                cueTrack = new CueTrack
                {
                    Number = int.Parse(trackMatch.Groups[1].Value),
                    DataType = trackMatch.Groups[2].Value.ToUpperInvariant(),
                    Indexes = []
                };
                cueFileEntry.Tracks.Add(cueTrack);
            }
            else if (indexMatch.Success && cueTrack != null)
            {
                var pos = indexMatch.Groups[2].Value.Split(':');

                cueTrack.Indexes.Add(new CueIndex
                {
                    Number = int.Parse(indexMatch.Groups[1].Value),
                    Position = new MsfPosition
                    {
                        Minutes = int.Parse(pos[0]),
                        Seconds = int.Parse(pos[1]),
                        Frames = int.Parse(pos[2])
                    }
                });
            }
        }
    }

    public static CueFile BuildCueFromChdInfo(ChdInfo info)
    {
        var entry = new CueFileEntry { FileType = "BINARY", Tracks = [] };
        long currentFrame = 0;

        foreach (var track in info.Tracks.Take(info.TrackCount))
        {
            var indexes = new List<CueIndex>();

            if (track.PreGap > 0)
            {
                indexes.Add(new CueIndex
                {
                    Number = 0,
                    Position = TocBuilder.PositionFromFrames(currentFrame)
                });
            }

            indexes.Add(new CueIndex
            {
                Number = 1,
                Position = TocBuilder.PositionFromFrames(currentFrame + track.PreGap)
            });

            entry.Tracks.Add(new CueTrack
            {
                Number = track.TrackNumber,
                DataType = MapChdTrackTypeToCue(track.TrackType),
                Indexes = indexes
            });

            currentFrame += track.Frames;
        }

        return new CueFile { Entries = [entry] };
    }

    private static string MapChdTrackTypeToCue(string? chdTrackType)
    {
        if (string.IsNullOrEmpty(chdTrackType))
        {
            return CueFormatStrings.Mode2_2352;
        }

        if (chdTrackType.Contains("AUDIO", StringComparison.InvariantCultureIgnoreCase))
        {
            return CueFormatStrings.Audio;
        }

        if (chdTrackType.Contains("MODE1_2352", StringComparison.InvariantCultureIgnoreCase) ||
            chdTrackType.Contains("MODE1/2352", StringComparison.InvariantCultureIgnoreCase))
        {
            return CueFormatStrings.Mode1_2352;
        }

        if (chdTrackType.Contains("MODE2_2336", StringComparison.InvariantCultureIgnoreCase) ||
            chdTrackType.Contains("MODE2/2336", StringComparison.InvariantCultureIgnoreCase))
        {
            return CueFormatStrings.Mode2_2336;
        }

        return CueFormatStrings.Mode2_2352;
    }
}