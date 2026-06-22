using PBP.Core.Models;
using System.Text.RegularExpressions;

namespace PBP.Core.Services;

public static class CueFileReader
{
    private static readonly Regex FileRegex = new(@"^FILE ""(.*?)"" (.*?)\s*$");
    private static readonly Regex TrackRegex = new(@"^\s*TRACK (\d+) (.*?)\s*$");
    private static readonly Regex IndexRegex = new(@"^\s*INDEX (\d+) (\d+:\d+:\d+)\s*$");

    public static CueFile Dummy() => new()
    {
        FileEntries =
        [
            new CueFileEntry
            {
                FileType = "BINARY",
                Tracks =
                [
                    new CueTrack
                    {
                        DataType = CueDataTypes.Data,
                        Number = 1,
                        Indexes = [new CueIndex { Number = 1, Position = new IndexPosition() }]
                    }
                ]
            }
        ]
    };

    public static CueFile Read(string file)
    {
        var cueFile = new CueFile { Path = file };
        CueFileEntry? cueFileEntry = null;
        CueTrack? cueTrack = null;

        foreach (var line in File.ReadAllLines(file))
        {
            var fileMatch = FileRegex.Match(line);
            var trackMatch = TrackRegex.Match(line);
            var indexMatch = IndexRegex.Match(line);

            if (fileMatch.Success)
            {
                cueFileEntry = new CueFileEntry
                {
                    FileName = fileMatch.Groups[1].Value,
                    FileType = fileMatch.Groups[2].Value,
                    Tracks = []
                };
                cueFile.FileEntries.Add(cueFileEntry);
            }
            else if (trackMatch.Success)
            {
                cueTrack = new CueTrack
                {
                    Number = int.Parse(trackMatch.Groups[1].Value),
                    DataType = trackMatch.Groups[2].Value,
                    Indexes = []
                };
                cueFileEntry!.Tracks.Add(cueTrack);
            }
            else if (indexMatch.Success)
            {
                var pos = indexMatch.Groups[2].Value.Split(':');

                cueTrack!.Indexes.Add(new CueIndex
                {
                    Number = int.Parse(indexMatch.Groups[1].Value),
                    Position = new IndexPosition
                    {
                        Minutes = int.Parse(pos[0]),
                        Seconds = int.Parse(pos[1]),
                        Frames = int.Parse(pos[2])
                    }
                });
            }
        }

        return cueFile;
    }

    public static CueFile Parse(string content)
    {
        var cueFile = new CueFile();
        CueFileEntry? cueFileEntry = null;
        CueTrack? cueTrack = null;

        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

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
                    FileType = fileMatch.Groups[2].Value, 
                    Tracks = [] 
                };
                cueFile.FileEntries.Add(cueFileEntry);
            }
            else if (trackMatch.Success)
            {
                cueTrack = new CueTrack
                { 
                    Number = int.Parse(trackMatch.Groups[1].Value), 
                    DataType = trackMatch.Groups[2].Value, 
                    Indexes = [] 
                };
                cueFileEntry!.Tracks.Add(cueTrack);
            }
            else if (indexMatch.Success)
            {
                var pos = indexMatch.Groups[2].Value.Split(':');

                cueTrack!.Indexes.Add(new CueIndex
                {
                    Number = int.Parse(indexMatch.Groups[1].Value),
                    Position = new IndexPosition 
                    { 
                        Minutes = int.Parse(pos[0]), 
                        Seconds = int.Parse(pos[1]), 
                        Frames = int.Parse(pos[2])
                    }
                });
            }
        }
        return cueFile;
    }
}