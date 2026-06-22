using PBP.Core.Models;

namespace PBP.Core.Services;

public static class TocBuilder
{
    public static byte[] BuildToc(CueFile cue, uint isoSize)
    {
        var tracks = cue.FileEntries.SelectMany(f => f.Tracks).ToList();
        var tocData = new byte[0xA * (tracks.Count + 3)];
        var buf = new byte[0xA];

        var leadOut = PositionFromFrames(isoSize / 2352);
        var ctr = 0;

        buf[0] = GetTrackType(tracks[0].DataType);
        buf[1] = 0x00; buf[2] = 0xA0; buf[3] = 0x00; buf[4] = 0x00;
        buf[5] = 0x00; buf[6] = 0x00;
        buf[7] = ToBcd(tracks[0].Number);
        buf[8] = ToBcd(0x20);
        buf[9] = 0x00;
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        buf[0] = GetTrackType(tracks[^1].DataType);
        buf[2] = 0xA1;
        buf[7] = ToBcd(tracks[^1].Number);
        buf[8] = 0x00;
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        buf[0] = 0x01;
        buf[2] = 0xA2;
        buf[7] = ToBcd(leadOut.Minutes);
        buf[8] = ToBcd(leadOut.Seconds);
        buf[9] = ToBcd(leadOut.Frames);
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        foreach (var track in tracks)
        {
            buf[0] = GetTrackType(track.DataType);
            buf[1] = 0x00;
            buf[2] = ToBcd(track.Number);

            var pos = track.Indexes.First(i => i.Number == 1).Position;
            buf[3] = ToBcd(pos.Minutes);
            buf[4] = ToBcd(pos.Seconds);
            buf[5] = ToBcd(pos.Frames);
            buf[6] = 0x00;

            pos += 150;
            buf[7] = ToBcd(pos.Minutes);
            buf[8] = ToBcd(pos.Seconds);
            buf[9] = ToBcd(pos.Frames);

            Array.Copy(buf, 0, tocData, ctr, 0xA);
            ctr += 0xA;
        }

        return tocData;
    }

    public static byte[] BuildSingleTrackToc(uint isoSize) => BuildToc(CueFileReader.Dummy(), isoSize);

    public static byte GetTrackType(string dataType) => dataType switch
    {
        CueDataTypes.Data => 0x41,
        CueDataTypes.Audio => 0x01,
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "MODE2/2352 또는 AUDIO만 지원 가능합니다.")
    };

    public static byte ToBcd(int value) => (byte)((value / 10) * 0x10 + (value % 10));

    public static IndexPosition PositionFromFrames(long frames)
    {
        var totalSeconds = (int)(frames / 75);

        return new IndexPosition { Minutes = totalSeconds / 60, Seconds = totalSeconds % 60, Frames = (int)(frames % 75) };
    }
}