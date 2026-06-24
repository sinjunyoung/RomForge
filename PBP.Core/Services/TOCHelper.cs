using PBP.Core.Enums;
using PBP.Core.Models;

namespace PBP.Core.Services;

public static class TOCHelper
{
    public static int FromBinaryDecimal(int value) => (value >> 4) * 10 + (value & 0x0F);

    public static int ToBinaryDecimal(int value) => ((value / 10) << 4) | (value % 10);

    public static int LBAToMinute(int lba) => lba / 75 / 60;

    public static int LBAToSecond(int lba) => lba / 75 % 60;

    public static int LBAToFrame(int lba) => lba % 75;

    public static CueFile TOCtoCUE(List<TocEntry> tocEntries, string fileName)
    {
        var cueFile = new CueFile();
        var cueFileEntry = new CueFileEntry
        {
            FileName = fileName,
            FileType = "BINARY"
        };

        cueFile.Entries.Add(cueFileEntry);

        var audioLeadIn = new MsfPosition { Seconds = 2 };

        foreach (var track in tocEntries)
        {
            var position = new MsfPosition { Minutes = track.Minutes, Seconds = track.Seconds, Frames = track.Frames };
            var indexes = new List<CueIndex>();

            if (track.TrackType == TrackType.Audio)
                indexes.Add(new CueIndex { Number = 0, Position = position - audioLeadIn });

            indexes.Add(new CueIndex { Number = 1, Position = position });

            cueFileEntry.Tracks.Add(new CueTrack
            {
                DataType = track.TrackType == TrackType.Audio ? "AUDIO" : "MODE2/2352",
                Indexes = indexes,
                Number = track.TrackNo
            });
        }

        return cueFile;
    }
}